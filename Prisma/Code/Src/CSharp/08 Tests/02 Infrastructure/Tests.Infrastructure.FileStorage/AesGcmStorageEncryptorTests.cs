using System.Security.Cryptography;
using System.Text;
using ExxerCube.Prisma.Infrastructure.FileStorage;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// Unit tests for <see cref="AesGcmStorageEncryptor"/>.
/// Covers round-trip, purpose isolation, wrong-key rejection and cancellation.
/// </summary>
public class AesGcmStorageEncryptorTests
{
    // A known-valid 32-byte (256-bit) Base64-encoded test key.
    private static readonly string ValidKey = Convert.ToBase64String(
        Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

    private static AesGcmStorageEncryptor BuildEncryptor(string? keyOverride = null)
    {
        var keyValue = keyOverride ?? ValidKey;
        var config = Substitute.For<IConfiguration>();
        config[AesGcmStorageEncryptor.ConfigurationKey].Returns(keyValue);
        return new AesGcmStorageEncryptor(config, NullLogger<AesGcmStorageEncryptor>.Instance);
    }

    // ── Constructor validation ────────────────────────────────────────────────

    /// <summary>
    /// Constructor throws when configuration key is absent.
    /// </summary>
    [Fact]
    public void Constructor_MissingKey_ThrowsInvalidOperationException()
    {
        var config = Substitute.For<IConfiguration>();
        config[AesGcmStorageEncryptor.ConfigurationKey].Returns((string?)null);

        Should.Throw<InvalidOperationException>(() =>
            new AesGcmStorageEncryptor(config, NullLogger<AesGcmStorageEncryptor>.Instance));
    }

    /// <summary>
    /// Constructor throws when the Base64 string decodes to fewer than 32 bytes.
    /// </summary>
    [Fact]
    public void Constructor_KeyTooShort_ThrowsInvalidOperationException()
    {
        var shortKey = Convert.ToBase64String(new byte[16]); // 128-bit — too short
        Should.Throw<InvalidOperationException>(() => BuildEncryptor(shortKey))
              .Message.ShouldContain("32 bytes");
    }

    /// <summary>
    /// Constructor throws when the configuration value is not valid Base64.
    /// </summary>
    [Fact]
    public void Constructor_InvalidBase64_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() => BuildEncryptor("NOT_VALID_BASE64!!!"))
              .Message.ShouldContain("not valid Base64");
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypt then decrypt with the same purpose returns the original plaintext.
    /// </summary>
    [Fact]
    public async Task EncryptAsync_ThenDecryptAsync_SamePurpose_ReturnsOriginalBytes()
    {
        var encryptor = BuildEncryptor();
        var plaintext = Encoding.UTF8.GetBytes("PRISMA-CONFIDENTIAL-DOCUMENT");
        var purpose = "document-download";
        var ct = TestContext.Current.CancellationToken;

        var encResult = await encryptor.EncryptAsync(plaintext, purpose, ct);
        encResult.IsSuccess.ShouldBeTrue();
        encResult.Value.ShouldNotBeNull();

        var decResult = await encryptor.DecryptAsync(encResult.Value!, purpose, ct);
        decResult.IsSuccess.ShouldBeTrue();
        decResult.Value.ShouldBe(plaintext);
    }

    /// <summary>
    /// Each call to EncryptAsync produces a different ciphertext blob (random nonce).
    /// </summary>
    [Fact]
    public async Task EncryptAsync_SamePlaintext_ProducesDifferentCiphertextEachCall()
    {
        var encryptor = BuildEncryptor();
        var plaintext = new byte[] { 0x01, 0x02, 0x03 };
        var ct = TestContext.Current.CancellationToken;

        var r1 = await encryptor.EncryptAsync(plaintext, "doc", ct);
        var r2 = await encryptor.EncryptAsync(plaintext, "doc", ct);

        r1.IsSuccess.ShouldBeTrue();
        r2.IsSuccess.ShouldBeTrue();

        // Blobs differ because nonce is random per write
        r1.Value.ShouldNotBe(r2.Value);
    }

    // ── Purpose isolation ─────────────────────────────────────────────────────

    /// <summary>
    /// Decrypting with a different purpose string fails — HKDF derives a different sub-key,
    /// which causes the GCM authentication tag to be rejected.
    /// </summary>
    [Fact]
    public async Task DecryptAsync_WrongPurpose_ReturnsFailureResult()
    {
        var encryptor = BuildEncryptor();
        var plaintext = Encoding.UTF8.GetBytes("sensitive payload");
        var ct = TestContext.Current.CancellationToken;

        var encResult = await encryptor.EncryptAsync(plaintext, "purpose-A", ct);
        encResult.IsSuccess.ShouldBeTrue();

        var decResult = await encryptor.DecryptAsync(encResult.Value!, "purpose-B", ct);

        decResult.IsSuccess.ShouldBeFalse();
        decResult.IsFailure.ShouldBeTrue();
        decResult.Error.ShouldNotBeNullOrEmpty();
    }

    // ── Wrong key ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypting with a different master key fails at the GCM tag level.
    /// </summary>
    [Fact]
    public async Task DecryptAsync_WrongKey_ReturnsFailureResult()
    {
        var encryptor = BuildEncryptor();

        // Different 32-byte key
        var altKey = Convert.ToBase64String(
            Enumerable.Range(1, 32).Select(i => (byte)(i + 100)).ToArray());
        var decryptorWithAltKey = BuildEncryptor(altKey);

        var plaintext = Encoding.UTF8.GetBytes("secret bytes");
        var ct = TestContext.Current.CancellationToken;

        var encResult = await encryptor.EncryptAsync(plaintext, "doc", ct);
        encResult.IsSuccess.ShouldBeTrue();

        var decResult = await decryptorWithAltKey.DecryptAsync(encResult.Value!, "doc", ct);
        decResult.IsSuccess.ShouldBeFalse();
        decResult.IsFailure.ShouldBeTrue();
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypting a truncated blob returns failure (too short to contain nonce+tag+payload).
    /// </summary>
    [Fact]
    public async Task DecryptAsync_TruncatedBlob_ReturnsFailureResult()
    {
        var encryptor = BuildEncryptor();
        var tooShort = new byte[5]; // 5 < 12 (nonce) + 16 (tag) + 1

        var result = await encryptor.DecryptAsync(tooShort, "doc", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Empty purpose string returns a failure result (not an exception).
    /// </summary>
    [Fact]
    public async Task EncryptAsync_EmptyPurpose_ReturnsFailureResult()
    {
        var encryptor = BuildEncryptor();
        var result = await encryptor.EncryptAsync(new byte[] { 1 }, string.Empty, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Decrypting with an empty purpose string returns a failure result (not an exception).
    /// </summary>
    [Fact]
    public async Task DecryptAsync_EmptyPurpose_ReturnsFailureResult()
    {
        var encryptor = BuildEncryptor();
        var blob = new byte[30]; // any blob > nonce+tag+1 minimum

        var result = await encryptor.DecryptAsync(blob, string.Empty, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    /// <summary>
    /// EncryptAsync returns a cancelled result when the token is already cancelled.
    /// </summary>
    [Fact]
    public async Task EncryptAsync_CancelledToken_ReturnsCancelledResult()
    {
        var encryptor = BuildEncryptor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await encryptor.EncryptAsync(new byte[] { 1 }, "doc", cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// DecryptAsync returns a cancelled result when the token is already cancelled.
    /// </summary>
    [Fact]
    public async Task DecryptAsync_CancelledToken_ReturnsCancelledResult()
    {
        var encryptor = BuildEncryptor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await encryptor.DecryptAsync(new byte[30], "doc", cts.Token);
        result.IsCancelled().ShouldBeTrue();
    }

    // ── Blob layout verification ──────────────────────────────────────────────

    /// <summary>
    /// Verifies the on-disk blob layout: first 12 bytes are the nonce, next 16 are the GCM tag,
    /// remainder is ciphertext. The nonce is random (not zero) and the ciphertext portion
    /// does not equal the plaintext.
    /// </summary>
    [Fact]
    public async Task EncryptAsync_BlobLayout_HasNonceTagAndCiphertext()
    {
        var encryptor = BuildEncryptor();
        var plaintext = new byte[] { 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45 };
        var ct = TestContext.Current.CancellationToken;

        var result = await encryptor.EncryptAsync(plaintext, "doc", ct);

        result.IsSuccess.ShouldBeTrue();
        var blob = result.Value!;

        // Total length: 12 (nonce) + 16 (tag) + plaintext.Length
        blob.Length.ShouldBe(12 + 16 + plaintext.Length);

        // Nonce is the first 12 bytes — should not be all zeros (random)
        var nonce = blob[..12];
        nonce.All(b => b == 0).ShouldBeFalse();

        // Ciphertext portion (after nonce+tag) should differ from plaintext
        var cipherPart = blob[28..];
        cipherPart.ShouldNotBe(plaintext);
    }
}

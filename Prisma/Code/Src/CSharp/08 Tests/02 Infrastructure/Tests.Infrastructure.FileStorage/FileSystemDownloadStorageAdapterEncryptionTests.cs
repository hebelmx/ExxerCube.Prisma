using System.Text;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.FileStorage;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// End-to-end tests for <see cref="FileSystemDownloadStorageAdapter"/> encryption at rest.
/// These tests write through the adapter, read the raw on-disk bytes directly, and assert
/// (a) the disk file is ciphertext — NOT the plaintext, (b) the disk file does NOT contain
/// the plaintext purpose string, then read back through the adapter and verify round-trip.
/// </summary>
public sealed class FileSystemDownloadStorageAdapterEncryptionTests : IDisposable
{
    // Stable test plaintext marker that can be searched inside the raw on-disk bytes.
    private static readonly byte[] KnownPlaintext =
        Encoding.UTF8.GetBytes("PRISMA-KNOWN-PLAINTEXT-MARKER-12345");

    // The purpose string used internally by the adapter (must stay in sync with
    // FileSystemDownloadStorageAdapter.EncryptionPurpose which is "document-download").
    private const string ExpectedPurpose = "document-download";

    private static readonly byte[] PurposeBytes = Encoding.UTF8.GetBytes(ExpectedPurpose);

    // A valid 32-byte AES-256 test key (never use in production).
    private static readonly string TestKey = Convert.ToBase64String(
        Enumerable.Range(42, 32).Select(i => (byte)i).ToArray());

    private readonly string _tempDir;
    private readonly FileSystemDownloadStorageAdapter _adapter;

    /// <summary>
    /// Initialises a fresh temp directory and an adapter wired with the AES-256-GCM encryptor.
    /// </summary>
    public FileSystemDownloadStorageAdapterEncryptionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new FileStorageOptions { StorageBasePath = _tempDir });

        var config = Substitute.For<IConfiguration>();
        config[AesGcmStorageEncryptor.ConfigurationKey].Returns(TestKey);

        var encryptor = new AesGcmStorageEncryptor(config, NullLogger<AesGcmStorageEncryptor>.Instance);
        _adapter = new FileSystemDownloadStorageAdapter(
            NullLogger<FileSystemDownloadStorageAdapter>.Instance,
            options,
            encryptor);
    }

    // ── Main E2E assertions ───────────────────────────────────────────────────

    /// <summary>
    /// On-disk bytes must NOT equal the plaintext document bytes.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_WrittenFile_RawDiskBytesAreNotPlaintext()
    {
        var ct = TestContext.Current.CancellationToken;

        var saveResult = await _adapter.SaveFileAsync(
            KnownPlaintext, "doc.pdf", FileFormat.Pdf, ct);

        saveResult.IsSuccess.ShouldBeTrue();
        var storagePath = saveResult.Value!;

        var rawDiskBytes = await File.ReadAllBytesAsync(storagePath, ct);

        // The raw disk bytes must differ from the original plaintext.
        rawDiskBytes.ShouldNotBe(KnownPlaintext);
    }

    /// <summary>
    /// The on-disk bytes must NOT contain the known plaintext marker as a sub-sequence.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_WrittenFile_RawDiskBytesDoNotContainKnownPlaintextMarker()
    {
        var ct = TestContext.Current.CancellationToken;

        var saveResult = await _adapter.SaveFileAsync(
            KnownPlaintext, "doc.pdf", FileFormat.Pdf, ct);

        saveResult.IsSuccess.ShouldBeTrue();
        var rawDiskBytes = await File.ReadAllBytesAsync(saveResult.Value!, ct);

        ContainsSubsequence(rawDiskBytes, KnownPlaintext).ShouldBeFalse(
            "On-disk bytes must not contain the plaintext marker — file is not encrypted.");
    }

    /// <summary>
    /// The on-disk bytes must NOT contain the plaintext purpose string ("document-download").
    /// The purpose is used only for HKDF key derivation and must NEVER appear on disk.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_WrittenFile_RawDiskBytesDoNotContainPlaintextPurposeString()
    {
        var ct = TestContext.Current.CancellationToken;

        var saveResult = await _adapter.SaveFileAsync(
            KnownPlaintext, "doc.pdf", FileFormat.Pdf, ct);

        saveResult.IsSuccess.ShouldBeTrue();
        var rawDiskBytes = await File.ReadAllBytesAsync(saveResult.Value!, ct);

        ContainsSubsequence(rawDiskBytes, PurposeBytes).ShouldBeFalse(
            $"On-disk bytes must not contain the plaintext purpose string '{ExpectedPurpose}'.");
    }

    /// <summary>
    /// A full round-trip: save via adapter (encrypts) then read via adapter (decrypts) must
    /// return the original plaintext bytes.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_ThenReadFileAsync_SamePurpose_ReturnsOriginalBytes()
    {
        var ct = TestContext.Current.CancellationToken;

        var saveResult = await _adapter.SaveFileAsync(
            KnownPlaintext, "doc.pdf", FileFormat.Pdf, ct);

        saveResult.IsSuccess.ShouldBeTrue();
        var storagePath = saveResult.Value!;

        var readResult = await _adapter.ReadFileAsync(storagePath, ct);

        readResult.IsSuccess.ShouldBeTrue();
        readResult.Value.ShouldBe(KnownPlaintext);
    }

    /// <summary>
    /// ReadFileAsync on a path that does not exist returns a failure result (not an exception).
    /// </summary>
    [Fact]
    public async Task ReadFileAsync_NonExistentPath_ReturnsFailureResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var fakePath = Path.Combine(_tempDir, "does-not-exist.bin");

        var result = await _adapter.ReadFileAsync(fakePath, ct);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// ReadFileAsync on a file written by an adapter using a different key returns a failure
    /// result (GCM authentication tag mismatch).
    /// </summary>
    [Fact]
    public async Task ReadFileAsync_WrongKey_ReturnsFailureResult()
    {
        var ct = TestContext.Current.CancellationToken;

        // Save with the default test adapter (TestKey)
        var saveResult = await _adapter.SaveFileAsync(
            KnownPlaintext, "doc.pdf", FileFormat.Pdf, ct);
        saveResult.IsSuccess.ShouldBeTrue();
        var storagePath = saveResult.Value!;

        // Build a second adapter with a different key
        var altKey = Convert.ToBase64String(
            Enumerable.Range(0, 32).Select(i => (byte)(i + 200)).ToArray());
        var altConfig = Substitute.For<IConfiguration>();
        altConfig[AesGcmStorageEncryptor.ConfigurationKey].Returns(altKey);
        var altEncryptor = new AesGcmStorageEncryptor(altConfig, NullLogger<AesGcmStorageEncryptor>.Instance);
        var altAdapter = new FileSystemDownloadStorageAdapter(
            NullLogger<FileSystemDownloadStorageAdapter>.Instance,
            Options.Create(new FileStorageOptions { StorageBasePath = _tempDir }),
            altEncryptor);

        var readResult = await altAdapter.ReadFileAsync(storagePath, ct);

        readResult.IsSuccess.ShouldBeFalse();
        readResult.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// On-disk blob size is strictly larger than plaintext (by at least 28 bytes: 12 nonce + 16 tag).
    /// This is a structural assertion on the encryption overhead.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_WrittenFile_BlobIsLargerThanPlaintextByAtLeastNonceAndTagSize()
    {
        var ct = TestContext.Current.CancellationToken;

        var saveResult = await _adapter.SaveFileAsync(
            KnownPlaintext, "doc.pdf", FileFormat.Pdf, ct);

        saveResult.IsSuccess.ShouldBeTrue();
        var rawDiskBytes = await File.ReadAllBytesAsync(saveResult.Value!, ct);

        const int nonceBytes = 12;
        const int tagBytes = 16;
        rawDiskBytes.Length.ShouldBeGreaterThanOrEqualTo(KnownPlaintext.Length + nonceBytes + tagBytes);
    }

    // ── SaveFileAsync pre-existing tests stay green ───────────────────────────

    /// <summary>
    /// SaveFileAsync creates the directory structure if it does not exist.
    /// </summary>
    [Fact]
    public async Task SaveFileAsync_ValidFile_CreatesDirectoryStructure()
    {
        var fileContent = new byte[] { 1, 2, 3 };
        var ct = TestContext.Current.CancellationToken;

        var result = await _adapter.SaveFileAsync(fileContent, "test.xml", FileFormat.Xml, ct);

        result.IsSuccess.ShouldBeTrue();
        var directory = Path.GetDirectoryName(result.Value);
        directory.ShouldNotBeNull();
        Directory.Exists(directory).ShouldBeTrue();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="needle"/> appears as a contiguous
    /// sub-sequence anywhere within <paramref name="haystack"/>.
    /// </summary>
    private static bool ContainsSubsequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.IsEmpty) return true;
        if (haystack.Length < needle.Length) return false;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i..].StartsWith(needle))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.FileStorage;

/// <summary>
/// AES-256-GCM implementation of <see cref="IStorageEncryptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key delivery:</strong> The master key is a 32-byte (256-bit) value encoded as
/// Base64 supplied via the configuration key <c>Storage:EncryptionKey</c> or the environment
/// variable <c>PRISMA_STORAGE_KEY</c>. Never commit a real key to source control — supply it
/// via environment variable injection (CI secrets, Key Vault reference, docker-compose env).
/// Example for local development (in user secrets or .env, never appsettings.json):
/// <code>
/// Storage__EncryptionKey=&lt;base64-of-32-random-bytes&gt;
/// </code>
/// Generate a key with: <c>openssl rand -base64 32</c>
/// </para>
/// <para>
/// <strong>Sub-key derivation:</strong> A per-purpose sub-key is derived via HKDF-SHA256
/// (RFC 5869) using the master key as IKM and the purpose string as the info label.
/// This provides key-domain separation without storing the purpose on disk.
/// </para>
/// <para>
/// <strong>On-disk blob layout:</strong>
/// <c>[12 bytes nonce][16 bytes GCM auth tag][N bytes ciphertext]</c>.
/// The nonce is random per write (RandomNumberGenerator). The GCM tag provides
/// authenticated encryption — any tamper of the blob or a wrong purpose/key causes
/// decryption to fail with a failure Result.
/// </para>
/// <para>
/// <strong>Purpose string protection:</strong> The purpose string is used ONLY as HKDF
/// info during key derivation and is NEVER written to disk. The caller must re-supply the
/// same purpose string at read time. A mismatch produces a different sub-key, causing
/// the GCM tag verification to fail (authenticated decryption failure).
/// </para>
/// <para>
/// <strong>Key rotation (NOT handled):</strong> This implementation does not support key
/// rotation or versioned keys. Rotating the master key requires re-encrypting all stored
/// blobs. Implement a key-version header and a migration job before enabling key rotation.
/// </para>
/// </remarks>
public sealed class AesGcmStorageEncryptor : IStorageEncryptor
{
    /// <summary>Configuration key for the Base64-encoded 32-byte master key.</summary>
    public const string ConfigurationKey = "Storage:EncryptionKey";

    /// <summary>Nonce size for AES-256-GCM (96 bits, recommended by NIST SP 800-38D).</summary>
    private const int NonceSizeBytes = 12;

    /// <summary>Authentication tag size for AES-256-GCM (128 bits).</summary>
    private const int TagSizeBytes = 16;

    /// <summary>Required master key length in bytes (AES-256 = 32 bytes).</summary>
    private const int KeySizeBytes = 32;

    /// <summary>HKDF output length matches the AES-256 key size.</summary>
    private const int DerivedKeySizeBytes = 32;

    private readonly byte[] _masterKey;
    private readonly ILogger<AesGcmStorageEncryptor> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AesGcmStorageEncryptor"/>.
    /// </summary>
    /// <param name="configuration">Application configuration — must contain <c>Storage:EncryptionKey</c>.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> or <paramref name="logger"/> is null.</exception>
    public AesGcmStorageEncryptor(IConfiguration configuration, ILogger<AesGcmStorageEncryptor> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        var raw = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Storage encryption key is missing. Set '{ConfigurationKey}' in configuration " +
                "or the PRISMA_STORAGE_KEY environment variable to a Base64-encoded 32-byte value. " +
                "Generate with: openssl rand -base64 32");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(raw);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Storage encryption key at '{ConfigurationKey}' is not valid Base64.", ex);
        }

        if (keyBytes.Length != KeySizeBytes)
        {
            throw new InvalidOperationException(
                $"Storage encryption key must be exactly {KeySizeBytes} bytes (256 bits). " +
                $"Provided key is {keyBytes.Length} bytes. Generate with: openssl rand -base64 32");
        }

        _masterKey = keyBytes;
    }

    /// <inheritdoc />
    public Task<Result<byte[]>> EncryptAsync(
        byte[] plaintext,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<byte[]>());

        ArgumentNullException.ThrowIfNull(plaintext);
        if (string.IsNullOrWhiteSpace(purpose))
            return Task.FromResult(Result<byte[]>.WithFailure("Encryption purpose must not be null or empty."));

        try
        {
            var subKey = DeriveSubKey(purpose);
            var nonce = new byte[NonceSizeBytes];
            RandomNumberGenerator.Fill(nonce);

            var cipherBytes = new byte[plaintext.Length];
            var tag = new byte[TagSizeBytes];

            using var aes = new AesGcm(subKey, TagSizeBytes);
            aes.Encrypt(nonce, plaintext, cipherBytes, tag);

            // Layout: [nonce (12)][tag (16)][ciphertext]
            var blob = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, blob, 0, NonceSizeBytes);
            Buffer.BlockCopy(tag, 0, blob, NonceSizeBytes, TagSizeBytes);
            Buffer.BlockCopy(cipherBytes, 0, blob, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

            _logger.LogDebug(
                "Encrypted {PlaintextLength} bytes → {BlobLength} byte blob (purpose key-domain applied, not stored)",
                plaintext.Length, blob.Length);

            return Task.FromResult(Result<byte[]>.Success(blob));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed");
            return Task.FromResult(Result<byte[]>.WithFailure($"Encryption failed: {ex.Message}", default, ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<byte[]>> DecryptAsync(
        byte[] ciphertext,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<byte[]>());

        ArgumentNullException.ThrowIfNull(ciphertext);
        if (string.IsNullOrWhiteSpace(purpose))
            return Task.FromResult(Result<byte[]>.WithFailure("Decryption purpose must not be null or empty."));

        var minBlobSize = NonceSizeBytes + TagSizeBytes + 1;
        if (ciphertext.Length < minBlobSize)
        {
            return Task.FromResult(Result<byte[]>.WithFailure(
                $"Ciphertext blob is too short ({ciphertext.Length} bytes). " +
                $"Minimum is {minBlobSize} bytes [nonce({NonceSizeBytes})+tag({TagSizeBytes})+1]."));
        }

        try
        {
            var subKey = DeriveSubKey(purpose);

            var nonce = ciphertext[..NonceSizeBytes];
            var tag = ciphertext[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
            var encryptedPayload = ciphertext[(NonceSizeBytes + TagSizeBytes)..];

            var plaintext = new byte[encryptedPayload.Length];

            using var aes = new AesGcm(subKey, TagSizeBytes);
            aes.Decrypt(nonce, encryptedPayload, tag, plaintext);

            _logger.LogDebug("Decrypted {BlobLength} byte blob → {PlaintextLength} bytes", ciphertext.Length, plaintext.Length);

            return Task.FromResult(Result<byte[]>.Success(plaintext));
        }
        catch (AuthenticationTagMismatchException ex)
        {
            _logger.LogWarning(ex, "GCM authentication tag mismatch — wrong key, wrong purpose, or blob tampered");
            return Task.FromResult(Result<byte[]>.WithFailure(
                "Decryption failed: authentication tag mismatch. The blob may be corrupt, " +
                "the key may have changed, or the purpose string does not match the one used during encryption.",
                default, ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed");
            return Task.FromResult(Result<byte[]>.WithFailure($"Decryption failed: {ex.Message}", default, ex));
        }
    }

    /// <summary>
    /// Derives a 256-bit sub-key from the master key and the purpose string using HKDF-SHA256.
    /// The purpose string provides per-domain key isolation without being stored on disk.
    /// </summary>
    private byte[] DeriveSubKey(string purpose)
    {
        var info = Encoding.UTF8.GetBytes(purpose);
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, _masterKey, DerivedKeySizeBytes, info: info);
    }
}

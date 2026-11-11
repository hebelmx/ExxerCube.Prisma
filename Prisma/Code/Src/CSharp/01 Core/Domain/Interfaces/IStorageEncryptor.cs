namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the vendor-agnostic storage encryption seam.
/// Local implementations use AES-256-GCM; cloud implementations may delegate to a KMS.
/// </summary>
/// <remarks>
/// The <c>purpose</c> parameter is a logical label (e.g. "document-download")
/// used for key isolation via HKDF sub-key derivation. It is NEVER persisted on disk in
/// plaintext — callers must supply the same purpose string at read time.
/// </remarks>
public interface IStorageEncryptor
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using a sub-key derived from the master key
    /// and the supplied <paramref name="purpose"/> string.
    /// </summary>
    /// <param name="plaintext">The raw bytes to protect.</param>
    /// <param name="purpose">
    /// A logical label for key isolation (e.g. "document-download"). Must match the value
    /// used at decrypt time. Never stored on disk.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the ciphertext blob on success, or an error
    /// message on failure. The blob layout is: [12-byte nonce][16-byte GCM tag][ciphertext].
    /// </returns>
    Task<Result<byte[]>> EncryptAsync(
        byte[] plaintext,
        string purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts a ciphertext blob produced by <see cref="EncryptAsync"/>.
    /// </summary>
    /// <param name="ciphertext">
    /// The blob in the form [12-byte nonce][16-byte GCM tag][ciphertext bytes].
    /// </param>
    /// <param name="purpose">
    /// The same logical label used during encryption. A mismatched purpose causes
    /// authenticated-decryption failure, which is returned as a failure Result.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the original plaintext on success, or an error
    /// on authentication/decryption failure. Never throws for control flow.
    /// </returns>
    Task<Result<byte[]>> DecryptAsync(
        byte[] ciphertext,
        string purpose,
        CancellationToken cancellationToken = default);
}

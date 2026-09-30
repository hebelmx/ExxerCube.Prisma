namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Defines the download storage service for persisting files to storage with deterministic paths.
/// </summary>
public interface IDownloadStorage
{
    /// <summary>
    /// Saves a downloaded file to storage with a deterministic path.
    /// </summary>
    /// <param name="fileContent">The file content to save.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="format">The file format.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the storage path where the file was saved, or an error.</returns>
    Task<Result<string>> SaveFileAsync(
        byte[] fileContent,
        string fileName,
        FileFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a deterministic storage path for a file.
    /// </summary>
    /// <param name="fileName">The original filename.</param>
    /// <param name="format">The file format.</param>
    /// <param name="checksum">The file checksum (optional, for checksum-based paths).</param>
    /// <returns>The deterministic storage path.</returns>
    string GenerateStoragePath(string fileName, FileFormat format, string? checksum = null);

    /// <summary>
    /// Reads and decrypts the file at the given storage path.
    /// </summary>
    /// <param name="storagePath">The path returned by <see cref="SaveFileAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result containing the original plaintext bytes on success, or an error if the file
    /// does not exist, is corrupt, or decryption fails.
    /// </returns>
    Task<Result<byte[]>> ReadFileAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}


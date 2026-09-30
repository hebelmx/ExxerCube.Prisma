namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the download tracker service for duplicate detection using file checksums.
/// </summary>
public interface IDownloadTracker
{
    /// <summary>
    /// Checks if a file with the specified checksum has already been downloaded.
    /// </summary>
    /// <param name="checksum">The SHA-256 checksum of the file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing true if duplicate exists, false otherwise, or an error.</returns>
    Task<Result<bool>> IsDuplicateAsync(
        string checksum,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file metadata for a file with the specified checksum if it exists.
    /// </summary>
    /// <param name="checksum">The SHA-256 checksum of the file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the file metadata if found, or an error.</returns>
    Task<Result<FileMetadata?>> GetFileMetadataByChecksumAsync(
        string checksum,
        CancellationToken cancellationToken = default);
}


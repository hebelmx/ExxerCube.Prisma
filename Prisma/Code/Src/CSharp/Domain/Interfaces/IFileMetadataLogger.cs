namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the file metadata logger service for logging file metadata to persistent storage.
/// </summary>
public interface IFileMetadataLogger
{
    /// <summary>
    /// Logs file metadata to persistent storage.
    /// </summary>
    /// <param name="metadata">The file metadata to log.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> LogFileMetadataAsync(
        FileMetadata metadata,
        CancellationToken cancellationToken = default);
}


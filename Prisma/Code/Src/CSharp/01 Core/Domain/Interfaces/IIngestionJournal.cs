namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Journal abstraction for tracking ingested documents with hash-based idempotency.
/// Implementations must be substitutable (Liskov) - any journal (EF Core, file-based, etc.)
/// should guarantee idempotency and manifest persistence.
/// </summary>
public interface IIngestionJournal
{
    /// <summary>
    /// Checks if a document has already been ingested based on hash and URL.
    /// </summary>
    /// <param name="contentHash">SHA256 hash of file content.</param>
    /// <param name="sourceUrl">Source URL from SIARA.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if document already exists in journal (skip re-download).</returns>
    /// <remarks>
    /// ITDD Contract:
    /// - MUST return true for exact hash+URL match (idempotency guarantee)
    /// - MUST complete within 100ms for typical queries
    /// - Liskov: All implementations must enforce uniqueness on (hash, sourceUrl) pair
    /// </remarks>
    Task<bool> ExistsAsync(
        string contentHash,
        string sourceUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new ingestion manifest entry.
    /// </summary>
    /// <param name="entry">The manifest entry to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <remarks>
    /// ITDD Contract:
    /// - MUST enforce unique constraint on (ContentHash, SourceUrl) - duplicate writes should succeed idempotently
    /// - MUST persist atomically (no partial writes)
    /// - Liskov: All implementations must support concurrent writes safely
    /// </remarks>
    Task RecordAsync(
        IngestionManifestEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a manifest entry by file ID.
    /// </summary>
    /// <param name="fileId">The unique file identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manifest entry if found, null otherwise.</returns>
    /// <remarks>
    /// ITDD Contract:
    /// - MUST return exact match by FileId
    /// - MUST complete within 100ms for typical queries
    /// - Liskov: All implementations must return null for non-existent IDs (not throw)
    /// </remarks>
    Task<IngestionManifestEntry?> GetByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);
}

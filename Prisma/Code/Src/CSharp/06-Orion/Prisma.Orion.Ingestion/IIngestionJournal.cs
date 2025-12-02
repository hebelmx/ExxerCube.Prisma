using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Ingestion journal abstraction for tracking processed documents (idempotency).
/// </summary>
public interface IIngestionJournal
{
    /// <summary>
    /// Checks if a document hash has already been processed.
    /// </summary>
    /// <param name="hash">SHA-256 hash of document content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if duplicate, false if new.</returns>
    Task<bool> IsDuplicateAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a processed document in the journal.
    /// </summary>
    /// <param name="hash">SHA-256 hash of document content.</param>
    /// <param name="storagePath">Full path where document was stored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordAsync(string hash, string storagePath, CancellationToken cancellationToken = default);
}

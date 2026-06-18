using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;

/// <summary>
/// Repository port for the append-only <see cref="ReprocessAuditEntry"/> log.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only contract:</b> Only <see cref="AppendAsync"/> (insert) and
/// <see cref="GetForContentHashAsync"/> (read) are exposed.  Existing entries are never
/// modified or deleted.  Each reprocess of the same statement adds a new entry alongside
/// the previous ones, preserving the full audit trail.
/// </para>
/// </remarks>
public interface IReprocessAuditRepository
{
    /// <summary>
    /// Appends a single <see cref="ReprocessAuditEntry"/> to the immutable log.
    /// </summary>
    /// <param name="entry">The audit entry to append.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the persisted entry on success;
    /// a cancelled result when <paramref name="ct"/> is signalled; failure on errors.
    /// </returns>
    Task<Result<ReprocessAuditEntry>> AppendAsync(ReprocessAuditEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="ReprocessAuditEntry"/> records for the given content hash,
    /// ordered chronologically by <see cref="ReprocessAuditEntry.ReprocessedAtUtc"/> ascending.
    /// </summary>
    /// <param name="contentHash">SHA-256 hex digest of the statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing an ordered read-only list.
    /// Returns an empty list (not a failure) when no reprocess history exists.
    /// </returns>
    Task<Result<IReadOnlyList<ReprocessAuditEntry>>> GetForContentHashAsync(
        string contentHash,
        CancellationToken ct = default);
}

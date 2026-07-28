using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Batch;

/// <summary>
/// Repository port for the durable <see cref="BatchExceptionLogEntry"/> dead-letter log
/// (VERIQAN-E3-S3). The durable log is the ops source of truth for failed batch items;
/// <see cref="BatchReport.ExceptionQueue"/> remains the in-memory summary for the immediate
/// batch response.
/// </summary>
public interface IBatchExceptionLogRepository
{
    /// <summary>
    /// Appends a single <see cref="BatchExceptionLogEntry"/> to the dead-letter log.
    /// </summary>
    /// <param name="entry">The dead-letter entry to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the persisted entry on success;
    /// a cancelled result when <paramref name="ct"/> is signalled; failure on errors.
    /// Callers must never let a failure here abort the batch — see
    /// <c>Batch.BatchProcessor</c>'s dead-letter write guard.
    /// </returns>
    Task<Result<BatchExceptionLogEntry>> AppendAsync(BatchExceptionLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="BatchExceptionLogEntry"/> rows recorded for the given
    /// <paramref name="batchId"/>, ordered chronologically by <see cref="BatchExceptionLogEntry.FailedAt"/>
    /// ascending. Backs <c>GET /exceptions?batchId=</c>.
    /// </summary>
    /// <param name="batchId">Identifier of the batch run to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing an ordered read-only list.
    /// Returns an empty list (not a failure) when no failures were recorded for that batch.
    /// </returns>
    Task<Result<IReadOnlyList<BatchExceptionLogEntry>>> GetByBatchIdAsync(Guid batchId, CancellationToken ct = default);
}

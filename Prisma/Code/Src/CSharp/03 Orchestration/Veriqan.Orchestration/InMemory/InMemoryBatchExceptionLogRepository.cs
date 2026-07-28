using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.InMemory;

/// <summary>
/// In-memory <see cref="IBatchExceptionLogRepository"/> backed by a <see cref="ConcurrentBag{T}"/>.
/// Suitable for integration tests and local dev without a database. Data is not persisted between
/// process restarts — hosts that need restart-survival must configure the SQL-backed
/// <c>EfBatchExceptionLogRepository</c> instead (see <c>VeriqanOrchestrationExtensions.AddVeriqan</c>).
/// </summary>
internal sealed class InMemoryBatchExceptionLogRepository : IBatchExceptionLogRepository
{
    private readonly ConcurrentBag<BatchExceptionLogEntry> _entries = new();

    /// <inheritdoc />
    public Task<Result<BatchExceptionLogEntry>> AppendAsync(
        BatchExceptionLogEntry entry,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<BatchExceptionLogEntry>());

        _entries.Add(entry);
        return Task.FromResult(Result<BatchExceptionLogEntry>.WithSuccess(entry));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<BatchExceptionLogEntry>>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<BatchExceptionLogEntry>>());

        IReadOnlyList<BatchExceptionLogEntry> result = _entries
            .Where(e => e.BatchId == batchId)
            .OrderBy(e => e.FailedAt)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<BatchExceptionLogEntry>>.WithSuccess(result));
    }
}

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.InMemory;

/// <summary>
/// In-memory <see cref="IReprocessAuditRepository"/> backed by a
/// <see cref="ConcurrentBag{T}"/>.  Suitable for integration tests and local dev.
/// Data is not persisted between process restarts.
/// </summary>
/// <remarks>
/// The append-only contract is honoured — no update or delete operations are exposed.
/// </remarks>
internal sealed class InMemoryReprocessAuditRepository : IReprocessAuditRepository
{
    private readonly ConcurrentBag<ReprocessAuditEntry> _entries = new();

    /// <inheritdoc />
    public Task<Result<ReprocessAuditEntry>> AppendAsync(
        ReprocessAuditEntry entry,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<ReprocessAuditEntry>());

        _entries.Add(entry);
        return Task.FromResult(Result<ReprocessAuditEntry>.WithSuccess(entry));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ReprocessAuditEntry>>> GetForContentHashAsync(
        string contentHash,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<ReprocessAuditEntry>>());

        IReadOnlyList<ReprocessAuditEntry> result = _entries
            .Where(e => e.ContentHash == contentHash)
            .OrderBy(e => e.ReprocessedAtUtc)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ReprocessAuditEntry>>.WithSuccess(result));
    }
}

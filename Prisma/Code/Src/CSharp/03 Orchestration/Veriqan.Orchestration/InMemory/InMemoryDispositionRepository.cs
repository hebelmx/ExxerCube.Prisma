using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.InMemory;

/// <summary>
/// In-memory <see cref="IDispositionRepository"/> stub for use when no database connection
/// string is configured (e.g. integration tests, local dev without SQL Server).
/// </summary>
/// <remarks>
/// This implementation stores dispositions in a process-local collection.
/// Data is not persisted between process restarts.
/// The append-only contract (AR-9) is honoured — no update or delete operations are exposed.
/// </remarks>
internal sealed class InMemoryDispositionRepository : IDispositionRepository
{
    private readonly ConcurrentBag<Disposition> _dispositions = new();

    /// <inheritdoc />
    public Task<Result<Disposition>> AppendAsync(
        Disposition disposition,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<Disposition>());

        _dispositions.Add(disposition);
        return Task.FromResult(Result<Disposition>.WithSuccess(disposition));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Disposition>>> GetForJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<IReadOnlyList<Disposition>>());

        IReadOnlyList<Disposition> result = _dispositions
            .Where(d => d.VerificationJobId == jobId)
            .OrderBy(d => d.DispositionedAtUtc)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<Disposition>>.WithSuccess(result));
    }
}

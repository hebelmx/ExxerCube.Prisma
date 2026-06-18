using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.InMemory;

/// <summary>
/// In-memory <see cref="IVerificationJobRepository"/> stub for use when no database connection
/// string is configured (e.g. integration tests, local dev without SQL Server).
/// </summary>
/// <remarks>
/// This implementation stores jobs in a process-local <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by content hash. Data is not persisted between process restarts.
/// </remarks>
internal sealed class InMemoryVerificationJobRepository : IVerificationJobRepository
{
    private readonly ConcurrentDictionary<string, VerificationJob> _byHash = new();

    /// <inheritdoc />
    public Task<Result<VerificationJob?>> FindByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<VerificationJob?>());

        _byHash.TryGetValue(contentHash, out var job);
        return Task.FromResult(Result<VerificationJob?>.WithSuccess(job));
    }

    /// <inheritdoc />
    public Task<Result<VerificationJob>> AddAsync(
        VerificationJob job,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<VerificationJob>());

        _byHash[job.ContentHash] = job;
        return Task.FromResult(Result<VerificationJob>.WithSuccess(job));
    }
}

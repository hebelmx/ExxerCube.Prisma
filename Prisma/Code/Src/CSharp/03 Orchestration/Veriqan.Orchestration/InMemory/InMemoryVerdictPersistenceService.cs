using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Orchestration.InMemory;

/// <summary>
/// In-memory <see cref="IVerdictPersistenceService"/> stub used when no database connection
/// string is configured (e.g. integration tests, local dev without SQL Server).
/// </summary>
/// <remarks>
/// Stores persisted verdicts and findings in process-local collections.
/// Data is not durable across process restarts.
/// Accessible via <see cref="GetVerdictForJob"/> and <see cref="GetFindingsForJob"/>
/// for test inspection.
/// </remarks>
internal sealed class InMemoryVerdictPersistenceService : IVerdictPersistenceService
{
    private readonly ConcurrentDictionary<Guid, JobVerdict> _verdicts = new();
    private readonly ConcurrentDictionary<Guid, List<Finding>> _findings = new();

    /// <inheritdoc />
    public Task<Result<JobVerdict>> PersistAsync(
        Guid jobId,
        VerdictSignal signal,
        IReadOnlyList<RuleFinding> findings,
        string engineVersion,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<JobVerdict>());

        ArgumentNullException.ThrowIfNull(findings);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);

        var verdict = new JobVerdict(
            id: Guid.NewGuid(),
            verificationJobId: jobId,
            signal: signal);

        _verdicts[jobId] = verdict;

        var findingEntities = new List<Finding>(findings.Count);
        foreach (var rf in findings)
        {
            findingEntities.Add(new Finding(
                id: Guid.NewGuid(),
                verificationJobId: jobId,
                checkId: rf.CheckId,
                verdict: rf.Verdict,
                engineVersion: engineVersion,
                expected: rf.Expected,
                observed: rf.Observed));
        }

        _findings[jobId] = findingEntities;

        return Task.FromResult(Result<JobVerdict>.WithSuccess(verdict));
    }

    /// <summary>
    /// Returns the persisted <see cref="JobVerdict"/> for <paramref name="jobId"/>, or
    /// <see langword="null"/> if no verdict has been persisted for that job yet.
    /// Intended for test inspection only.
    /// </summary>
    public JobVerdict? GetVerdictForJob(Guid jobId) =>
        _verdicts.TryGetValue(jobId, out var v) ? v : null;

    /// <summary>
    /// Returns the persisted <see cref="Finding"/> rows for <paramref name="jobId"/>, or
    /// an empty list if no findings have been persisted for that job yet.
    /// Intended for test inspection only.
    /// </summary>
    public IReadOnlyList<Finding> GetFindingsForJob(Guid jobId) =>
        _findings.TryGetValue(jobId, out var f) ? f : [];
}

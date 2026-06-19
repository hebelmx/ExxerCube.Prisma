using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Services;

/// <summary>
/// EF Core implementation of <see cref="IVerdictPersistenceService"/>.
/// Persists a <see cref="JobVerdict"/> and its associated <see cref="Finding"/> rows in
/// a single <c>SaveChangesAsync</c> call on <see cref="VeriqanDbContext"/>.
/// </summary>
internal sealed class EfVerdictPersistenceService : IVerdictPersistenceService
{
    private readonly VeriqanDbContext _context;
    private readonly ILogger<EfVerdictPersistenceService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfVerdictPersistenceService"/>.
    /// </summary>
    /// <param name="context">The Veriqan EF Core database context.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public EfVerdictPersistenceService(
        VeriqanDbContext context,
        ILogger<EfVerdictPersistenceService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<JobVerdict>> PersistAsync(
        Guid jobId,
        VerdictSignal signal,
        IReadOnlyList<RuleFinding> findings,
        string engineVersion,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<JobVerdict>();

        ArgumentNullException.ThrowIfNull(findings);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);

        var verdict = new JobVerdict(
            id: Guid.NewGuid(),
            verificationJobId: jobId,
            signal: signal);

        var findingEntities = MapFindings(jobId, findings, engineVersion);

        _logger.LogInformation(
            "Persisting JobVerdict {VerdictId} Signal={Signal} for Job {JobId} with {FindingCount} finding(s).",
            verdict.Id,
            signal,
            jobId,
            findingEntities.Count);

        try
        {
            await _context.JobVerdicts.AddAsync(verdict, cancellationToken)
                .ConfigureAwait(false);

            if (findingEntities.Count > 0)
            {
                await _context.Findings.AddRangeAsync(findingEntities, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _context.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Persisted JobVerdict {VerdictId} and {FindingCount} Finding(s) for Job {JobId}.",
                verdict.Id,
                findingEntities.Count,
                jobId);

            return Result<JobVerdict>.WithSuccess(verdict);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<JobVerdict>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist JobVerdict and Findings for Job {JobId}.",
                jobId);
            return Result<JobVerdict>.WithFailure(
                $"Database error while persisting verdict and findings: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Maps each <see cref="RuleFinding"/> (rich in-process value object) to a
    /// <see cref="Finding"/> persistence entity.
    /// </summary>
    private static List<Finding> MapFindings(
        Guid jobId,
        IReadOnlyList<RuleFinding> rulefindings,
        string engineVersion)
    {
        var entities = new List<Finding>(rulefindings.Count);
        foreach (var rf in rulefindings)
        {
            entities.Add(new Finding(
                id: Guid.NewGuid(),
                verificationJobId: jobId,
                checkId: rf.CheckId,
                verdict: rf.Verdict,
                engineVersion: engineVersion,
                expected: rf.Expected,
                observed: rf.Observed));
        }

        return entities;
    }
}

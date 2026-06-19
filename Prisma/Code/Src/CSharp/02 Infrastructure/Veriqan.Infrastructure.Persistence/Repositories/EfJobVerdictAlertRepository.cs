using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IJobVerdictAlertRepository"/> (Story E2-S15).
/// Provides the narrow read+flag-update interface used by <c>VecAlertService</c> to
/// enforce exactly-once alert dispatch.
/// </summary>
internal sealed class EfJobVerdictAlertRepository : IJobVerdictAlertRepository
{
    private readonly VeriqanDbContext _context;
    private readonly ILogger<EfJobVerdictAlertRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfJobVerdictAlertRepository"/>.
    /// </summary>
    /// <param name="context">The Veriqan EF Core database context.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public EfJobVerdictAlertRepository(
        VeriqanDbContext context,
        ILogger<EfJobVerdictAlertRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<JobVerdict?>> FindByIdAsync(
        Guid verdictId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<JobVerdict?>();

        try
        {
            var verdict = await _context.JobVerdicts
                .FirstOrDefaultAsync(v => v.Id == verdictId, cancellationToken)
                .ConfigureAwait(false);

            return Result<JobVerdict?>.WithSuccess(verdict);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<JobVerdict?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load JobVerdict {VerdictId} for alert dedup check.",
                verdictId);
            return Result<JobVerdict?>.WithFailure(
                $"Database error loading verdict {verdictId}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SaveAlertSentAsync(
        JobVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        try
        {
            // The entity was loaded by this context instance (via FindByIdAsync), so EF
            // already tracks it.  SaveChanges will detect the AlertSentAt change and issue
            // an UPDATE with the concurrency-token WHERE clause (original AlertSentAt IS NULL).
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AlertSentAt flag persisted for JobVerdict {VerdictId} at {AlertSentAt:O}.",
                verdict.Id,
                verdict.AlertSentAt);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another concurrent caller already wrote AlertSentAt — treat as success (no-op).
            _logger.LogInformation(
                "Concurrency conflict writing AlertSentAt for JobVerdict {VerdictId} — another caller already set the flag; treating as duplicate (success).",
                verdict.Id);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist AlertSentAt flag for JobVerdict {VerdictId}.",
                verdict.Id);
            return Result.WithFailure(
                $"Database error persisting AlertSentAt for verdict {verdict.Id}: {ex.Message}");
        }
    }
}

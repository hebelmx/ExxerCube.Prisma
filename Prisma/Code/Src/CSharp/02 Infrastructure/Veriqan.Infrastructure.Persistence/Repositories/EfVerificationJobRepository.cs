using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IVerificationJobRepository"/>.
/// All persistence is funnelled through <see cref="VeriqanDbContext"/>.
/// </summary>
internal sealed class EfVerificationJobRepository : IVerificationJobRepository
{
    private readonly VeriqanDbContext _context;
    private readonly ILogger<EfVerificationJobRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfVerificationJobRepository"/>.
    /// </summary>
    /// <param name="context">The Veriqan EF Core database context.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public EfVerificationJobRepository(
        VeriqanDbContext context,
        ILogger<EfVerificationJobRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<VerificationJob?>> FindByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationJob?>();

        try
        {
            var job = await _context.VerificationJobs
                .FirstOrDefaultAsync(j => j.ContentHash == contentHash, cancellationToken)
                .ConfigureAwait(false);

            // Return a success with null when not found — callers distinguish via IsSuccessMayBeNull.
            return Result<VerificationJob?>.WithSuccess(job);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<VerificationJob?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query VerificationJob by content hash {ContentHash}.", contentHash);
            return Result<VerificationJob?>.WithFailure($"Database error while looking up job by hash: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<VerificationJob>> AddAsync(
        VerificationJob job,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationJob>();

        ArgumentNullException.ThrowIfNull(job);

        try
        {
            await _context.VerificationJobs.AddAsync(job, cancellationToken)
                .ConfigureAwait(false);

            await _context.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<VerificationJob>.WithSuccess(job);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<VerificationJob>();
        }
        catch (DbUpdateException dbEx)
        {
            // SaveChangesAsync threw — the failed entity is still tracked as Added in the
            // ChangeTracker.  Clear it first so the identity map cannot shadow the committed
            // winner row when we re-query.
            _context.ChangeTracker.Clear();

            _logger.LogWarning(
                dbEx,
                "DbUpdateException on insert for job {JobId} — re-querying by ContentHash to distinguish duplicate from other failure.",
                job.Id);

            try
            {
                // AsNoTracking ensures we read the committed row from the store, not a
                // cache entry from the (now-cleared) identity map.
                var existing = await _context.VerificationJobs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ContentHash == job.ContentHash, cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null)
                {
                    // A concurrent caller already committed this ContentHash — idempotent success.
                    return Result<VerificationJob>.WithSuccess(existing);
                }

                // No matching row: this was NOT a duplicate-key conflict (e.g. FK violation,
                // deadlock, column-length overflow).  Surface the real failure honestly.
                _logger.LogError(
                    dbEx,
                    "Persist failed for job {JobId} — not a duplicate-key conflict (no row found for hash {ContentHash}).",
                    job.Id,
                    job.ContentHash);
                return Result<VerificationJob>.WithFailure(
                    $"Persist failed: {dbEx.GetBaseException().Message}");
            }
            catch (OperationCanceledException)
            {
                return ResultExtensions.Cancelled<VerificationJob>();
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Failed to re-query after DbUpdateException for hash {ContentHash}.", job.ContentHash);
                return Result<VerificationJob>.WithFailure(
                    $"Database error during conflict re-query: {retryEx.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist VerificationJob {JobId}.", job.Id);
            return Result<VerificationJob>.WithFailure($"Database error while persisting verification job: {ex.Message}");
        }
    }
}

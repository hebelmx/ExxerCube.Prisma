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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist VerificationJob {JobId}.", job.Id);
            return Result<VerificationJob>.WithFailure($"Database error while persisting verification job: {ex.Message}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
/// EF Core implementation of <see cref="IDispositionRepository"/>.
/// Provides append-only access to the <c>veriqan.Dispositions</c> table.
/// </summary>
/// <remarks>
/// Only <see cref="AppendAsync"/> (insert) and <see cref="GetForJobAsync"/> (read) are exposed.
/// No update or delete operations are ever performed on the <c>Dispositions</c> table.
/// </remarks>
internal sealed class EfDispositionRepository : IDispositionRepository
{
    private readonly VeriqanDbContext _context;
    private readonly ILogger<EfDispositionRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfDispositionRepository"/>.
    /// </summary>
    /// <param name="context">The Veriqan EF Core database context.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    public EfDispositionRepository(
        VeriqanDbContext context,
        ILogger<EfDispositionRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<Disposition>> AppendAsync(
        Disposition disposition,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<Disposition>();

        ArgumentNullException.ThrowIfNull(disposition);

        try
        {
            await _context.Dispositions.AddAsync(disposition, cancellationToken)
                .ConfigureAwait(false);

            await _context.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<Disposition>.WithSuccess(disposition);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<Disposition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to append Disposition {DispositionId} for Job {JobId}.",
                disposition.Id, disposition.VerificationJobId);
            return Result<Disposition>.WithFailure(
                $"Database error while appending disposition: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Disposition>>> GetForJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyList<Disposition>>();

        try
        {
            IQueryable<Disposition> query = _context.Dispositions;
            query = query.Where(d => d.VerificationJobId == jobId);
            query = query.OrderBy(d => d.DispositionedAtUtc);

            var rows = await query.ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<IReadOnlyList<Disposition>>.WithSuccess(rows);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<Disposition>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to query Dispositions for Job {JobId}.",
                jobId);
            return Result<IReadOnlyList<Disposition>>.WithFailure(
                $"Database error while querying dispositions: {ex.Message}");
        }
    }
}

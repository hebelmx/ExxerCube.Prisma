using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Repositories;

/// <summary>
/// EF Core SQL Server–backed implementation of <see cref="IBatchExceptionLogRepository"/>
/// (VERIQAN-E3-S3). Registered as a singleton; all database access is performed through a
/// short-lived scope created via <see cref="IServiceScopeFactory"/> to avoid consuming the
/// scoped <see cref="VeriqanDbContext"/> from a singleton.
/// </summary>
internal sealed class EfBatchExceptionLogRepository : IBatchExceptionLogRepository
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfBatchExceptionLogRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfBatchExceptionLogRepository"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create short-lived DI scopes for DB access.</param>
    /// <param name="logger">Structured logger.</param>
    public EfBatchExceptionLogRepository(
        IServiceScopeFactory scopeFactory,
        ILogger<EfBatchExceptionLogRepository> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<BatchExceptionLogEntry>> AppendAsync(
        BatchExceptionLogEntry entry,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<BatchExceptionLogEntry>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var entity = new BatchExceptionLogEntity
            {
                Id = entry.Id,
                BatchId = entry.BatchId,
                StatementHash = entry.StatementHash,
                InstitutionId = entry.InstitutionId,
                FailureReason = entry.FailureReason,
                FailedAt = entry.FailedAt,
                RetryCount = entry.RetryCount,
                LastRetryAt = entry.LastRetryAt,
                ResolvedAt = entry.ResolvedAt,
            };

            await ctx.BatchExceptionLog.AddAsync(entity, ct).ConfigureAwait(false);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

            return Result<BatchExceptionLogEntry>.WithSuccess(entry);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<BatchExceptionLogEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append batch exception log entry {EntryId}.", entry.Id);
            return Result<BatchExceptionLogEntry>.WithFailure(
                $"Database error appending batch exception log entry: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<BatchExceptionLogEntry>>> GetByBatchIdAsync(
        Guid batchId,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyList<BatchExceptionLogEntry>>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var entities = await ctx.BatchExceptionLog
                .Where(e => e.BatchId == batchId)
                .OrderBy(e => e.FailedAt)
                .AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            IReadOnlyList<BatchExceptionLogEntry> result = entities
                .Select(e => new BatchExceptionLogEntry(
                    Id: e.Id,
                    BatchId: e.BatchId,
                    StatementHash: e.StatementHash,
                    InstitutionId: e.InstitutionId,
                    FailureReason: e.FailureReason,
                    FailedAt: e.FailedAt,
                    RetryCount: e.RetryCount,
                    LastRetryAt: e.LastRetryAt,
                    ResolvedAt: e.ResolvedAt))
                .ToList();

            return Result<IReadOnlyList<BatchExceptionLogEntry>>.WithSuccess(result);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<BatchExceptionLogEntry>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get batch exception log for batch {BatchId}.", batchId);
            return Result<IReadOnlyList<BatchExceptionLogEntry>>.WithFailure(
                $"Database error getting batch exception log: {ex.Message}");
        }
    }
}

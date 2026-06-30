using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Repositories;

/// <summary>
/// EF Core SQL Server–backed implementation of <see cref="IReprocessAuditRepository"/>.
/// Registered as a singleton; all database access is performed through a short-lived scope
/// created via <see cref="IServiceScopeFactory"/> to avoid consuming the scoped
/// <see cref="VeriqanDbContext"/> from a singleton.
/// </summary>
internal sealed class EfReprocessAuditRepository : IReprocessAuditRepository
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfReprocessAuditRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfReprocessAuditRepository"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create short-lived DI scopes for DB access.</param>
    /// <param name="logger">Structured logger.</param>
    public EfReprocessAuditRepository(
        IServiceScopeFactory scopeFactory,
        ILogger<EfReprocessAuditRepository> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<ReprocessAuditEntry>> AppendAsync(
        ReprocessAuditEntry entry,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<ReprocessAuditEntry>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var entity = new ReprocessAuditLogEntity
            {
                Id = entry.Id,
                ContentHash = entry.ContentHash,
                Actor = entry.Actor,
                Reason = entry.Reason,
                BeforeSignal = entry.BeforeSignal.HasValue ? (int)entry.BeforeSignal.Value : (int?)null,
                AfterSignal = (int)entry.AfterSignal,
                ReprocessedAtUtc = entry.ReprocessedAtUtc,
            };

            await ctx.ReprocessAuditLog.AddAsync(entity, ct).ConfigureAwait(false);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

            return Result<ReprocessAuditEntry>.WithSuccess(entry);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<ReprocessAuditEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append reprocess audit entry {EntryId}.", entry.Id);
            return Result<ReprocessAuditEntry>.WithFailure(
                $"Database error appending audit entry: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ReprocessAuditEntry>>> GetForContentHashAsync(
        string contentHash,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyList<ReprocessAuditEntry>>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var entities = await ctx.ReprocessAuditLog
                .Where(e => e.ContentHash == contentHash)
                .OrderBy(e => e.ReprocessedAtUtc)
                .AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            IReadOnlyList<ReprocessAuditEntry> result = entities
                .Select(e => new ReprocessAuditEntry(
                    Id: e.Id,
                    ContentHash: e.ContentHash,
                    Actor: e.Actor,
                    Reason: e.Reason,
                    BeforeSignal: e.BeforeSignal.HasValue
                        ? (VerdictSignal)e.BeforeSignal.Value
                        : (VerdictSignal?)null,
                    AfterSignal: (VerdictSignal)e.AfterSignal,
                    ReprocessedAtUtc: e.ReprocessedAtUtc))
                .ToList();

            return Result<IReadOnlyList<ReprocessAuditEntry>>.WithSuccess(result);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<IReadOnlyList<ReprocessAuditEntry>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get reprocess audit for hash {ContentHash}.", contentHash);
            return Result<IReadOnlyList<ReprocessAuditEntry>>.WithFailure(
                $"Database error getting audit log: {ex.Message}");
        }
    }
}

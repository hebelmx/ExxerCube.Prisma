using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using ExxerCube.Prisma.Veriqan.Orchestration.Serialization;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Stores;

/// <summary>
/// EF Core SQL Server–backed implementation of <see cref="IVerificationResultStore"/>.
/// Registered as a singleton; all database access is performed through a short-lived scope
/// created via <see cref="IServiceScopeFactory"/> to avoid consuming the scoped
/// <see cref="VeriqanDbContext"/> from a singleton.
/// </summary>
internal sealed class EfVerificationResultStore : IVerificationResultStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfVerificationResultStore> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EfVerificationResultStore"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create short-lived DI scopes for DB access.</param>
    /// <param name="logger">Structured logger.</param>
    public EfVerificationResultStore(
        IServiceScopeFactory scopeFactory,
        ILogger<EfVerificationResultStore> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // PascalCase — matches serialised property names of domain types
        };
        opts.Converters.Add(new TimeSpanTicksJsonConverter());
        opts.Converters.Add(new VerdictSummaryJsonConverter());
        return opts;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> IsCompletedAsync(string contentHash, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<bool>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var exists = await ctx.OutcomeSnapshots
                .AnyAsync(e => e.ContentHash == contentHash, ct)
                .ConfigureAwait(false);

            return Result<bool>.WithSuccess(exists);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<bool>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check completion for hash {ContentHash}.", contentHash);
            return Result<bool>.WithFailure($"Database error checking completion: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> SaveOutcomeAsync(
        string contentHash,
        VerificationOutcome outcome,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var entity = new VerificationOutcomeSnapshotEntity
            {
                ContentHash = contentHash,
                OutcomeJson = JsonSerializer.Serialize(outcome, JsonOptions),
                SavedAtUtc = DateTimeOffset.UtcNow,
            };

            await ctx.OutcomeSnapshots.AddAsync(entity, ct).ConfigureAwait(false);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled();
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
        {
            // Unique-key violation: another caller already saved this hash — silent no-op (first-write-wins).
            _logger.LogDebug(
                "SaveOutcomeAsync no-op: hash {ContentHash} already stored (concurrent insert).",
                contentHash);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save outcome for hash {ContentHash}.", contentHash);
            return Result.WithFailure($"Database error saving outcome: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result> ReplaceOutcomeAsync(
        string contentHash,
        VerificationOutcome outcome,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var json = JsonSerializer.Serialize(outcome, JsonOptions);
            var existing = await ctx.OutcomeSnapshots
                .FindAsync(new object?[] { contentHash }, ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                // First-writer path: insert a new snapshot.
                ctx.OutcomeSnapshots.Add(new VerificationOutcomeSnapshotEntity
                {
                    ContentHash = contentHash,
                    OutcomeJson = json,
                    SavedAtUtc = DateTimeOffset.UtcNow,
                });

                try
                {
                    await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                catch (DbUpdateException dbEx) when (dbEx.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
                {
                    // Concurrent-insert race: two ReplaceOutcomeAsync calls both found no existing
                    // row, staged an Add, and the second SaveChanges hit a PK violation (error 2627).
                    // Retry once as an update: the row now exists — re-find it and overwrite.
                    // If the retry also fails, the exception propagates to the outer handler.
                    _logger.LogDebug(
                        "ReplaceOutcomeAsync concurrent-insert race for hash {ContentHash}; retrying as update.",
                        contentHash);
                    ctx.ChangeTracker.Clear();
                    var raceWinner = await ctx.OutcomeSnapshots
                        .FindAsync(new object?[] { contentHash }, ct)
                        .ConfigureAwait(false);
                    if (raceWinner is not null)
                    {
                        raceWinner.OutcomeJson = json;
                        raceWinner.ReplacedAtUtc = DateTimeOffset.UtcNow;
                        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                existing.OutcomeJson = json;
                existing.ReplacedAtUtc = DateTimeOffset.UtcNow;
                await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replace outcome for hash {ContentHash}.", contentHash);
            return Result.WithFailure($"Database error replacing outcome: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<VerificationOutcome?>> GetOutcomeAsync(
        string contentHash,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VerificationOutcome?>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();

            var entity = await ctx.OutcomeSnapshots
                .FindAsync(new object?[] { contentHash }, ct)
                .ConfigureAwait(false);

            if (entity is null)
                return Result<VerificationOutcome?>.WithSuccess(null);

            var restored = JsonSerializer.Deserialize<VerificationOutcome>(entity.OutcomeJson, JsonOptions);
            return Result<VerificationOutcome?>.WithSuccess(restored);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled<VerificationOutcome?>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outcome for hash {ContentHash}.", contentHash);
            return Result<VerificationOutcome?>.WithFailure($"Database error getting outcome: {ex.Message}");
        }
    }
}

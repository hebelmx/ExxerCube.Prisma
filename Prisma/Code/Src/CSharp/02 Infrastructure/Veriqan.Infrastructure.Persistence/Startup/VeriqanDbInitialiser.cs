using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Startup;

/// <summary>
/// Provides the startup initialisation logic for the Veriqan legal-baseline SQL store.
/// Intended to be called once by the host's startup service (<c>VeriqanLegalBaselineStartupService</c>).
/// </summary>
/// <remarks>
/// Separated from the hosted service so the Orchestration layer (which hosts the
/// <c>IHostedService</c> registration) does not need to reference EF Core directly.
/// </remarks>
public static class VeriqanDbInitialiser
{
    /// <summary>
    /// Optionally applies EF Core migrations and seeds the legal-baseline tolerance table,
    /// then always warms the <see cref="SqlLegalToleranceProvider"/> in-memory cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Cache warming is unconditional</b> — Step 3 (<c>provider.InitialiseAsync</c>) runs
    /// regardless of <paramref name="applyMigrationsAndSeed"/>. The
    /// <see cref="SqlLegalToleranceProvider.For"/> method throws
    /// <see cref="InvalidOperationException"/> on any cold-cache access, so warming is
    /// required for the host to serve any verification request.
    /// </para>
    /// <para>
    /// <b>Migrate+seed gate:</b> when <paramref name="applyMigrationsAndSeed"/> is
    /// <c>false</c>, Steps 1 and 2 are skipped. The caller (typically CI) is responsible
    /// for having applied migrations via the <c>--migrate</c> CLI command before starting
    /// the host. The fail-loud contract still applies: if the DB was not pre-migrated and
    /// the tolerance table is empty, Step 3 throws <see cref="InvalidOperationException"/>,
    /// aborting host startup.
    /// </para>
    /// <para>
    /// <b>Fail-loud contract:</b> throws on any failure — DB unreachable, migration failure,
    /// or empty cache after (optional) seeding. Never swallows and never falls back silently.
    /// </para>
    /// </remarks>
    /// <param name="scopeFactory">
    /// Factory used to create short-lived scopes for EF Core operations.
    /// </param>
    /// <param name="provider">
    /// The singleton <see cref="SqlLegalToleranceProvider"/> to warm after seeding.
    /// </param>
    /// <param name="applyMigrationsAndSeed">
    /// <c>true</c> (dev / standalone default): run EF Core <c>MigrateAsync</c> and
    /// <see cref="LegalBaselineSeeder.SeedAsync"/> before warming the cache.
    /// <c>false</c> (CI / 12-factor): skip DDL — migrations were applied separately via
    /// <c>--migrate</c>; only warm the cache from the already-seeded store.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tolerance cache is empty after (optional) seeding — the DB was not
    /// pre-populated or the migration was not applied before the host started.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Propagated when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task InitialiseAsync(
        IServiceScopeFactory scopeFactory,
        SqlLegalToleranceProvider provider,
        bool applyMigrationsAndSeed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(provider);

        if (applyMigrationsAndSeed)
        {
            // Step 1 + 2: migrate and seed inside a short-lived scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await LegalBaselineSeeder.SeedAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // Step 3: warm the in-memory cache from the (already-seeded) store.
        // This step is UNCONDITIONAL — skipping it leaves SqlLegalToleranceProvider.For() broken.
        // InitialiseAsync creates its own scope internally.
        await provider.InitialiseAsync(cancellationToken).ConfigureAwait(false);
    }
}

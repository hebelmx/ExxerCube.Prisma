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
    /// Applies EF Core migrations, seeds the legal-baseline tolerance table idempotently,
    /// and warms the <see cref="SqlLegalToleranceProvider"/> in-memory cache.
    /// </summary>
    /// <remarks>
    /// <b>Fail-loud contract:</b> throws on any failure — DB unreachable, migration failure,
    /// or empty cache after seeding. Never swallows and never falls back silently.
    /// </remarks>
    /// <param name="scopeFactory">
    /// Factory used to create short-lived scopes for EF Core operations.
    /// </param>
    /// <param name="provider">
    /// The singleton <see cref="SqlLegalToleranceProvider"/> to warm after seeding.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tolerance cache is empty after seeding.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Propagated when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task InitialiseAsync(
        IServiceScopeFactory scopeFactory,
        SqlLegalToleranceProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(provider);

        // Step 1 + 2: migrate and seed inside a short-lived scope.
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await LegalBaselineSeeder.SeedAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // Step 3: warm the in-memory cache from the now-seeded store.
        // InitialiseAsync creates its own scope internally.
        await provider.InitialiseAsync(cancellationToken).ConfigureAwait(false);
    }
}

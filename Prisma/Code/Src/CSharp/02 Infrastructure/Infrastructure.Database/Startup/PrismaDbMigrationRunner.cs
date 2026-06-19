using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Database.Startup;

/// <summary>
/// Runs EF Core migrations for registered Prisma DbContexts and exits.
/// Designed for the <c>--migrate-only</c> K8s init-container pattern: build the host,
/// call <see cref="RunMigrationsAsync"/>, then return its exit code without starting
/// the host's normal run loop.
/// </summary>
/// <remarks>
/// Migration order (FK/schema dependency):
/// <list type="number">
///   <item><see cref="PrismaDbContext"/> — core Prisma schema (master data + audit)</item>
/// </list>
/// Additional contexts (<c>ApplicationDbContext</c>, <c>TemplateDbContext</c>) are owned by
/// the Web.UI and are not migrated here.  A worker that registers none of the above contexts
/// receives exit code 0 with an informational log (no-op is valid: worker without DB is allowed
/// in dev/test where the connection string is blank or a DEV-PLACEHOLDER).
/// </remarks>
public static class PrismaDbMigrationRunner
{
    /// <summary>
    /// Applies pending EF Core migrations for every Prisma DbContext registered in
    /// <paramref name="host"/>'s DI container.
    /// </summary>
    /// <param name="host">The built (but not yet started) host.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// <c>0</c> if all applicable migrations succeeded (including the no-context case);
    /// <c>1</c> if any migration failed.
    /// </returns>
    public static async Task<int> RunMigrationsAsync(
        IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var logger = host.Services.GetRequiredService<ILogger<PrismaDbMigrationRunnerMarker>>();

        // ── Stage 1: PrismaDbContext ────────────────────────────────────────────────
        var migrated = await TryMigrateAsync<PrismaDbContext>(host, logger, cancellationToken)
            .ConfigureAwait(false);
        if (!migrated)
        {
            return 1;
        }

        logger.LogInformation("Migration runner: all applicable migrations applied successfully.");
        return 0;
    }

    // ── private helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves <typeparamref name="TContext"/> from a short-lived DI scope and calls
    /// <c>MigrateAsync</c>.  If the context is not registered the
    /// method logs an information message and returns <c>true</c> (not-registered is
    /// intentional — a worker without DB is valid in dev/test).
    /// </summary>
    /// <returns><c>true</c> on success or context-not-registered; <c>false</c> on failure.</returns>
    private static async Task<bool> TryMigrateAsync<TContext>(
        IHost host,
        ILogger logger,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var contextName = typeof(TContext).Name;

        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetService<TContext>();

        if (context is null)
        {
            logger.LogInformation(
                "Migration runner: {DbContext} is not registered in this host — skipping (no-op is valid in dev/test).",
                contextName);
            return true;
        }

        try
        {
            logger.LogInformation("Migration runner: applying pending migrations for {DbContext}…", contextName);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Migration runner: {DbContext} — migrations applied.", contextName);
            return true;
        }
        catch (OperationCanceledException)
        {
            logger.LogError(
                "Migration runner: {DbContext} migration cancelled.",
                contextName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Migration runner: {DbContext} migration FAILED — {ErrorMessage}",
                contextName,
                ex.Message);
            return false;
        }
    }
}

/// <summary>
/// Marker type used solely to obtain a typed <see cref="ILogger{T}"/> for
/// <see cref="PrismaDbMigrationRunner"/> (top-level static classes cannot be generic
/// parameters for <c>ILogger&lt;T&gt;</c>).
/// </summary>
internal sealed class PrismaDbMigrationRunnerMarker { }

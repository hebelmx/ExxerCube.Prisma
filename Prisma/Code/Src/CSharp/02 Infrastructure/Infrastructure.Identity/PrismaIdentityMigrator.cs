namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// Startup helper that applies pending EF Core migrations for <see cref="PrismaIdentityDbContext"/>.
/// </summary>
/// <remarks>
/// Called from <c>Program.cs</c> via the extension method
/// <see cref="PrismaIdentityExtensions.MigratePrismaIdentityAsync"/> after
/// <c>app.Build()</c> and before <c>app.Run()</c>.
///
/// Fail-open: on failure the error is logged but not re-thrown, matching the
/// template-seeder pattern. The application still boots; it may be partially functional
/// if the schema is already up to date.
/// </remarks>
public static class PrismaIdentityMigrator
{
    /// <summary>
    /// Applies any pending migrations for <see cref="PrismaIdentityDbContext"/>.
    /// </summary>
    /// <param name="serviceProvider">Root service provider (post-build).</param>
    /// <param name="logger">Logger for migration status messages.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task MigrateAsync(
        IServiceProvider serviceProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PrismaIdentityDbContext>>();

            await using var context = await dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Applying PrismaIdentity EF Core migrations...");
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("PrismaIdentity migrations applied successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("PrismaIdentity migration was cancelled");
        }
        catch (Exception ex)
        {
            // Fail-open: log but do not rethrow — app boots even when DB is temporarily unreachable.
            logger.LogError(ex,
                "PrismaIdentity migration failed. The application will continue but identity " +
                "features may be unavailable until the database is reachable and migrations applied.");
        }
    }
}

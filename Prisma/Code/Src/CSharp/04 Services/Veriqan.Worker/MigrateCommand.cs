using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Startup;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ExxerCube.Prisma.Veriqan.Worker;

/// <summary>
/// Migrate-only CLI entrypoint for Veriqan EF Core migrations.
/// Program.cs delegates here when the process is invoked with <c>--migrate</c>.
/// Applies EF Core migrations, seeds the legal-baseline tolerance table, and warms
/// <see cref="SqlLegalToleranceProvider"/> — then exits without starting the web host.
/// </summary>
/// <remarks>
/// A test-friendly overload accepts an explicit connection string and
/// <see cref="IConfiguration"/> so integration tests can exercise the same code path
/// against a Testcontainers database without standing up the full worker host.
/// </remarks>
public static class MigrateCommand
{
    /// <summary>The CLI flag that activates migrate-only mode.</summary>
    public const string Flag = "--migrate";

    /// <summary>
    /// Reads the connection string from configuration then runs the migration path.
    /// Called from <c>Program.cs</c> when <paramref name="args"/> contains
    /// <see cref="Flag"/>.
    /// </summary>
    /// <param name="args">Command-line arguments (must contain <c>--migrate</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 0 on success; 1 on cancellation or unexpected failure;
    /// 2 when <c>ConnectionStrings:VeriqanDb</c> is absent.
    /// </returns>
    public static async Task<int> RunMigrateAsync(
        string[] args,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Build configuration from the same sources the worker host would use.
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
                optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("VeriqanDb");
        if (string.IsNullOrWhiteSpace(cs))
        {
            Log.Error(
                "Migration failed: {ConfigKey} is not configured. " +
                "Provide the connection string via the environment variable " +
                "ConnectionStrings__VeriqanDb or in appsettings.json.",
                "ConnectionStrings:VeriqanDb");
            return 2;
        }

        return await RunMigrateAsync(cs, config, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Test-friendly overload: accepts an explicit connection string and configuration.
    /// Applies EF Core migrations, seeds the legal-baseline tolerance table, and warms
    /// <see cref="SqlLegalToleranceProvider"/>.
    /// </summary>
    /// <param name="connectionString">
    /// SQL Server connection string for the Veriqan database.
    /// </param>
    /// <param name="configuration">
    /// Application configuration. Must include
    /// <c>Veriqan:LegalBaseline:EncryptionKey</c> (Base64-encoded 32-byte AES-256 key)
    /// so the encrypted legal-baseline store can be seeded.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>0 on success; 1 on cancellation or unexpected failure.</returns>
    public static async Task<int> RunMigrateAsync(
        string connectionString,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(configuration);

        if (ct.IsCancellationRequested)
        {
            Log.Warning("Migration skipped — cancellation was requested before it started.");
            return 1;
        }

        Log.Information(
            "Veriqan migrate mode: applying EF Core migrations, seeding legal baseline, " +
            "and warming SqlLegalToleranceProvider cache.");

        // Build a minimal DI container with only the persistence layer.
        // This is intentionally lightweight — no pipeline, no rules, no HTTP host.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(lb => lb.AddSerilog(dispose: false));
        services.AddVeriqanPersistence(connectionString);

        await using var sp = services.BuildServiceProvider();

        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var provider = sp.GetRequiredService<SqlLegalToleranceProvider>();

        try
        {
            // applyMigrationsAndSeed=true: this IS the migration command — always run DDL.
            await VeriqanDbInitialiser.InitialiseAsync(
                    scopeFactory,
                    provider,
                    applyMigrationsAndSeed: true,
                    cancellationToken: ct)
                .ConfigureAwait(false);

            Log.Information(
                "Migration completed successfully — EF migrations applied, " +
                "baseline seeded, cache warmed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Migration was cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Migration failed with an unexpected error.");
            return 1;
        }
    }
}

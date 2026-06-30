using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Startup;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Startup;

/// <summary>
/// <see cref="IHostedService"/> that runs once at host startup to optionally apply EF Core
/// migrations and seed the legal-baseline tolerance table, then always warms the
/// <see cref="SqlLegalToleranceProvider"/> in-memory cache.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cache warming is unconditional.</b> This service is always registered when a SQL
/// connection string is present (see <c>AddVeriqan</c>). It must warm
/// <see cref="SqlLegalToleranceProvider"/> so that <see cref="SqlLegalToleranceProvider.For"/>
/// does not throw <see cref="InvalidOperationException"/> on the first verification request.
/// </para>
/// <para>
/// <b>Migrate+seed gate:</b> the DDL steps (EF Core <c>MigrateAsync</c> + seed) run only
/// when <c>Veriqan:RunMigrationsAtStartup</c> is <c>true</c> (or absent — default <c>true</c>
/// preserves backward compatibility). CI sets the flag to <c>false</c> and uses
/// <c>--migrate</c> to apply migrations separately before starting the host.
/// </para>
/// <para>
/// <b>Fail-loud contract:</b> if the database is unreachable, the seed fails, or the
/// tolerance cache is empty after the (optional) seed step, this service throws. The host
/// treats an unhandled exception from <c>StartAsync</c> as a fatal startup failure — never
/// silently fall back to in-code defaults.
/// </para>
/// <para>
/// Registered by <c>AddVeriqan</c> in <c>VeriqanOrchestrationExtensions</c> only when a SQL
/// connection string (<c>"VeriqanDb"</c>) is present. When using
/// <c>AddVeriqanInMemoryPersistence</c> this service is NOT registered — the in-code
/// <c>DefaultLegalToleranceProvider</c> remains active and no hosted service runs.
/// </para>
/// </remarks>
public sealed class VeriqanLegalBaselineStartupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqlLegalToleranceProvider _provider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VeriqanLegalBaselineStartupService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VeriqanLegalBaselineStartupService"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to resolve scoped EF Core services.</param>
    /// <param name="provider">The singleton <see cref="SqlLegalToleranceProvider"/> to warm up.</param>
    /// <param name="configuration">
    /// Application configuration — used to read <c>Veriqan:RunMigrationsAtStartup</c>.
    /// </param>
    /// <param name="logger">Structured logger.</param>
    public VeriqanLegalBaselineStartupService(
        IServiceScopeFactory scopeFactory,
        SqlLegalToleranceProvider provider,
        IConfiguration configuration,
        ILogger<VeriqanLegalBaselineStartupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Default true: dev / standalone deployments migrate+seed on every boot (backward compat).
        // Set false in CI / 12-factor deployments that run `--migrate` as a pre-step.
        var applyMigrationsAndSeed = _configuration.GetValue<bool?>("Veriqan:RunMigrationsAtStartup") ?? true;

        _logger.LogInformation(
            "{Service} starting — applyMigrationsAndSeed={Apply}, warming SQL provider.",
            nameof(VeriqanLegalBaselineStartupService),
            applyMigrationsAndSeed);

        // Delegate to the Persistence-layer helper that owns EF Core references.
        // Any failure propagates as an exception, aborting host startup (fail-loud contract).
        await VeriqanDbInitialiser.InitialiseAsync(
                _scopeFactory,
                _provider,
                applyMigrationsAndSeed,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "{Service} startup complete — encrypted SQL legal baseline is live " +
            "(migrations applied: {Applied}).",
            nameof(VeriqanLegalBaselineStartupService),
            applyMigrationsAndSeed);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

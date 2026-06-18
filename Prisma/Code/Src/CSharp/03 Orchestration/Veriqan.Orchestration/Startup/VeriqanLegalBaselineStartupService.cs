using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Startup;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Startup;

/// <summary>
/// <see cref="IHostedService"/> that runs once at host startup to apply EF Core migrations,
/// seed the legal-baseline tolerance table, and initialise <see cref="SqlLegalToleranceProvider"/>
/// from the encrypted SQL store.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-loud contract:</b> if the database is unreachable, the seed fails, or the
/// tolerance cache remains empty after seeding, this service throws. The host treats an
/// unhandled exception from <c>StartAsync</c> as a fatal startup failure, which is the
/// correct behaviour — never silently fall back to in-code defaults because that defeats
/// the security guarantee of the encrypted SQL store.
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
    private readonly ILogger<VeriqanLegalBaselineStartupService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VeriqanLegalBaselineStartupService"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to resolve scoped EF Core services.</param>
    /// <param name="provider">The singleton <see cref="SqlLegalToleranceProvider"/> to warm up.</param>
    /// <param name="logger">Structured logger.</param>
    public VeriqanLegalBaselineStartupService(
        IServiceScopeFactory scopeFactory,
        SqlLegalToleranceProvider provider,
        ILogger<VeriqanLegalBaselineStartupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Service} starting — applying migrations, seeding legal baseline, initialising SQL provider.",
            nameof(VeriqanLegalBaselineStartupService));

        // Delegate to the Persistence-layer helper that owns EF Core references.
        // Any failure propagates as an exception, aborting host startup (fail-loud contract).
        await VeriqanDbInitialiser.InitialiseAsync(_scopeFactory, _provider, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "{Service} startup complete — encrypted SQL legal baseline is live.",
            nameof(VeriqanLegalBaselineStartupService));
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

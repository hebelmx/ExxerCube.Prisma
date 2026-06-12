using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.Worker;

/// <summary>
/// Thin host wrapper for the Orion SIARA watch loop; exposes the hosted-service lifecycle.
/// </summary>
/// <remarks>
/// The worker is a singleton <see cref="BackgroundService"/>. It simply drives the singleton
/// <see cref="SiaraWatchLoop"/>, which owns the DI scopes: one long-lived discovery scope (to keep the
/// SIARA session warm) and a fresh scope per discovered document (so each pull rides its own scoped
/// Playwright browser and SIARA session). Keeping the scope ownership inside the loop avoids a captive
/// dependency on the singleton host.
/// </remarks>
public class OrionWorkerService : BackgroundService
{
    private readonly SiaraWatchLoop _watchLoop;
    private readonly ILogger<OrionWorkerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrionWorkerService"/> class.
    /// </summary>
    /// <param name="watchLoop">The SIARA watch loop that discovers and ingests documents.</param>
    /// <param name="logger">The logger.</param>
    public OrionWorkerService(SiaraWatchLoop watchLoop, ILogger<OrionWorkerService> logger)
    {
        _watchLoop = watchLoop;
        _logger = logger;
    }

    /// <summary>
    /// Executes the worker service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orion worker starting");
        await _watchLoop.RunAsync(stoppingToken).ConfigureAwait(false);
    }
}

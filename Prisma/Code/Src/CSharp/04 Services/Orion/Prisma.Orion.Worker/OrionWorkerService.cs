using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.Worker;

/// <summary>
/// Thin host wrapper for the Orion ingestion orchestrator; exposes the hosted-service lifecycle.
/// </summary>
/// <remarks>
/// The worker is a singleton <see cref="BackgroundService"/>, but the ingestion orchestrator and the real
/// SIARA downloader it drives are <strong>scoped</strong> (they ride a scoped Playwright browser and SIARA
/// session). The worker therefore owns the scope: it resolves the orchestrator from a DI scope per unit of
/// work rather than capturing it, which avoids a captive dependency. The 1.2 watch loop refines this to one
/// scope per discovered document.
/// </remarks>
public class OrionWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrionWorkerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrionWorkerService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The DI scope factory used to resolve the scoped orchestrator per unit of work.</param>
    /// <param name="logger">The logger.</param>
    public OrionWorkerService(IServiceScopeFactory scopeFactory, ILogger<OrionWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
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

        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
        await orchestrator.StartAsync(stoppingToken).ConfigureAwait(false);
    }
}

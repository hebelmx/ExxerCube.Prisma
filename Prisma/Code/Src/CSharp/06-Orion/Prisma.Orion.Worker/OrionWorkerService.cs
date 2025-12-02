using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.Worker;

/// <summary>
/// Thin host wrapper for Orion ingestion orchestrator; exposes hosted-service lifecycle.
/// </summary>
public class OrionWorkerService : BackgroundService
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly ILogger<OrionWorkerService> _logger;

    public OrionWorkerService(IngestionOrchestrator orchestrator, ILogger<OrionWorkerService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orion worker starting");
        await _orchestrator.StartAsync(stoppingToken).ConfigureAwait(false);
    }
}

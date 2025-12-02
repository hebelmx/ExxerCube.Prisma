using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Athena.Processing;

namespace Prisma.Athena.Worker;

/// <summary>
/// Thin host wrapper for Athena processing orchestrator; exposes hosted-service lifecycle.
/// </summary>
public class AthenaWorkerService : BackgroundService
{
    private readonly ProcessingOrchestrator _orchestrator;
    private readonly ILogger<AthenaWorkerService> _logger;

    public AthenaWorkerService(ProcessingOrchestrator orchestrator, ILogger<AthenaWorkerService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Athena worker starting");
        await _orchestrator.StartAsync(stoppingToken).ConfigureAwait(false);
    }
}

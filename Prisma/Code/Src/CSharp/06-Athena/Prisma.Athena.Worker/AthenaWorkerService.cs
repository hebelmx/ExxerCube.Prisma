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

    /// <summary>
    /// Initializes a new instance of the <see cref="AthenaWorkerService"/> class.
    /// </summary>
    /// <param name="orchestrator">The processing orchestrator.</param>
    /// <param name="logger">The logger.</param>
    public AthenaWorkerService(ProcessingOrchestrator orchestrator, ILogger<AthenaWorkerService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the worker service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Athena worker starting");
        await _orchestrator.StartAsync(stoppingToken).ConfigureAwait(false);
    }
}
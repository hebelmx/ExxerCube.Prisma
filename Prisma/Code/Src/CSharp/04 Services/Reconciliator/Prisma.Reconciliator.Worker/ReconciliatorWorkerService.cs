using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Athena.Processing.Reconciliation;

namespace Prisma.Reconciliator.Worker;

/// <summary>
/// Thin host wrapper for the <em>Reconciliator</em> actor (MVP-PATH 1.4, ADR-011); exposes hosted-service
/// lifecycle. It drives the <see cref="ReconciliationPipelineService"/> (load fused-expediente handoff →
/// Classification → Export → terminal completion event).
/// </summary>
public class ReconciliatorWorkerService : BackgroundService
{
    private readonly ReconciliationPipelineService _reconciliationPipeline;
    private readonly ILogger<ReconciliatorWorkerService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ReconciliatorWorkerService"/> class.</summary>
    /// <param name="reconciliationPipeline">The Reconciliator pipeline driver.</param>
    /// <param name="logger">The logger.</param>
    public ReconciliatorWorkerService(ReconciliationPipelineService reconciliationPipeline, ILogger<ReconciliatorWorkerService> logger)
    {
        _reconciliationPipeline = reconciliationPipeline;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reconciliator worker starting");
        await _reconciliationPipeline.StartAsync(stoppingToken).ConfigureAwait(false);
    }
}

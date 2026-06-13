using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prisma.Athena.Processing;

namespace Prisma.Athena.Worker;

/// <summary>
/// Thin host wrapper for the Athena <em>Extractor</em> actor (MVP-PATH 1.4, ADR-011); exposes hosted-service
/// lifecycle. It drives the <see cref="ExtractionPipelineService"/> (Quality → OCR → Fusion → persist handoff →
/// broadcast to the Reconciliator), not the full monolith — Classification → Export run in the separate
/// Reconciliator process.
/// </summary>
public class AthenaWorkerService : BackgroundService
{
    private readonly ExtractionPipelineService _extractionPipeline;

    private readonly ILogger<AthenaWorkerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AthenaWorkerService"/> class.
    /// </summary>
    /// <param name="extractionPipeline">The Extractor pipeline driver.</param>
    /// <param name="logger">The logger.</param>
    public AthenaWorkerService(ExtractionPipelineService extractionPipeline, ILogger<AthenaWorkerService> logger)
    {
        _extractionPipeline = extractionPipeline;
        _logger = logger;
    }

    /// <summary>
    /// Executes the worker service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Athena Extractor worker starting");
        await _extractionPipeline.StartAsync(stoppingToken).ConfigureAwait(false);
    }
}
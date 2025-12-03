using Microsoft.Extensions.Logging;
using Prisma.Athena.Processing;

namespace Prisma.Athena.HealthChecks;

/// <summary>
/// Dashboard service for Athena processing worker.
/// </summary>
/// <remarks>
/// Provides real-time metrics and statistics for monitoring:
/// - Documents processed count
/// - Last event timestamp
/// - Queue depth
/// - Heartbeat timestamp
/// </remarks>
public sealed class AthenaDashboardService : IDashboardService
{
    private readonly ProcessingOrchestrator _orchestrator;
    private readonly ILogger<AthenaDashboardService> _logger;
    private DateTime _lastHeartbeat;
    private int _documentsProcessed;
    private DateTime? _lastEventTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="AthenaDashboardService"/> class.
    /// </summary>
    /// <param name="orchestrator">Processing orchestrator.</param>
    /// <param name="logger">Logger.</param>
    public AthenaDashboardService(
        ProcessingOrchestrator orchestrator,
        ILogger<AthenaDashboardService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _lastHeartbeat = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Dashboard stats requested");

        // Update heartbeat
        _lastHeartbeat = DateTime.UtcNow;

        // TODO: Get actual metrics from orchestrator when available:
        // - _documentsProcessed = _orchestrator.DocumentsProcessedCount;
        // - _lastEventTime = _orchestrator.LastEventTime;
        // - queueDepth = _orchestrator.QueueDepth;

        var stats = new DashboardStats(
            WorkerName: "Athena Processing Worker",
            Status: "Running",
            DocumentsProcessed: _documentsProcessed,
            LastEventTime: _lastEventTime,
            LastHeartbeat: _lastHeartbeat,
            QueueDepth: 0);

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Records a document processed event.
    /// </summary>
    /// <remarks>
    /// TODO: This should be called by orchestrator via event subscription or direct call.
    /// </remarks>
    public void RecordDocumentProcessed()
    {
        _documentsProcessed++;
        _lastEventTime = DateTime.UtcNow;
    }
}

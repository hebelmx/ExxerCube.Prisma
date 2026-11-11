using Microsoft.Extensions.Logging;

namespace Prisma.Athena.HealthChecks;

/// <summary>
/// Dashboard service for Athena processing worker.
/// </summary>
/// <remarks>
/// Provides real-time metrics and statistics for monitoring:
/// - Documents processed count (incremented by the pipeline via <see cref="IDashboardService.RecordDocumentProcessed"/>)
/// - Last event timestamp
/// - Queue depth
/// - Heartbeat timestamp
/// </remarks>
public sealed class AthenaDashboardService : IDashboardService
{
    private readonly ILogger<AthenaDashboardService> _logger;
    private DateTime _lastHeartbeat;
    private int _documentsProcessed;
    private DateTime? _lastEventTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="AthenaDashboardService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public AthenaDashboardService(ILogger<AthenaDashboardService> logger)
    {
        _logger = logger;
        _lastHeartbeat = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Dashboard stats requested");

        // Update heartbeat
        _lastHeartbeat = DateTime.UtcNow;

        var stats = new DashboardStats(
            WorkerName: "Athena Processing Worker",
            Status: "Running",
            DocumentsProcessed: Volatile.Read(ref _documentsProcessed),
            LastEventTime: _lastEventTime,
            LastHeartbeat: _lastHeartbeat,
            QueueDepth: 0);

        return Task.FromResult(stats);
    }

    /// <inheritdoc/>
    public void RecordDocumentProcessed()
    {
        // Registered as a singleton and called from concurrent pipeline threads —
        // increment atomically to avoid lost updates.
        var total = Interlocked.Increment(ref _documentsProcessed);
        _lastEventTime = DateTime.UtcNow;

        _logger.LogDebug(
            "Athena dashboard: document recorded. Total processed: {DocumentsProcessed}",
            total);
    }
}

using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace Prisma.Orion.HealthChecks;

/// <summary>
/// Dashboard service for Orion ingestion worker.
/// </summary>
/// <remarks>
/// Provides real-time metrics and statistics for monitoring:
/// - Documents processed count
/// - Last event timestamp
/// - Queue depth
/// - Heartbeat timestamp
/// </remarks>
public sealed class OrionDashboardService : IDashboardService
{
    private readonly IngestionOrchestrator _orchestrator;
    private readonly ILogger<OrionDashboardService> _logger;
    private DateTime _lastHeartbeat;
    private int _documentsProcessed;
    private DateTime? _lastEventTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrionDashboardService"/> class.
    /// </summary>
    /// <param name="orchestrator">Ingestion orchestrator.</param>
    /// <param name="logger">Logger.</param>
    public OrionDashboardService(
        IngestionOrchestrator orchestrator,
        ILogger<OrionDashboardService> logger)
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
            WorkerName: "Orion Ingestion Worker",
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

    // ========================================================================
    // NEW: Railway-Oriented Programming Methods (Stage 4.5)
    // ========================================================================

    /// <summary>
    /// Gets the dashboard statistics using Railway-Oriented Programming.
    /// Returns Result&lt;DashboardStats&gt; instead of throwing exceptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing DashboardStats on success.</returns>
    public async Task<Result<DashboardStats>> GetStatsWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<DashboardStats>();
        }

        var stats = await GetStatsAsync(cancellationToken).ConfigureAwait(false);
        return Result<DashboardStats>.Success(stats);
    }
}

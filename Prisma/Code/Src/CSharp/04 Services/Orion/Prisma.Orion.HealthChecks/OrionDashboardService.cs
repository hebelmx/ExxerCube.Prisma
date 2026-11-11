using Microsoft.Extensions.Logging;

namespace Prisma.Orion.HealthChecks;

/// <summary>
/// Dashboard service for Orion ingestion worker.
/// </summary>
/// <remarks>
/// Provides real-time metrics and statistics for monitoring:
/// - Documents processed count (incremented by the pipeline via <see cref="IDashboardService.RecordDocumentProcessed"/>)
/// - Last event timestamp
/// - Queue depth
/// - Heartbeat timestamp
/// </remarks>
public sealed class OrionDashboardService : IDashboardService
{
    private readonly ILogger<OrionDashboardService> _logger;
    private DateTime _lastHeartbeat;
    private int _documentsProcessed;
    private DateTime? _lastEventTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrionDashboardService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public OrionDashboardService(ILogger<OrionDashboardService> logger)
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
            WorkerName: "Orion Ingestion Worker",
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
            "Orion dashboard: document recorded. Total processed: {DocumentsProcessed}",
            total);
    }

    // ========================================================================
    // Railway-Oriented Programming Methods (Stage 4.5)
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

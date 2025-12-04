namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Service for providing dashboard metrics and statistics for orchestrator workers.
/// </summary>
/// <remarks>
/// Dashboard exposes real-time metrics for monitoring and observability:
/// - Documents processed count
/// - Last event timestamp
/// - Queue depth (if available)
/// - Worker heartbeat
/// </remarks>
public interface IDashboardService
{
    /// <summary>
    /// Gets current dashboard statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dashboard statistics.</returns>
    Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Dashboard statistics.
/// </summary>
/// <param name="WorkerName">Name of the worker.</param>
/// <param name="Status">Current worker status.</param>
/// <param name="DocumentsProcessed">Total documents processed since start.</param>
/// <param name="LastEventTime">Timestamp of last processed event (null if none).</param>
/// <param name="LastHeartbeat">Timestamp of last heartbeat.</param>
/// <param name="QueueDepth">Current queue depth (0 if not available).</param>
public record DashboardStats(
    string WorkerName,
    string Status,
    int DocumentsProcessed,
    DateTime? LastEventTime,
    DateTime? LastHeartbeat,
    int QueueDepth);

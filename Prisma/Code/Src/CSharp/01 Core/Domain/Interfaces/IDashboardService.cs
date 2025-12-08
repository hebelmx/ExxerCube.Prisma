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
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

    /// <summary>
    /// Records a completed document, incrementing the processed count and refreshing the last-event timestamp.
    /// Called by the pipeline at the moment a document successfully completes its work unit (extraction or ingestion).
    /// </summary>
    void RecordDocumentProcessed();
}
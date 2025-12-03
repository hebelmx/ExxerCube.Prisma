namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Abstraction for monitoring worker heartbeats and detecting failures.
/// </summary>
public interface IHeartbeatMonitor
{
    /// <summary>
    /// Records a heartbeat from a worker.
    /// </summary>
    /// <param name="heartbeat">Worker heartbeat event.</param>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation.</param>
    Task RecordHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for workers that have missed heartbeats beyond SLA threshold.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation.</param>
    /// <returns>Collection of worker IDs that need restart.</returns>
    Task<IEnumerable<string>> GetFailedWorkersAsync(CancellationToken cancellationToken = default);
}

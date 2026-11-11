namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Abstraction for restarting failed/zombie worker processes.
/// </summary>
public interface IProcessRestarter
{
    /// <summary>
    /// Attempts to restart a worker process.
    /// </summary>
    /// <param name="workerId">Unique identifier for the worker to restart.</param>
    /// <param name="workerName">Human-readable worker name.</param>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation.</param>
    /// <returns>True if restart succeeded, false otherwise.</returns>
    Task<bool> RestartAsync(string workerId, string workerName, CancellationToken cancellationToken = default);
}

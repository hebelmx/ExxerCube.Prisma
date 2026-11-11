namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Result of a worker check operation.
/// </summary>
/// <param name="WorkersChecked">Total number of failed workers detected.</param>
/// <param name="WorkersRestarted">Number of workers successfully restarted.</param>
/// <param name="WorkersFailed">Number of workers that failed to restart.</param>
/// <param name="FailedWorkerIds">IDs of workers that failed to restart.</param>
public sealed record CheckWorkersResult(
    int WorkersChecked,
    int WorkersRestarted,
    int WorkersFailed,
    IReadOnlyList<string> FailedWorkerIds);

using SnakeWorker.Core.Models;

namespace SnakeWorker.Core.Interfaces;

/// <summary>
/// Interface for task queue operations
/// </summary>
public interface ITaskQueue
{
    /// <summary>
    /// Enqueue a task for processing
    /// </summary>
    /// <param name="task">Task to enqueue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnqueueAsync(WorkerTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeue the next available task
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next task to process, or null if none available</returns>
    Task<WorkerTask?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current queue size
    /// </summary>
    /// <returns>Number of tasks in queue</returns>
    Task<int> GetQueueSizeAsync();

    /// <summary>
    /// Get tasks by status
    /// </summary>
    /// <param name="status">Task status to filter by</param>
    /// <param name="limit">Maximum number of tasks to return</param>
    /// <returns>List of tasks</returns>
    Task<IEnumerable<WorkerTask>> GetTasksByStatusAsync(string status, int limit = 100);

    /// <summary>
    /// Clear all tasks from the queue
    /// </summary>
    Task ClearAsync();
}
using Microsoft.Extensions.Logging;
using SnakeWorker.Core.Interfaces;
using SnakeWorker.Core.Models;
using System.Collections.Concurrent;

namespace SnakeWorker.Core.Services;

/// <summary>
/// In-memory implementation of task queue
/// </summary>
public class InMemoryTaskQueue : ITaskQueue
{
    private readonly ConcurrentQueue<WorkerTask> _taskQueue = new();
    private readonly ConcurrentDictionary<string, WorkerTask> _allTasks = new();
    private readonly ILogger<InMemoryTaskQueue> _logger;

    public InMemoryTaskQueue(ILogger<InMemoryTaskQueue> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task EnqueueAsync(WorkerTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enqueuing task {TaskId} of type {TaskType}", task.Id, task.TaskType);
        
        _allTasks[task.Id] = task;
        _taskQueue.Enqueue(task);
        
        _logger.LogDebug("Task {TaskId} enqueued. Queue size: {QueueSize}", task.Id, _taskQueue.Count);
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkerTask?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        if (_taskQueue.TryDequeue(out var task))
        {
            // Check if task should be executed now (for delayed tasks)
            if (task.ScheduledAt.HasValue && task.ScheduledAt > DateTime.UtcNow)
            {
                // Re-enqueue for later
                _taskQueue.Enqueue(task);
                return Task.FromResult<WorkerTask?>(null);
            }

            _logger.LogInformation("Dequeuing task {TaskId} of type {TaskType}", task.Id, task.TaskType);
            return Task.FromResult<WorkerTask?>(task);
        }

        return Task.FromResult<WorkerTask?>(null);
    }

    /// <inheritdoc />
    public Task<int> GetQueueSizeAsync()
    {
        return Task.FromResult(_taskQueue.Count);
    }

    /// <inheritdoc />
    public Task<IEnumerable<WorkerTask>> GetTasksByStatusAsync(string status, int limit = 100)
    {
        var tasks = _allTasks.Values
            .Where(t => t.Metadata.ContainsKey("status") && t.Metadata["status"].ToString() == status)
            .Take(limit);
            
        return Task.FromResult(tasks);
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        _logger.LogWarning("Clearing all tasks from queue");
        
        while (_taskQueue.TryDequeue(out _)) { }
        _allTasks.Clear();
        
        return Task.CompletedTask;
    }
}
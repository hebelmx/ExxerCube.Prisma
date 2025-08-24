using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SnakeWorker.Core.Interfaces;
using SnakeWorker.Core.Models;
using System.Diagnostics;

namespace SnakeWorker.Core.Services;

/// <summary>
/// Background worker service that processes tasks from the queue
/// </summary>
public class BackgroundWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundWorkerService> _logger;
    private readonly WorkerOptions _options;

    public BackgroundWorkerService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundWorkerService> logger,
        IOptions<WorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background worker service started");

        // Start multiple worker tasks based on configuration
        var workerTasks = new List<Task>();
        
        for (int i = 0; i < _options.ConcurrentWorkers; i++)
        {
            var workerId = i + 1;
            var workerTask = ProcessTasksAsync(workerId, stoppingToken);
            workerTasks.Add(workerTask);
        }

        try
        {
            await Task.WhenAll(workerTasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background worker service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background worker service encountered an error");
            throw;
        }
    }

    private async Task ProcessTasksAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var taskQueue = scope.ServiceProvider.GetRequiredService<ITaskQueue>();
                var pythonExecutor = scope.ServiceProvider.GetRequiredService<IPythonExecutor>();
                var metricsCollector = scope.ServiceProvider.GetRequiredService<IMetricsCollector>();

                // Try to get a task from the queue
                var task = await taskQueue.DequeueAsync(cancellationToken);
                
                if (task == null)
                {
                    // No tasks available, wait before trying again
                    await Task.Delay(_options.PollingIntervalMs, cancellationToken);
                    continue;
                }

                // Record that we're processing a task
                metricsCollector.IncrementCounter("tasks.dequeued", 1.0, new Dictionary<string, string>
                {
                    ["worker_id"] = workerId.ToString(),
                    ["task_type"] = task.TaskType
                });

                // Process the task
                await ProcessSingleTaskAsync(task, pythonExecutor, metricsCollector, workerId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} encountered an error", workerId);
                
                // Wait before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessSingleTaskAsync(
        WorkerTask task,
        IPythonExecutor pythonExecutor,
        IMetricsCollector metricsCollector,
        int workerId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var tags = new Dictionary<string, string>
        {
            ["worker_id"] = workerId.ToString(),
            ["task_type"] = task.TaskType,
            ["task_id"] = task.Id
        };

        _logger.LogInformation("Worker {WorkerId} processing task {TaskId} ({TaskType})", 
            workerId, task.Id, task.TaskType);

        TaskResult? result = null;
        bool shouldRetry = false;

        try
        {
            // Execute the Python task
            result = await pythonExecutor.ExecuteAsync(task, cancellationToken);

            if (result.IsSuccess)
            {
                metricsCollector.IncrementCounter("tasks.completed", 1.0, tags);
                metricsCollector.RecordHistogram("tasks.duration_ms", result.Duration.TotalMilliseconds, tags);
                metricsCollector.RecordHistogram("tasks.memory_usage_bytes", result.MemoryUsageBytes, tags);

                _logger.LogInformation("Worker {WorkerId} completed task {TaskId} in {Duration}ms", 
                    workerId, task.Id, result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Worker {WorkerId} task {TaskId} failed: {Error}", 
                    workerId, task.Id, result.ErrorMessage);

                shouldRetry = ShouldRetryTask(task, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {WorkerId} encountered exception processing task {TaskId}", 
                workerId, task.Id);

            result = new TaskResult
            {
                TaskId = task.Id,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ErrorDetails = ex.ToString(),
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            shouldRetry = ShouldRetryTask(task, result);
        }
        finally
        {
            stopwatch.Stop();
            
            // Record processing metrics
            metricsCollector.IncrementCounter("tasks.processed", 1.0, tags);
            
            if (result?.IsSuccess == false)
            {
                metricsCollector.IncrementCounter("tasks.failed", 1.0, tags);
            }
        }

        // Handle retries
        if (shouldRetry)
        {
            await HandleTaskRetryAsync(task, result, metricsCollector, workerId);
        }
        else
        {
            // Task completed (successfully or failed with no retries left)
            await HandleTaskCompletionAsync(task, result, metricsCollector, workerId);
        }
    }

    private bool ShouldRetryTask(WorkerTask task, TaskResult? result)
    {
        if (task.CurrentRetry >= task.MaxRetries)
        {
            return false;
        }

        // Don't retry certain types of errors
        if (result?.ErrorMessage?.Contains("ValueError") == true ||
            result?.ErrorMessage?.Contains("TypeError") == true)
        {
            return false;
        }

        return true;
    }

    private async Task HandleTaskRetryAsync(
        WorkerTask task,
        TaskResult? result,
        IMetricsCollector metricsCollector,
        int workerId)
    {
        task.CurrentRetry++;
        
        var retryDelayMs = CalculateRetryDelay(task.CurrentRetry);
        task.ScheduledAt = DateTime.UtcNow.AddMilliseconds(retryDelayMs);

        _logger.LogInformation("Worker {WorkerId} scheduling retry {Retry}/{MaxRetries} for task {TaskId} in {DelayMs}ms",
            workerId, task.CurrentRetry, task.MaxRetries, task.Id, retryDelayMs);

        metricsCollector.IncrementCounter("tasks.retried", 1.0, new Dictionary<string, string>
        {
            ["worker_id"] = workerId.ToString(),
            ["task_type"] = task.TaskType,
            ["retry_count"] = task.CurrentRetry.ToString()
        });

        // Re-enqueue the task for retry
        using var scope = _scopeFactory.CreateScope();
        var taskQueue = scope.ServiceProvider.GetRequiredService<ITaskQueue>();
        await taskQueue.EnqueueAsync(task);
    }

    private async Task HandleTaskCompletionAsync(
        WorkerTask task,
        TaskResult? result,
        IMetricsCollector metricsCollector,
        int workerId)
    {
        if (result?.IsSuccess == true)
        {
            _logger.LogInformation("Worker {WorkerId} completed task {TaskId} successfully", workerId, task.Id);
        }
        else
        {
            _logger.LogError("Worker {WorkerId} failed to complete task {TaskId} after {Retries} retries. Final error: {Error}",
                workerId, task.Id, task.CurrentRetry, result?.ErrorMessage);

            metricsCollector.IncrementCounter("tasks.failed_final", 1.0, new Dictionary<string, string>
            {
                ["worker_id"] = workerId.ToString(),
                ["task_type"] = task.TaskType
            });
        }

        // Here you could implement result storage (database, file system, etc.)
        await Task.CompletedTask;
    }

    private static int CalculateRetryDelay(int retryCount)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, etc. (capped at 60s)
        var delayMs = (int)Math.Min(1000 * Math.Pow(2, retryCount - 1), 60000);
        
        // Add jitter to prevent thundering herd
        var jitter = new Random().Next(0, delayMs / 4);
        
        return delayMs + jitter;
    }
}

/// <summary>
/// Configuration options for the background worker
/// </summary>
public class WorkerOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Worker";

    /// <summary>
    /// Number of concurrent worker threads
    /// </summary>
    public int ConcurrentWorkers { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Polling interval when no tasks are available (milliseconds)
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Default maximum retries for failed tasks
    /// </summary>
    public int DefaultMaxRetries { get; set; } = 3;

    /// <summary>
    /// Default task timeout (seconds)
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;
}
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SnakeWorker.Core.Interfaces;

namespace SnakeWorker.Host.Services;

/// <summary>
/// Health check for Python environment
/// </summary>
public class PythonHealthCheck : IHealthCheck
{
    private readonly IPythonExecutor _pythonExecutor;

    public PythonHealthCheck(IPythonExecutor pythonExecutor)
    {
        _pythonExecutor = pythonExecutor;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if basic Python modules are available
            var sysAvailable = await _pythonExecutor.IsModuleAvailableAsync("sys");
            var osAvailable = await _pythonExecutor.IsModuleAvailableAsync("os");

            if (sysAvailable && osAvailable)
            {
                var data = new Dictionary<string, object>
                {
                    ["sys_module"] = sysAvailable,
                    ["os_module"] = osAvailable
                };

                return HealthCheckResult.Healthy("Python environment is healthy", data);
            }
            else
            {
                var data = new Dictionary<string, object>
                {
                    ["sys_module"] = sysAvailable,
                    ["os_module"] = osAvailable
                };

                return HealthCheckResult.Degraded("Some Python modules are not available", null, data);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Python environment check failed", ex);
        }
    }
}

/// <summary>
/// Health check for task queue
/// </summary>
public class QueueHealthCheck : IHealthCheck
{
    private readonly ITaskQueue _taskQueue;

    public QueueHealthCheck(ITaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueSize = await _taskQueue.GetQueueSizeAsync();

            var data = new Dictionary<string, object>
            {
                ["queue_size"] = queueSize
            };

            if (queueSize < 1000) // Threshold for healthy queue size
            {
                return HealthCheckResult.Healthy($"Task queue is healthy (size: {queueSize})", data);
            }
            else if (queueSize < 5000)
            {
                return HealthCheckResult.Degraded($"Task queue is getting large (size: {queueSize})", null, data);
            }
            else
            {
                return HealthCheckResult.Unhealthy($"Task queue is too large (size: {queueSize})", null, data);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Task queue check failed", ex);
        }
    }
}
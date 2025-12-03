using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Sentinel monitoring service for detecting zombie/dead workers and triggering restarts.
/// </summary>
public sealed class SentinelService
{
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly IProcessRestarter _processRestarter;
    private readonly ISentinelConfiguration _configuration;
    private readonly ILogger<SentinelService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentinelService"/> class.
    /// </summary>
    /// <param name="heartbeatMonitor">Heartbeat monitoring service.</param>
    /// <param name="processRestarter">Process restart service.</param>
    /// <param name="configuration">Sentinel configuration.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SentinelService(
        IHeartbeatMonitor heartbeatMonitor,
        IProcessRestarter processRestarter,
        ISentinelConfiguration configuration,
        ILogger<SentinelService> logger)
    {
        _heartbeatMonitor = heartbeatMonitor ?? throw new ArgumentNullException(nameof(heartbeatMonitor));
        _processRestarter = processRestarter ?? throw new ArgumentNullException(nameof(processRestarter));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks for failed workers and triggers restarts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckWorkersAsync(CancellationToken cancellationToken = default)
    {
        var failedWorkers = await _heartbeatMonitor.GetFailedWorkersAsync(cancellationToken);

        foreach (var workerId in failedWorkers)
        {
            _logger.LogWarning("Worker {WorkerId} has failed heartbeat checks, attempting restart", workerId);

            var success = await _processRestarter.RestartAsync(workerId, workerId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Successfully restarted worker {WorkerId}", workerId);
            }
            else
            {
                _logger.LogError("Failed to restart worker {WorkerId}", workerId);
            }
        }
    }

    /// <summary>
    /// Starts the sentinel monitoring service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task MonitorAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sentinel monitoring service started with check interval {Interval}", _configuration.CheckInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckWorkersAsync(cancellationToken);
                await Task.Delay(_configuration.CheckInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sentinel monitoring service stopping gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sentinel monitoring check");
                // Continue monitoring despite errors
                await Task.Delay(_configuration.CheckInterval, cancellationToken);
            }
        }

        _logger.LogInformation("Sentinel monitoring service stopped");
    }

    // ========================================================================
    // NEW: Railway-Oriented Programming Methods (Stage 5.5)
    // ========================================================================

    /// <summary>
    /// Checks for failed workers and triggers restarts using Railway-Oriented Programming.
    /// Returns Result&lt;CheckWorkersResult&gt; with detailed statistics about the operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation.</param>
    /// <returns>A Result containing CheckWorkersResult with operation statistics.</returns>
    public async Task<Result<CheckWorkersResult>> CheckWorkersWithResultAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<CheckWorkersResult>();
        }

        var failedWorkers = (await _heartbeatMonitor.GetFailedWorkersAsync(cancellationToken)).ToList();

        var workersChecked = failedWorkers.Count;
        var workersRestarted = 0;
        var failedRestarts = new List<string>();

        foreach (var workerId in failedWorkers)
        {
            _logger.LogWarning("Worker {WorkerId} has failed heartbeat checks, attempting restart", workerId);

            var success = await _processRestarter.RestartAsync(workerId, workerId, cancellationToken);

            if (success)
            {
                workersRestarted++;
                _logger.LogInformation("Successfully restarted worker {WorkerId}", workerId);
            }
            else
            {
                failedRestarts.Add(workerId);
                _logger.LogError("Failed to restart worker {WorkerId}", workerId);
            }
        }

        var result = new CheckWorkersResult(
            WorkersChecked: workersChecked,
            WorkersRestarted: workersRestarted,
            WorkersFailed: failedRestarts.Count,
            FailedWorkerIds: failedRestarts.AsReadOnly());

        return Result<CheckWorkersResult>.Success(result);
    }
}
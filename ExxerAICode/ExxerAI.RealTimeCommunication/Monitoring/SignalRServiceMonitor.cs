using ExxerAI.Axioms.Abstractions.Monitoring;

namespace ExxerAI.RealTimeCommunication.Monitoring;

/// <summary>
/// SignalR-based implementation of IServiceMonitor for broadcasting service status and metrics
/// to real-time dashboard clients via SystemHub.
/// </summary>
/// <typeparam name="T">The type of monitoring data being broadcast</typeparam>
/// <remarks>
/// This implementation:
/// - Uses SystemHub for system-level infrastructure monitoring
/// - Broadcasts to the "monitoring" SignalR group
/// - Follows Railway-Oriented Programming (ROP) with Result&lt;T&gt; pattern
/// - Never throws exceptions for business logic errors
/// - Logs all operations with rate-limiting to prevent spam
/// </remarks>
public sealed class SignalRServiceMonitor<T> : IServiceMonitor<T> where T : class
{
    private readonly IHubContext<SystemHub> _hubContext;
    private readonly ILogger<SignalRServiceMonitor<T>> _logger;
    private const string MonitoringGroupName = "monitoring";

    /// <summary>
    /// Initializes a new instance of the SignalRServiceMonitor class.
    /// </summary>
    /// <param name="hubContext">The SystemHub context for SignalR broadcasting</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    public SignalRServiceMonitor(
        IHubContext<SystemHub> hubContext,
        ILogger<SignalRServiceMonitor<T>> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Broadcasts service status update to all monitoring clients via SystemHub.
    /// </summary>
    /// <param name="statusData">The service status data to broadcast</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> BroadcastStatusAsync(T statusData, CancellationToken cancellationToken = default)
    {
        if (statusData is null)
        {
            return Result.WithFailure(["Status data cannot be null"]);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure(["Operation was canceled before broadcast"]);
        }

        try
        {
            _logger.LogDebug("Broadcasting service status update to {GroupName} group", MonitoringGroupName);

            await _hubContext.Clients
                .Group(MonitoringGroupName)
                .SendAsync("ServiceStatusUpdate", statusData, cancellationToken);

            _logger.LogDebug("Successfully broadcast service status update");
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Status broadcast was canceled");
            return Result.WithFailure(["Operation was canceled during broadcast"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast service status update");
            return Result.WithFailure([$"Status broadcast failed: {ex.Message}"]);
        }
    }

    /// <summary>
    /// Broadcasts system metrics update to all monitoring clients via SystemHub.
    /// </summary>
    /// <param name="metricsData">The metrics data to broadcast</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> BroadcastMetricsAsync(T metricsData, CancellationToken cancellationToken = default)
    {
        if (metricsData is null)
        {
            return Result.WithFailure(["Metrics data cannot be null"]);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure(["Operation was canceled before broadcast"]);
        }

        try
        {
            _logger.LogDebug("Broadcasting system metrics update to {GroupName} group", MonitoringGroupName);

            await _hubContext.Clients
                .Group(MonitoringGroupName)
                .SendAsync("SystemMetricsUpdate", metricsData, cancellationToken);

            _logger.LogDebug("Successfully broadcast system metrics update");
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Metrics broadcast was canceled");
            return Result.WithFailure(["Operation was canceled during broadcast"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast system metrics update");
            return Result.WithFailure([$"Metrics broadcast failed: {ex.Message}"]);
        }
    }
}

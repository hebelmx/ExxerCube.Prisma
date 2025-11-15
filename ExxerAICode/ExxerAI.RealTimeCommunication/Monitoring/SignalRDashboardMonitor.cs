using ExxerAI.Axioms.Abstractions.Monitoring;

namespace ExxerAI.RealTimeCommunication.Monitoring;

/// <summary>
/// SignalR-based implementation of IDashboardMonitor for dashboard-specific monitoring
/// with custom update types and explicit subscription management.
/// </summary>
/// <typeparam name="T">The type of data being sent to the dashboard</typeparam>
/// <remarks>
/// This implementation:
/// - Uses SystemHub for dashboard communications
/// - Supports custom update types for flexible dashboard patterns
/// - Provides explicit group subscription management
/// - Follows Railway-Oriented Programming (ROP) with Result&lt;T&gt; pattern
/// - Never throws exceptions for business logic errors
/// </remarks>
public sealed class SignalRDashboardMonitor<T> : IDashboardMonitor<T> where T : class
{
    private readonly IHubContext<SystemHub> _hubContext;
    private readonly ILogger<SignalRDashboardMonitor<T>> _logger;

    /// <summary>
    /// Initializes a new instance of the SignalRDashboardMonitor class.
    /// </summary>
    /// <param name="hubContext">The SystemHub context for SignalR broadcasting</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    public SignalRDashboardMonitor(
        IHubContext<SystemHub> hubContext,
        ILogger<SignalRDashboardMonitor<T>> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sends a custom update to all dashboard clients.
    /// </summary>
    /// <param name="updateType">The type of update (e.g., "ServiceStatusUpdate", "AlertTriggered")</param>
    /// <param name="data">The data payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> SendUpdateAsync(string updateType, T data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateType))
        {
            return Result.WithFailure(["Update type cannot be null or empty"]);
        }

        if (data is null)
        {
            return Result.WithFailure(["Data cannot be null"]);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure(["Operation was canceled before sending update"]);
        }

        try
        {
            _logger.LogDebug("Sending dashboard update: {UpdateType}", updateType);

            await _hubContext.Clients.All
                .SendAsync(updateType, data, cancellationToken);

            _logger.LogDebug("Successfully sent dashboard update: {UpdateType}", updateType);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard update was canceled: {UpdateType}", updateType);
            return Result.WithFailure(["Operation was canceled during update"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send dashboard update: {UpdateType}", updateType);
            return Result.WithFailure([$"Dashboard update failed: {ex.Message}"]);
        }
    }

    /// <summary>
    /// Subscribes a client to a monitoring group.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <param name="groupName">The group name to subscribe to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> SubscribeAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Result.WithFailure(["Connection ID cannot be null or empty"]);
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Result.WithFailure(["Group name cannot be null or empty"]);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure(["Operation was canceled before subscription"]);
        }

        try
        {
            _logger.LogDebug("Subscribing connection {ConnectionId} to group {GroupName}", connectionId, groupName);

            await _hubContext.Groups.AddToGroupAsync(connectionId, groupName, cancellationToken);

            _logger.LogInformation("Connection {ConnectionId} subscribed to {GroupName}", connectionId, groupName);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Subscription canceled for {ConnectionId} to {GroupName}", connectionId, groupName);
            return Result.WithFailure(["Operation was canceled during subscription"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe {ConnectionId} to {GroupName}", connectionId, groupName);
            return Result.WithFailure([$"Subscription failed: {ex.Message}"]);
        }
    }

    /// <summary>
    /// Unsubscribes a client from a monitoring group.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <param name="groupName">The group name to unsubscribe from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> UnsubscribeAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Result.WithFailure(["Connection ID cannot be null or empty"]);
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Result.WithFailure(["Group name cannot be null or empty"]);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.WithFailure(["Operation was canceled before unsubscription"]);
        }

        try
        {
            _logger.LogDebug("Unsubscribing connection {ConnectionId} from group {GroupName}", connectionId, groupName);

            await _hubContext.Groups.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);

            _logger.LogInformation("Connection {ConnectionId} unsubscribed from {GroupName}", connectionId, groupName);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Unsubscription canceled for {ConnectionId} from {GroupName}", connectionId, groupName);
            return Result.WithFailure(["Operation was canceled during unsubscription"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe {ConnectionId} from {GroupName}", connectionId, groupName);
            return Result.WithFailure([$"Unsubscription failed: {ex.Message}"]);
        }
    }
}

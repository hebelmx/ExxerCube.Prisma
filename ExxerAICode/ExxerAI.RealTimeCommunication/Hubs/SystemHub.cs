namespace ExxerAI.RealTimeCommunication.Hubs;

/// <summary>
/// SignalR hub for system-level real-time communications including health monitoring and metrics.
/// </summary>
/// <remarks>
/// <para>
/// This hub provides:
/// - Service health status updates
/// - System metrics broadcasting
/// - Monitoring group subscription management
/// - Infrastructure health notifications
/// </para>
/// <para>
/// Clients can join the "monitoring" group to receive real-time system updates.
/// </para>
/// </remarks>
public sealed class SystemHub : Hub
{
    /// <summary>
    /// Join the monitoring group for real-time system health updates.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task JoinMonitoringGroupAsync(CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "monitoring", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // SignalR hub methods handle cancellation gracefully
            return;
        }
    }

    /// <summary>
    /// Leave the monitoring group.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task LeaveMonitoringGroupAsync(CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "monitoring", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // SignalR hub methods handle cancellation gracefully
            return;
        }
    }
}

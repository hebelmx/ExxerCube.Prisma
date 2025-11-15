namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// SignalR hub for system-wide communications
    /// </summary>
    public sealed class SystemHub : BaseHub<SystemHub>
    {
        /// <summary>
        /// Initializes a new instance of the SystemHub
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public SystemHub(ILogger<SystemHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Joins the system monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinSystemMonitoringAsync()
        {
            await AddToGroupAsync("SystemMonitoring");
            Logger.LogInformation("Connection {ConnectionId} joined SystemMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the system monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveSystemMonitoringAsync()
        {
            await RemoveFromGroupAsync("SystemMonitoring");
            Logger.LogInformation("Connection {ConnectionId} left SystemMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Joins the system alerts group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinSystemAlertsAsync()
        {
            await AddToGroupAsync("SystemAlerts");
            Logger.LogInformation("Connection {ConnectionId} joined SystemAlerts group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the system alerts group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveSystemAlertsAsync()
        {
            await RemoveFromGroupAsync("SystemAlerts");
            Logger.LogInformation("Connection {ConnectionId} left SystemAlerts group", Context.ConnectionId);
        }
    }
}
namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// SignalR hub for economic-related communications
    /// </summary>
    public sealed class EconomicHub : BaseHub<EconomicHub>
    {
        /// <summary>
        /// Initializes a new instance of the EconomicHub
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public EconomicHub(ILogger<EconomicHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Joins the economic monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinEconomicMonitoringAsync()
        {
            await AddToGroupAsync("EconomicMonitoring");
            Logger.LogInformation("Connection {ConnectionId} joined EconomicMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the economic monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveEconomicMonitoringAsync()
        {
            await RemoveFromGroupAsync("EconomicMonitoring");
            Logger.LogInformation("Connection {ConnectionId} left EconomicMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Joins the cost analysis group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinCostAnalysisAsync()
        {
            await AddToGroupAsync("CostAnalysis");
            Logger.LogInformation("Connection {ConnectionId} joined CostAnalysis group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the cost analysis group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveCostAnalysisAsync()
        {
            await RemoveFromGroupAsync("CostAnalysis");
            Logger.LogInformation("Connection {ConnectionId} left CostAnalysis group", Context.ConnectionId);
        }

        /// <summary>
        /// Joins the economic alerts group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinEconomicAlertsAsync()
        {
            await AddToGroupAsync("EconomicAlerts");
            Logger.LogInformation("Connection {ConnectionId} joined EconomicAlerts group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the economic alerts group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveEconomicAlertsAsync()
        {
            await RemoveFromGroupAsync("EconomicAlerts");
            Logger.LogInformation("Connection {ConnectionId} left EconomicAlerts group", Context.ConnectionId);
        }
    }
}
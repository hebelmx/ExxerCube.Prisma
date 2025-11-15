namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// SignalR hub for agent-related communications
    /// </summary>
    public sealed class AgentHub : BaseHub<AgentHub>
    {
        /// <summary>
        /// Initializes a new instance of the AgentHub
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public AgentHub(ILogger<AgentHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Joins an agent coordination group
        /// </summary>
        /// <param name="agentId">The ID of the agent</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinAgentGroupAsync(string agentId)
        {
            var groupName = $"Agent_{agentId}";
            await AddToGroupAsync(groupName);
            Logger.LogInformation("Connection {ConnectionId} joined agent group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Leaves an agent coordination group
        /// </summary>
        /// <param name="agentId">The ID of the agent</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveAgentGroupAsync(string agentId)
        {
            var groupName = $"Agent_{agentId}";
            await RemoveFromGroupAsync(groupName);
            Logger.LogInformation("Connection {ConnectionId} left agent group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Joins the agent status monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinAgentStatusMonitoringAsync()
        {
            await AddToGroupAsync("AgentStatusMonitoring");
            Logger.LogInformation("Connection {ConnectionId} joined AgentStatusMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the agent status monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveAgentStatusMonitoringAsync()
        {
            await RemoveFromGroupAsync("AgentStatusMonitoring");
            Logger.LogInformation("Connection {ConnectionId} left AgentStatusMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Joins the agent coordination group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinAgentCoordinationAsync()
        {
            await AddToGroupAsync("AgentCoordination");
            Logger.LogInformation("Connection {ConnectionId} joined AgentCoordination group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the agent coordination group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveAgentCoordinationAsync()
        {
            await RemoveFromGroupAsync("AgentCoordination");
            Logger.LogInformation("Connection {ConnectionId} left AgentCoordination group", Context.ConnectionId);
        }
    }
}
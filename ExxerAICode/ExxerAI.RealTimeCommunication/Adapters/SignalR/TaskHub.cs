namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// SignalR hub for task-related communications
    /// </summary>
    public sealed class TaskHub : BaseHub<TaskHub>
    {
        /// <summary>
        /// Initializes a new instance of the TaskHub
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public TaskHub(ILogger<TaskHub> logger) : base(logger)
        {
        }

        /// <summary>
        /// Joins a task monitoring group
        /// </summary>
        /// <param name="taskId">The ID of the task</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinTaskGroupAsync(string taskId)
        {
            var groupName = $"Task_{taskId}";
            await AddToGroupAsync(groupName);
            Logger.LogInformation("Connection {ConnectionId} joined task group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Leaves a task monitoring group
        /// </summary>
        /// <param name="taskId">The ID of the task</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveTaskGroupAsync(string taskId)
        {
            var groupName = $"Task_{taskId}";
            await RemoveFromGroupAsync(groupName);
            Logger.LogInformation("Connection {ConnectionId} left task group {GroupName}", 
                Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Joins the task progress monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinTaskProgressMonitoringAsync()
        {
            await AddToGroupAsync("TaskProgressMonitoring");
            Logger.LogInformation("Connection {ConnectionId} joined TaskProgressMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the task progress monitoring group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveTaskProgressMonitoringAsync()
        {
            await RemoveFromGroupAsync("TaskProgressMonitoring");
            Logger.LogInformation("Connection {ConnectionId} left TaskProgressMonitoring group", Context.ConnectionId);
        }

        /// <summary>
        /// Joins the task distribution group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task JoinTaskDistributionAsync()
        {
            await AddToGroupAsync("TaskDistribution");
            Logger.LogInformation("Connection {ConnectionId} joined TaskDistribution group", Context.ConnectionId);
        }

        /// <summary>
        /// Leaves the task distribution group
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LeaveTaskDistributionAsync()
        {
            await RemoveFromGroupAsync("TaskDistribution");
            Logger.LogInformation("Connection {ConnectionId} left TaskDistribution group", Context.ConnectionId);
        }
    }
}
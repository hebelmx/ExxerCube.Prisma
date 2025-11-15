namespace ExxerAI.RealTimeCommunication.Adapters.SignalR
{
    /// <summary>
    /// Base hub class providing common functionality for all ExxerAI hubs
    /// </summary>
    /// <typeparam name="THub">The hub type</typeparam>
    public abstract class BaseHub<THub> : Hub where THub : BaseHub<THub>
    {
        /// <summary>
        /// Logger instance for the hub
        /// </summary>
        protected readonly ILogger<THub> Logger;

        /// <summary>
        /// Initializes a new instance of the base hub
        /// </summary>
        /// <param name="logger">The logger instance</param>
        protected BaseHub(ILogger<THub> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when a new connection is established
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public override async Task OnConnectedAsync()
        {
            Logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a connection is terminated
        /// </summary>
        /// <param name="exception">The exception that caused the disconnection, if any</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                Logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                Logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            }
        
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Adds the current connection to a group
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected async Task AddToGroupAsync(string groupName)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                Logger.LogInformation("Added connection {ConnectionId} to group {GroupName}", 
                    Context.ConnectionId, groupName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add connection {ConnectionId} to group {GroupName}", 
                    Context.ConnectionId, groupName);
                throw;
            }
        }

        /// <summary>
        /// Removes the current connection from a group
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected async Task RemoveFromGroupAsync(string groupName)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                Logger.LogInformation("Removed connection {ConnectionId} from group {GroupName}", 
                    Context.ConnectionId, groupName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to remove connection {ConnectionId} from group {GroupName}", 
                    Context.ConnectionId, groupName);
                throw;
            }
        }
    }
}
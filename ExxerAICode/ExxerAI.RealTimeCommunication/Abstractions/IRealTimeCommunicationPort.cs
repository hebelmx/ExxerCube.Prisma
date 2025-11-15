namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Port for real-time communication operations - infrastructure-agnostic
    /// </summary>
    public interface IRealTimeCommunicationPort
    {
        /// <summary>
        /// Broadcasts a message to all connected clients
        /// </summary>
        /// <param name="request">The broadcast request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> BroadcastMessageAsync(BroadcastMessageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to a specific group of clients
        /// </summary>
        /// <param name="request">The group message request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendToGroupAsync(GroupMessageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to a specific user
        /// </summary>
        /// <param name="request">The user message request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendToUserAsync(UserMessageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a connection to a group
        /// </summary>
        /// <param name="request">The add to group request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> AddToGroupAsync(AddToGroupRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a connection from a group
        /// </summary>
        /// <param name="request">The remove from group request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> RemoveFromGroupAsync(RemoveFromGroupRequest request, CancellationToken cancellationToken = default);
    }
}
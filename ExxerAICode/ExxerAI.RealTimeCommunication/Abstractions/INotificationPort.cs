namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Port for notification operations - infrastructure-agnostic
    /// </summary>
    public interface INotificationPort
    {
        /// <summary>
        /// Sends a notification to clients
        /// </summary>
        /// <param name="request">The notification request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an alert to clients
        /// </summary>
        /// <param name="request">The alert request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendAlertAsync(AlertRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a status update to clients
        /// </summary>
        /// <param name="request">The status update request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendStatusUpdateAsync(StatusUpdateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a progress update to clients
        /// </summary>
        /// <param name="request">The progress update request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or failure</returns>
        Task<Result> SendProgressUpdateAsync(ProgressUpdateRequest request, CancellationToken cancellationToken = default);
    }
}
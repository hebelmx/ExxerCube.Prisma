namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Notification request
    /// </summary>
    /// <param name="Target">The communication target</param>
    /// <param name="NotificationType">The type of notification</param>
    /// <param name="Title">The notification title</param>
    /// <param name="Message">The notification message</param>
    /// <param name="Data">Additional notification data</param>
    public sealed record NotificationRequest(
        CommunicationTarget Target,
        string NotificationType,
        string Title,
        string Message,
        object? Data = null);
}
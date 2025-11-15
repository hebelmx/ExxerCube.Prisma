namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Request to send a message to a specific user
    /// </summary>
    /// <param name="Target">The communication target/hub</param>
    /// <param name="UserId">The ID of the target user</param>
    /// <param name="MessageType">The type of message being sent</param>
    /// <param name="Data">The message data</param>
    public sealed record UserMessageRequest(
        CommunicationTarget Target,
        string UserId,
        string MessageType,
        object Data);
}
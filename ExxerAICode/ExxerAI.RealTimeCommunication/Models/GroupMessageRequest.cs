namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Request to send a message to a specific group
    /// </summary>
    /// <param name="Target">The communication target/hub</param>
    /// <param name="GroupName">The name of the group</param>
    /// <param name="MessageType">The type of message being sent</param>
    /// <param name="Data">The message data</param>
    public sealed record GroupMessageRequest(
        CommunicationTarget Target,
        string GroupName,
        string MessageType,
        object Data);
}
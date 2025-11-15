namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Request to broadcast a message to all clients
    /// </summary>
    /// <param name="Target">The communication target/hub</param>
    /// <param name="MessageType">The type of message being sent</param>
    /// <param name="Data">The message data</param>
    public sealed record BroadcastMessageRequest(
        CommunicationTarget Target,
        string MessageType,
        object Data);
}
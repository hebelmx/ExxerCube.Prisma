namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Request to remove a connection from a group
    /// </summary>
    /// <param name="Target">The communication target/hub</param>
    /// <param name="ConnectionId">The connection ID to remove</param>
    /// <param name="GroupName">The name of the group</param>
    public sealed record RemoveFromGroupRequest(
        CommunicationTarget Target,
        string ConnectionId,
        string GroupName);
}
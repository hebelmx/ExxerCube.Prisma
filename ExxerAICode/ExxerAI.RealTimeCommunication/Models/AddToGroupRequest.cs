namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Request to add a connection to a group
    /// </summary>
    /// <param name="Target">The communication target/hub</param>
    /// <param name="ConnectionId">The connection ID to add</param>
    /// <param name="GroupName">The name of the group</param>
    public sealed record AddToGroupRequest(
        CommunicationTarget Target,
        string ConnectionId,
        string GroupName);
}
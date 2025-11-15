namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Status update request
    /// </summary>
    /// <param name="Target">The communication target</param>
    /// <param name="EntityType">The type of entity</param>
    /// <param name="EntityId">The ID of the entity</param>
    /// <param name="Status">The new status</param>
    /// <param name="Data">Additional status data</param>
    public sealed record StatusUpdateRequest(
        CommunicationTarget Target,
        string EntityType,
        string EntityId,
        string Status,
        object? Data = null);
}
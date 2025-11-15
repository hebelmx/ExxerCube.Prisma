namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Progress update request
    /// </summary>
    /// <param name="Target">The communication target</param>
    /// <param name="EntityType">The type of entity</param>
    /// <param name="EntityId">The ID of the entity</param>
    /// <param name="Progress">The progress percentage (0-100)</param>
    /// <param name="Message">Optional progress message</param>
    /// <param name="Data">Additional progress data</param>
    public sealed record ProgressUpdateRequest(
        CommunicationTarget Target,
        string EntityType,
        string EntityId,
        int Progress,
        string? Message = null,
        object? Data = null);
}
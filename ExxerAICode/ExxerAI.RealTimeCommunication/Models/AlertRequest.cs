namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Alert request
    /// </summary>
    /// <param name="Target">The communication target</param>
    /// <param name="AlertType">The type of alert</param>
    /// <param name="Severity">The alert severity</param>
    /// <param name="Message">The alert message</param>
    /// <param name="Data">Additional alert data</param>
    public sealed record AlertRequest(
        CommunicationTarget Target,
        string AlertType,
        CommunicationAlertSeverity Severity,
        string Message,
        object? Data = null);
}

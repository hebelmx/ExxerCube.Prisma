namespace ExxerAI.RealTimeCommunication.Abstractions
{
    /// <summary>
    /// Composite port that combines all real-time communication capabilities
    /// </summary>
    public interface ICompleteCommunicationPort : IRealTimeCommunicationPort, IEventBroadcastingPort, INotificationPort
    {
    }
}
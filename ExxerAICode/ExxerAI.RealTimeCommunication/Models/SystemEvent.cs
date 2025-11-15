namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// System-wide event
    /// </summary>
    /// <param name="EventType">The type of system event</param>
    /// <param name="Timestamp">When the event occurred</param>
    /// <param name="Data">The event data</param>
    public sealed record SystemEvent(
        string EventType,
        DateTime Timestamp,
        object Data) : BaseEvent(EventType, Timestamp, Data)
    {
        /// <summary>
        /// Initializes a new instance of the SystemEvent class with current timestamp
        /// </summary>
        /// <param name="eventType">The type of system event</param>
        /// <param name="data">The event data</param>
        public SystemEvent(string eventType, object data) : this(eventType, DateTime.UtcNow, data) { }
    }
}
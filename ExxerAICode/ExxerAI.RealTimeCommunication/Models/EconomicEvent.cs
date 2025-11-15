namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Economic-related event
    /// </summary>
    /// <param name="EventType">The type of economic event</param>
    /// <param name="Timestamp">When the event occurred</param>
    /// <param name="Data">The event data</param>
    public sealed record EconomicEvent(
        string EventType,
        DateTime Timestamp,
        object Data) : BaseEvent(EventType, Timestamp, Data)
    {
        /// <summary>
        /// Initializes a new instance of the EconomicEvent class with current timestamp
        /// </summary>
        /// <param name="eventType">The type of economic event</param>
        /// <param name="data">The event data</param>
        public EconomicEvent(string eventType, object data) : this(eventType, DateTime.UtcNow, data) { }
    }
}
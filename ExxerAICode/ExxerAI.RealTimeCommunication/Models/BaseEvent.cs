namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Base class for all events
    /// </summary>
    public abstract record BaseEvent(
        string EventType,
        DateTime Timestamp,
        object Data)
    {
        /// <summary>
        /// Initializes a new instance of the BaseEvent class with current timestamp
        /// </summary>
        /// <param name="eventType">The type of event</param>
        /// <param name="data">The event data</param>
        protected BaseEvent(string eventType, object data) 
            : this(eventType, DateTime.UtcNow, data) { }
    }
}
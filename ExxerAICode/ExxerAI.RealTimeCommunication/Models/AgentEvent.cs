namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Agent-related event
    /// </summary>
    /// <param name="EventType">The type of agent event</param>
    /// <param name="Timestamp">When the event occurred</param>
    /// <param name="Data">The event data</param>
    /// <param name="AgentId">The ID of the agent</param>
    public sealed record AgentEvent(
        string EventType,
        DateTime Timestamp,
        object Data,
        string AgentId) : BaseEvent(EventType, Timestamp, Data)
    {
        /// <summary>
        /// Initializes a new instance of the AgentEvent class with current timestamp
        /// </summary>
        /// <param name="eventType">The type of agent event</param>
        /// <param name="data">The event data</param>
        /// <param name="agentId">The ID of the agent</param>
        public AgentEvent(string eventType, object data, string agentId) : this(eventType, DateTime.UtcNow, data, agentId) { }
    }
}
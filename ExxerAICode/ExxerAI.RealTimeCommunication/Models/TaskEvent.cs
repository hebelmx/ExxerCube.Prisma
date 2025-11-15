namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Task-related event
    /// </summary>
    /// <param name="EventType">The type of task event</param>
    /// <param name="Timestamp">When the event occurred</param>
    /// <param name="Data">The event data</param>
    /// <param name="TaskId">The ID of the task</param>
    public sealed record TaskEvent(
        string EventType,
        DateTime Timestamp,
        object Data,
        string TaskId) : BaseEvent(EventType, Timestamp, Data)
    {
        /// <summary>
        /// Initializes a new instance of the TaskEvent class with current timestamp
        /// </summary>
        /// <param name="eventType">The type of task event</param>
        /// <param name="data">The event data</param>
        /// <param name="taskId">The ID of the task</param>
        public TaskEvent(string eventType, object data, string taskId) : this(eventType, DateTime.UtcNow, data, taskId) { }
    }
}
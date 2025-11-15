namespace ExxerAI.RealTimeCommunication.Models
{
    /// <summary>
    /// Document-related event
    /// </summary>
    /// <param name="EventType">The type of document event</param>
    /// <param name="Timestamp">When the event occurred</param>
    /// <param name="Data">The event data</param>
    /// <param name="DocumentId">The ID of the document</param>
    public sealed record DocumentEvent(
        string EventType,
        DateTime Timestamp,
        object Data,
        string DocumentId) : BaseEvent(EventType, Timestamp, Data)
    {
        /// <summary>
        /// Initializes a new instance of the DocumentEvent class with current timestamp
        /// </summary>
        /// <param name="eventType">The type of document event</param>
        /// <param name="data">The event data</param>
        /// <param name="documentId">The ID of the document</param>
        public DocumentEvent(string eventType, object data, string documentId) : this(eventType, DateTime.UtcNow, data, documentId) { }
    }
}
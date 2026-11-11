namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Document processing completed successfully (80%+ auto-processed goal).
/// </summary>
public record DocumentProcessingCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the completed file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the total processing time from download to completion.
    /// </summary>
    public TimeSpan TotalProcessingTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the document was auto-processed (true) or flagged for review (false).
    /// </summary>
    public bool AutoProcessed { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessingCompletedEvent"/> class.
    /// </summary>
    public DocumentProcessingCompletedEvent()
    {
        EventType = nameof(DocumentProcessingCompletedEvent);
    }
}
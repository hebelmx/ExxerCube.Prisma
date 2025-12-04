namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Document flagged for manual review (defensive intelligence - not rejection).
/// </summary>
public record DocumentFlaggedForReviewEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the flagged file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the list of reasons why the document was flagged.
    /// </summary>
    public List<string> Reasons { get; init; } = new();

    /// <summary>
    /// Gets the review priority level (Low, Normal, High, Urgent).
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentFlaggedForReviewEvent"/> class.
    /// </summary>
    public DocumentFlaggedForReviewEvent()
    {
        EventType = nameof(DocumentFlaggedForReviewEvent);
    }
}
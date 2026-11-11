namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Document rejected due to unacceptable quality (defensive flagging - not crash).
/// </summary>
public record QualityRejectedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the rejected file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the quality score that caused rejection.
    /// </summary>
    public decimal Score { get; init; }

    /// <summary>
    /// Gets the reason for quality rejection.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityRejectedEvent"/> class.
    /// </summary>
    public QualityRejectedEvent()
    {
        EventType = nameof(QualityRejectedEvent);
    }
}
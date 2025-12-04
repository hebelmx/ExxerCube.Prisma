namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Classification completed with confidence and warnings.
/// </summary>
public record ClassificationCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the classified file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the requirement type ID from classification.
    /// </summary>
    public int RequirementTypeId { get; init; }

    /// <summary>
    /// Gets the requirement type name (e.g., Aseguramiento, Desbloqueo).
    /// </summary>
    public string RequirementTypeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the classification confidence score (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the list of warnings generated during classification.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether manual review is required.
    /// </summary>
    public bool RequiresManualReview { get; init; }

    /// <summary>
    /// Gets the relation type (NewRequirement, Recordatorio, Alcance, Precision).
    /// </summary>
    public string RelationType { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassificationCompletedEvent"/> class.
    /// </summary>
    public ClassificationCompletedEvent()
    {
        EventType = nameof(ClassificationCompletedEvent);
    }
}
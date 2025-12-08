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
    /// The name of the classified file.
    /// </summary>
    public string FileName { get; } = string.Empty;
    /// <summary>
    /// The classification type assigned to the document.
    /// </summary>
    public string ClassificationType { get; } = string.Empty;
    /// <summary>
    /// The confidence score of the classification (0.0 to 1.0).
    /// </summary>
    public double ConfidenceScore { get; }
    /// <summary>
    /// The correlation ID for tracking related events.
    /// </summary>
    public DateTimeOffset Timestamp1 { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassificationCompletedEvent"/> class.
    /// </summary>
    public ClassificationCompletedEvent()
    {
        EventType = nameof(ClassificationCompletedEvent);
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassificationCompletedEvent"/> class.
    /// </summary>
    /// <param name="FileId"></param>
    /// <param name="FileName"></param>
    /// <param name="ClassificationType"></param>
    /// <param name="ConfidenceScore"></param>
    /// <param name="CorrelationId"></param>
    /// <param name="Timestamp"></param>
    public ClassificationCompletedEvent(Guid FileId, string FileName, string ClassificationType, double ConfidenceScore, Guid CorrelationId, DateTimeOffset Timestamp)
    {
        this.FileId = FileId;
        this.FileName = FileName;
        this.ClassificationType = ClassificationType;
        this.ConfidenceScore = ConfidenceScore;
        this.CorrelationId = CorrelationId;
        Timestamp1 = Timestamp;
    }
}
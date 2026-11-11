namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Conflict detected between XML and OCR data during reconciliation.
/// </summary>
public record ConflictDetectedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the file with conflict.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the name of the field where conflict was detected.
    /// </summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value from XML metadata.
    /// </summary>
    public string XmlValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value from OCR extraction.
    /// </summary>
    public string OcrValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets the similarity score between XML and OCR values (0-1).
    /// </summary>
    public decimal SimilarityScore { get; init; }

    /// <summary>
    /// Gets the conflict severity level (Low, Medium, High).
    /// </summary>
    public string ConflictSeverity { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictDetectedEvent"/> class.
    /// </summary>
    public ConflictDetectedEvent()
    {
        EventType = nameof(ConflictDetectedEvent);
    }
}
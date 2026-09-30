namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Quality analysis completion event payload.
/// </summary>
public sealed record QualityCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the document file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the name of the analyzed file.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the quality score (0.0 to 1.0).
    /// </summary>
    public double QualityScore { get; init; }

    /// <summary>
    /// Gets whether quality meets acceptance criteria.
    /// </summary>
    public bool IsAcceptable { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityCompletedEvent"/> class.
    /// </summary>
    public QualityCompletedEvent()
    {
        EventType = nameof(QualityCompletedEvent);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityCompletedEvent"/> class with specified details.
    /// </summary>
    /// <param name="FileId">Unique identifier for the document file.</param>
    /// <param name="FileName">Name of the analyzed file.</param>
    /// <param name="QualityScore">Quality score (0.0 to 1.0).</param>
    /// <param name="IsAcceptable">Whether quality meets acceptance criteria.</param>
    /// <param name="CorrelationId">End-to-end tracing identifier.</param>
    /// <param name="Timestamp">UTC timestamp when quality analysis completed.</param>
    public QualityCompletedEvent(
        Guid FileId,
        string FileName,
        double QualityScore,
        bool IsAcceptable,
        Guid CorrelationId,
        DateTimeOffset Timestamp)
        : this()
    {
        this.FileId = FileId;
        this.FileName = FileName;
        this.QualityScore = QualityScore;
        this.IsAcceptable = IsAcceptable;
        this.CorrelationId = CorrelationId;
        this.Timestamp = Timestamp.UtcDateTime;
    }
}
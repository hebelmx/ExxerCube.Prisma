namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Full processing completion event payload.
/// </summary>
public sealed record ProcessingCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the document file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the name of the processed file.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the processing status ("Success", "Failed", "PartialSuccess").
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total time taken to process the document.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingCompletedEvent"/> class.
    /// </summary>
    public ProcessingCompletedEvent()
    {
        EventType = nameof(ProcessingCompletedEvent);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingCompletedEvent"/> class with specified details.
    /// </summary>
    /// <param name="FileId">Unique identifier for the document file.</param>
    /// <param name="FileName">Name of the processed file.</param>
    /// <param name="Status">Processing status ("Success", "Failed", "PartialSuccess").</param>
    /// <param name="ProcessingDuration">Total time taken to process the document.</param>
    /// <param name="CorrelationId">End-to-end tracing identifier.</param>
    /// <param name="Timestamp">UTC timestamp when processing completed.</param>
    public ProcessingCompletedEvent(
        Guid FileId,
        string FileName,
        string Status,
        TimeSpan ProcessingDuration,
        Guid CorrelationId,
        DateTimeOffset Timestamp)
        : this()
    {
        this.FileId = FileId;
        this.FileName = FileName;
        this.Status = Status;
        this.ProcessingDuration = ProcessingDuration;
        this.CorrelationId = CorrelationId;
        this.Timestamp = Timestamp.UtcDateTime;
    }
}
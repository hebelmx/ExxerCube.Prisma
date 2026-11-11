namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Represents a processing event for real-time monitoring.
/// </summary>
public record ProcessingEvent : DomainEvent
{
    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the processing time in seconds.
    /// </summary>
    public double ProcessingTimeSeconds { get; init; }

    /// <summary>
    /// Gets whether the processing was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the OCR confidence score.
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Gets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingEvent"/> class.
    /// </summary>
    public ProcessingEvent()
    {
        EventType = nameof(ProcessingEvent);
    }
}
namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Processing error occurred (logged but system continues - defensive intelligence).
/// </summary>
public record ProcessingErrorEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the file where error occurred (null if system-level error).
    /// </summary>
    public Guid? FileId { get; init; }

    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stack trace for debugging purposes.
    /// </summary>
    public string StackTrace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the component where error occurred (OCR, Classification, Storage, etc.).
    /// </summary>
    public string Component { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingErrorEvent"/> class.
    /// </summary>
    public ProcessingErrorEvent()
    {
        EventType = nameof(ProcessingErrorEvent);
    }
}
namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Adaptive export completed successfully (Expediente → target format).
/// </summary>
public record ExportCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the source file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the destination path where file was exported.
    /// </summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>
    /// Gets the export format used (PDF, Excel, Word, etc.).
    /// </summary>
    public string Format { get; init; } = string.Empty;

    /// <summary>
    /// Gets the size of the exported file in bytes.
    /// </summary>
    public long ExportedSizeBytes { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportCompletedEvent"/> class.
    /// </summary>
    public ExportCompletedEvent()
    {
        EventType = nameof(ExportCompletedEvent);
    }
}
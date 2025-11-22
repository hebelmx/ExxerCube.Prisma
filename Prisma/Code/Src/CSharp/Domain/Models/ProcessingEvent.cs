namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents a processing event for real-time monitoring.
/// </summary>
public class ProcessingEvent
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processing time in seconds.
    /// </summary>
    public double ProcessingTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets whether the processing was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence score.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
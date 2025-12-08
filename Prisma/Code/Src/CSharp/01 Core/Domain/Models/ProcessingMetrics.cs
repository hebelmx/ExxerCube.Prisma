namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents processing metrics for a single document.
/// </summary>
public class ProcessingMetrics
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source path of the document.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processing time in seconds.
    /// </summary>
    public double ProcessingTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence score.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the number of extracted fields.
    /// </summary>
    public int ExtractedFieldCount { get; set; }

    /// <summary>
    /// Gets or sets whether the processing was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets when the processing was completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }
}
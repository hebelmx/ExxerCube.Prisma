namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents current processing statistics.
/// </summary>
public class ProcessingStatistics
{
    /// <summary>
    /// Gets or sets the total number of documents processed.
    /// </summary>
    public int TotalDocumentsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of successful documents.
    /// </summary>
    public int SuccessfulDocuments { get; set; }

    /// <summary>
    /// Gets or sets the number of failed documents.
    /// </summary>
    public int FailedDocuments { get; set; }

    /// <summary>
    /// Gets or sets the average processing time in seconds.
    /// </summary>
    public double AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the average OCR confidence score.
    /// </summary>
    public float AverageConfidence { get; set; }

    /// <summary>
    /// Gets or sets the average number of extracted fields.
    /// </summary>
    public double AverageExtractedFields { get; set; }

    /// <summary>
    /// Gets or sets the success rate as a percentage.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets when the statistics were last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
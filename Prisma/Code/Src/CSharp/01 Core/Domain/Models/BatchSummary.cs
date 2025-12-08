namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Summary statistics for a completed batch processing operation.
/// </summary>
public class BatchSummary
{
    /// <summary>
    /// Gets or sets the number of successfully processed documents.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of documents in the batch.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the average OCR confidence across all documents.
    /// </summary>
    public float AvgOcrConfidence { get; set; }

    /// <summary>
    /// Gets or sets the average match rate (percentage) across all documents.
    /// </summary>
    public float AvgMatchRate { get; set; }

    /// <summary>
    /// Gets or sets the total processing time in milliseconds for the entire batch.
    /// </summary>
    public long TotalTimeMs { get; set; }
}

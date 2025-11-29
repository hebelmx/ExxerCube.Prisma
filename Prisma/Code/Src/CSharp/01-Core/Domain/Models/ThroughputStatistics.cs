namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents throughput statistics for a time period.
/// </summary>
public class ThroughputStatistics
{
    /// <summary>
    /// Gets or sets the time period analyzed.
    /// </summary>
    public TimeSpan Period { get; set; }

    /// <summary>
    /// Gets or sets the total number of documents processed.
    /// </summary>
    public int TotalDocuments { get; set; }

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
    /// Gets or sets the documents processed per hour.
    /// </summary>
    public double DocumentsPerHour { get; set; }

    /// <summary>
    /// Gets or sets the success rate as a percentage.
    /// </summary>
    public double SuccessRate { get; set; }
}
namespace ExxerCube.Prisma.Web.UI.Models;

/// <summary>
/// Model for dashboard metrics.
/// </summary>
public class DashboardMetrics
{
    /// <summary>
    /// Gets or sets the total number of documents processed.
    /// </summary>
    public int TotalDocumentsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the success rate percentage.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average processing time in seconds.
    /// </summary>
    public double AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the average confidence score.
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// Gets or sets the number of documents in the processing queue.
    /// </summary>
    public int DocumentsInQueue { get; set; }

    /// <summary>
    /// Gets or sets the list of recent processing errors.
    /// </summary>
    public List<string> RecentErrors { get; set; } = new();
}
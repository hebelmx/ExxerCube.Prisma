namespace ExxerCube.Prisma.Web.UI.Models;

/// <summary>
/// Response model for document processing operations.
/// </summary>
public class ProcessingResponse
{
    /// <summary>
    /// Gets or sets the job identifier.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processing status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for processing status queries.
/// </summary>
public class ProcessingStatusResponse : ProcessingResponse
{
    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }
}

/// <summary>
/// Response model for processing results.
/// </summary>
public class ProcessingResultResponse : ProcessingResponse
{
    /// <summary>
    /// Gets or sets the processing results data.
    /// </summary>
    public object? Data { get; set; }
}

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

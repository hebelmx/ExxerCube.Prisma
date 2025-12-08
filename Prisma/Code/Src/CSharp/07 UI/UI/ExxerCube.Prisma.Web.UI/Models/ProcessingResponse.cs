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
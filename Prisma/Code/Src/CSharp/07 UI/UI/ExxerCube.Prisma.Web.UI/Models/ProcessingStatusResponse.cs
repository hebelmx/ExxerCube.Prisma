namespace ExxerCube.Prisma.Web.UI.Models;

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
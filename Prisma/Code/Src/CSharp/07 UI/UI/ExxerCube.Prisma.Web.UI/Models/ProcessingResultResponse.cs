namespace ExxerCube.Prisma.Web.UI.Models;

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
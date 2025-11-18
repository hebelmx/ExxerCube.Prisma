namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents the result of a health check for a specific component.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Gets or sets the name of the component being checked.
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the health status of the component.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a descriptive message about the health status.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the health check was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets additional details about the health check.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}
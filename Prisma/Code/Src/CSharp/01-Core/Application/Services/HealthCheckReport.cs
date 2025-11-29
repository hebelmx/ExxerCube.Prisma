namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents a comprehensive health check report for the entire system.
/// </summary>
public class HealthCheckReport
{
    /// <summary>
    /// Gets or sets the overall health status of the system.
    /// </summary>
    public HealthStatus OverallHealth { get; set; }

    /// <summary>
    /// Gets or sets the list of individual health check results.
    /// </summary>
    public List<HealthCheckResult> HealthChecks { get; set; } = new();

    /// <summary>
    /// Gets or sets when the health check report was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the current processing statistics.
    /// </summary>
    public ProcessingStatistics? ProcessingStatistics { get; set; }
}
namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents performance validation results.
/// </summary>
public class PerformanceValidation
{
    /// <summary>
    /// Gets or sets whether the system is meeting performance requirements.
    /// </summary>
    public bool IsMeetingRequirements { get; set; }

    /// <summary>
    /// Gets or sets the validation result messages.
    /// </summary>
    public List<string> ValidationResults { get; set; } = new();

    /// <summary>
    /// Gets or sets the 1-hour throughput statistics.
    /// </summary>
    public ThroughputStatistics? Throughput1Hour { get; set; }

    /// <summary>
    /// Gets or sets the 5-minute throughput statistics.
    /// </summary>
    public ThroughputStatistics? Throughput5Minutes { get; set; }

    /// <summary>
    /// Gets or sets the current processing statistics.
    /// </summary>
    public ProcessingStatistics? CurrentStatistics { get; set; }
}
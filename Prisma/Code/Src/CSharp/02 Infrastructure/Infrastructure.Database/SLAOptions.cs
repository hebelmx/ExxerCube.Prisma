namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Configuration options for SLA tracking and escalation.
/// </summary>
public class SLAOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SLA";

    /// <summary>
    /// Gets or sets the critical threshold (default: 4 hours).
    /// </summary>
    public TimeSpan CriticalThreshold { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Gets or sets the warning threshold (default: 24 hours).
    /// </summary>
    public TimeSpan WarningThreshold { get; set; } = TimeSpan.FromHours(24);
}


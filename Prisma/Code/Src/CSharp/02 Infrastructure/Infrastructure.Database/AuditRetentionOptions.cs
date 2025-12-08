namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Configuration options for audit retention background service.
/// </summary>
public class AuditRetentionOptions
{
    /// <summary>
    /// Gets or sets the configuration section name.
    /// </summary>
    public const string SectionName = "AuditRetention";

    /// <summary>
    /// Gets or sets the interval in hours between retention policy enforcement cycles (default: 24 hours).
    /// </summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the batch size for processing records during deletion (default: 1000).
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the retry delay in hours if an error occurs (default: 1 hour).
    /// </summary>
    public int RetryDelayHours { get; set; } = 1;
}


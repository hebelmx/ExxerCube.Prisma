namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Configuration options for SLA background update service.
/// </summary>
public class SLAUpdateOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SLA:BackgroundUpdate";

    /// <summary>
    /// Gets or sets the update interval in seconds (default: 60 seconds).
    /// </summary>
    public int UpdateIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the batch size for processing SLA updates (default: 100).
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of retries on failure (default: 3).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the retry delay in seconds (default: 5 seconds).
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;
}


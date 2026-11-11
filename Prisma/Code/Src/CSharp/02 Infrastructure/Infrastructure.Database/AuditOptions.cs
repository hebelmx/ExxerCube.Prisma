namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Configuration options for audit logging and retention policies.
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// Gets or sets the configuration section name.
    /// </summary>
    public const string SectionName = "Audit";

    /// <summary>
    /// Gets or sets the retention period in years (default: 7 years per regulatory requirements).
    /// </summary>
    public int RetentionYears { get; set; } = 7;

    /// <summary>
    /// Gets or sets the number of years after which to archive audit records (default: 1 year).
    /// </summary>
    public int ArchiveAfterYears { get; set; } = 1;

    /// <summary>
    /// Gets or sets the archive location path (optional, null if archiving is disabled).
    /// </summary>
    public string? ArchiveLocation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically delete audit records after retention period (default: true).
    /// </summary>
    public bool AutoDeleteAfterRetention { get; set; } = true;
}


namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Result of demo data cleanup operation.
/// </summary>
public record DemoCleanupResult
{
    /// <summary>
    /// Whether cleanup succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of audit records deleted.
    /// </summary>
    public int AuditRecordsDeleted { get; set; }

    /// <summary>
    /// Number of file metadata records deleted.
    /// </summary>
    public int FileMetadataDeleted { get; set; }

    /// <summary>
    /// When cleanup started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When cleanup ended.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Duration of cleanup operation.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Success or error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error message if cleanup failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
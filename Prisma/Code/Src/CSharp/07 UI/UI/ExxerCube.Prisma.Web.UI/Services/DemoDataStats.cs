namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Statistics about current demo data in database.
/// </summary>
public record DemoDataStats
{
    /// <summary>
    /// Number of audit records (events).
    /// </summary>
    public int AuditRecordsCount { get; init; }

    /// <summary>
    /// Number of file metadata records.
    /// </summary>
    public int FileMetadataCount { get; init; }

    /// <summary>
    /// When these statistics were captured.
    /// </summary>
    public DateTime Timestamp { get; init; }
}
namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Entities;

/// <summary>
/// EF Core persistence entity that stores the serialised <c>UnifiedMetadataRecord</c> JSON payload
/// keyed by the originating file identifier.  The payload is stored as a single <c>nvarchar(max)</c>
/// column so that the rich value-object graph (including SmartEnum members) can be round-tripped
/// without spreading it across many relational columns.
/// </summary>
public class PersistedUnifiedMetadata
{
    /// <summary>
    /// Gets or sets the file identifier (primary key).  Matches <c>FileMetadata.FileId</c> semantics
    /// but carries no FK constraint so the record can be written before or after the file row exists.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON-serialised <c>UnifiedMetadataRecord</c> payload.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last upsert operation.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

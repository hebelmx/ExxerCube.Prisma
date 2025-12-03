namespace Prisma.Orion.Ingestion;

/// <summary>
/// Result of a successful document ingestion operation.
/// </summary>
/// <param name="FileId">Unique identifier assigned to the ingested file.</param>
/// <param name="FileName">Name of the ingested file.</param>
/// <param name="Hash">SHA-256 hash of the file contents.</param>
/// <param name="StoredPath">File system path where the document was stored (YYYY/MM/DD partitioned).</param>
/// <param name="FileSizeBytes">Size of the file in bytes.</param>
/// <param name="CorrelationId">End-to-end correlation identifier for tracing.</param>
/// <param name="WasDuplicate">True if the document was a duplicate (idempotent skip), false if newly ingested.</param>
public sealed record IngestionResult(
    Guid FileId,
    string FileName,
    string Hash,
    string StoredPath,
    long FileSizeBytes,
    Guid CorrelationId,
    bool WasDuplicate);

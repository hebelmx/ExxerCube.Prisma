namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Ingestion manifest record for tracking downloaded documents.
/// </summary>
/// <param name="FileId">Unique file identifier (correlation).</param>
/// <param name="FileName">Original file name.</param>
/// <param name="SourceUrl">Source URL from SIARA.</param>
/// <param name="ContentHash">SHA256 hash of file content for idempotency.</param>
/// <param name="FileSizeBytes">File size in bytes.</param>
/// <param name="StoredPath">Partitioned storage path (year/month/day/filename).</param>
/// <param name="CorrelationId">Correlation ID for end-to-end tracing.</param>
/// <param name="DownloadedAt">Timestamp when file was downloaded.</param>
/// <param name="ActorId">
/// Trustworthy actor that authorized the download, recorded for per-document non-repudiation (ADR-010 P2).
/// Empty only on the legacy/stub path that carries no provenance.
/// </param>
/// <param name="SessionId">Correlation id of the SIARA session used to pull the document (audit trail).</param>
public sealed record IngestionManifestEntry(
    Guid FileId,
    string FileName,
    string SourceUrl,
    string ContentHash,
    long FileSizeBytes,
    string StoredPath,
    Guid CorrelationId,
    DateTimeOffset DownloadedAt,
    string ActorId = "",
    string SessionId = "");
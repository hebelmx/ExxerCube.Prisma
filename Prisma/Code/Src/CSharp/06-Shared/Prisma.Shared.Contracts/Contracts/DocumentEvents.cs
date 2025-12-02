namespace Prisma.Shared.Contracts;

/// <summary>
/// Contract events shared across Orion (ingestion), Athena (processing), and HMI.
/// </summary>
public static class DocumentEvents
{
    public const string DocumentDownloaded = nameof(DocumentDownloaded);
    public const string QualityCompleted = nameof(QualityCompleted);
    public const string OcrCompleted = nameof(OcrCompleted);
    public const string ClassificationCompleted = nameof(ClassificationCompleted);
    public const string ProcessingCompleted = nameof(ProcessingCompleted);
}

/// <summary>Download event payload.</summary>
public sealed record DocumentDownloadedEvent(
    Guid FileId,
    string FileName,
    string Source,
    long FileSizeBytes,
    string Path,
    string JournalPath,
    Guid CorrelationId,
    DateTimeOffset Timestamp);

/// <summary>Heartbeat payload for workers.</summary>
public sealed record WorkerHeartbeat(
    string WorkerName,
    DateTimeOffset Timestamp,
    string Status,
    string? Details = null);

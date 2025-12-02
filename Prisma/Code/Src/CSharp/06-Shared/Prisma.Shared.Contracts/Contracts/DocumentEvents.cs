using System;

namespace Prisma.Shared.Contracts;

/// <summary>
/// Contract events shared across Orion (ingestion), Athena (processing), and HMI.
/// </summary>
public static class DocumentEvents
{
    /// <summary>Event name for document download completion.</summary>
    public const string DocumentDownloaded = nameof(DocumentDownloaded);

    /// <summary>Event name for quality analysis completion.</summary>
    public const string QualityCompleted = nameof(QualityCompleted);

    /// <summary>Event name for OCR processing completion.</summary>
    public const string OcrCompleted = nameof(OcrCompleted);

    /// <summary>Event name for classification completion.</summary>
    public const string ClassificationCompleted = nameof(ClassificationCompleted);

    /// <summary>Event name for full processing pipeline completion.</summary>
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

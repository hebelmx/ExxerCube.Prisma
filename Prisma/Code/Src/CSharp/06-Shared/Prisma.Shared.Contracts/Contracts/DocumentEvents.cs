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

/// <summary>
/// Classification completion event payload.
/// </summary>
/// <param name="FileId">Unique identifier for the document file.</param>
/// <param name="FileName">Name of the classified file.</param>
/// <param name="ClassificationType">Classification result (e.g., "Invoice", "Contract", "Report").</param>
/// <param name="ConfidenceScore">ML model confidence score (0.0 to 1.0).</param>
/// <param name="CorrelationId">End-to-end tracing identifier.</param>
/// <param name="Timestamp">UTC timestamp when classification completed.</param>
public sealed record ClassificationCompletedEvent(
    Guid FileId,
    string FileName,
    string ClassificationType,
    double ConfidenceScore,
    Guid CorrelationId,
    DateTimeOffset Timestamp);

/// <summary>
/// Full processing completion event payload.
/// </summary>
/// <param name="FileId">Unique identifier for the document file.</param>
/// <param name="FileName">Name of the processed file.</param>
/// <param name="Status">Processing status ("Success", "Failed", "PartialSuccess").</param>
/// <param name="ProcessingDuration">Total time taken to process the document.</param>
/// <param name="CorrelationId">End-to-end tracing identifier.</param>
/// <param name="Timestamp">UTC timestamp when processing completed.</param>
public sealed record ProcessingCompletedEvent(
    Guid FileId,
    string FileName,
    string Status,
    TimeSpan ProcessingDuration,
    Guid CorrelationId,
    DateTimeOffset Timestamp);

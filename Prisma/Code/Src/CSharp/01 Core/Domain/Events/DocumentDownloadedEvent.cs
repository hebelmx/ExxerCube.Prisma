// <copyright file="ProcessingEvents.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Document downloaded from SIARA, email, or manual upload.
/// </summary>
public record DocumentDownloadedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the downloaded file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the name of the downloaded file.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source of the download (SIARA, Email, Manual).
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the detected file format.
    /// </summary>
    public FileFormat Format { get; init; } = FileFormat.Unknown;

    /// <summary>
    /// Gets the URL from which the document was downloaded.
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;
    /// <summary>
    /// Gets the expected correlation ID for tracking.
    /// </summary>
    public Guid ExpectedCorrelationId { get; }
    /// <summary>
    /// Gets the timestamp when the document was downloaded.
    /// </summary>
    public DateTimeOffset Timestamp1 { get; }
    /// <summary>
    /// Gets the storage-relative path under which the document was stored (relative to the shared storage
    /// base both the Downloader and Extractor processes mount — e.g. <c>2026/06/12/{fileId}.pdf</c>).
    /// The Downloader (Orion) stamps this; the Extractor (Athena) resolves it against its own configured
    /// storage base to obtain a locally-loadable absolute path (MVP-PATH 1.3 shared storage, ADR-011).
    /// Empty for in-process / single-service flows.
    /// </summary>
    public string Path { get; init; } = string.Empty;
    /// <summary>
    /// Gets the journal path for audit logging.
    /// </summary>
    public string JournalPath { get; } = string.Empty;
    /// <summary>
    /// Gets the timestamp when the document was processed.
    /// </summary>
    public DateTimeOffset Timestamp2 { get; }

    /// <summary>
    /// Gets the short-lived process clearance token minted by the sender (MVP-PATH 1.5, A5).
    /// The receiving forwarder validates this token before forwarding the event into the local
    /// pipeline. Empty for in-process / single-service flows.
    /// </summary>
    public string ClearanceToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the companion case files downloaded alongside the primary file (MVP-PATH 2.1).
    /// Each entry carries the storage-relative path and format of one companion file so the Extractor
    /// process can locate and load all sources for the case without a second discovery round.
    /// Defaults to an empty list — all existing per-file producers continue to work unchanged.
    /// </summary>
    public IReadOnlyList<CaseFileReference> CaseFiles { get; init; } = Array.Empty<CaseFileReference>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentDownloadedEvent"/> class.
    /// </summary>
    public DocumentDownloadedEvent()
    {
        EventType = nameof(DocumentDownloadedEvent);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentDownloadedEvent"/> class with specified details.
    /// </summary>
    /// <param name="FileId"></param>
    /// <param name="FileName"></param>
    /// <param name="Source"></param>
    /// <param name="FileSizeBytes"></param>
    /// <param name="Path"></param>
    /// <param name="JournalPath"></param>
    /// <param name="CorrelationId"></param>
    /// <param name="Timestamp"></param>
    public DocumentDownloadedEvent(Guid FileId, string FileName, string Source, long FileSizeBytes, string Path, string JournalPath, Guid CorrelationId, DateTimeOffset Timestamp)
    {
        this.FileId = FileId;
        this.FileName = FileName;
        this.Source = Source;
        this.FileSizeBytes = FileSizeBytes;
        this.Path = Path;
        this.JournalPath = JournalPath;
        this.CorrelationId = CorrelationId;
        Timestamp2 = Timestamp;
    }
}
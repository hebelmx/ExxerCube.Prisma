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
    /// Gets the file system path where the document is stored.
    /// </summary>
    public string Path { get; } = string.Empty;
    /// <summary>
    /// Gets the journal path for audit logging.
    /// </summary>
    public string JournalPath { get; } = string.Empty;
    /// <summary>
    /// Gets the timestamp when the document was processed.
    /// </summary>
    public DateTimeOffset Timestamp2 { get; }

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
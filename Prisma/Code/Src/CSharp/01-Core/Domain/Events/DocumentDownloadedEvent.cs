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
    /// Initializes a new instance of the <see cref="DocumentDownloadedEvent"/> class.
    /// </summary>
    public DocumentDownloadedEvent()
    {
        EventType = nameof(DocumentDownloadedEvent);
    }
}
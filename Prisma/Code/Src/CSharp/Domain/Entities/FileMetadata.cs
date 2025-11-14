using System;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents metadata for a downloaded regulatory document.
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for the file.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original filename from download source.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path where file is saved.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source URL where file was downloaded from (nullable).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the file was downloaded.
    /// </summary>
    public DateTime DownloadTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash for duplicate detection.
    /// </summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the file format (PDF, XML, DOCX, ZIP).
    /// </summary>
    public FileFormat Format { get; set; }
}


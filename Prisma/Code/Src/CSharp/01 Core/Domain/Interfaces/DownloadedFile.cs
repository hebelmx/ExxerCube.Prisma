namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents a downloaded file with its content.
/// </summary>
public class DownloadedFile
{
    /// <summary>
    /// Gets or sets the URL where the file was downloaded from.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the downloaded file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file format (PDF, XML, DOCX, ZIP).
    /// </summary>
    public FileFormat Format { get; set; } = FileFormat.Unknown;

    /// <summary>
    /// Gets or sets the file content as a byte array.
    /// </summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize => Content.Length;
}

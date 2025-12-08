namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents a downloadable file identified on a webpage.
/// </summary>
public class DownloadableFile
{
    /// <summary>
    /// Gets or sets the URL of the downloadable file.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the downloadable file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file format (PDF, XML, DOCX, ZIP).
    /// </summary>
    public FileFormat Format { get; set; } = FileFormat.Unknown;
}

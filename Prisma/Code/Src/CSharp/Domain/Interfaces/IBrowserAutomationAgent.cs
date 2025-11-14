using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the browser automation agent for navigating websites and downloading files.
/// </summary>
public interface IBrowserAutomationAgent
{
    /// <summary>
    /// Launches a browser session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> LaunchBrowserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Navigates to the specified URL.
    /// </summary>
    /// <param name="url">The URL to navigate to.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> NavigateToAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies downloadable files matching the specified patterns.
    /// </summary>
    /// <param name="filePatterns">Array of file patterns to match (e.g., "*.pdf", "*.xml", "*.docx").</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of downloadable file URLs or an error.</returns>
    Task<Result<List<DownloadableFile>>> IdentifyDownloadableFilesAsync(
        string[] filePatterns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from the specified URL.
    /// </summary>
    /// <param name="fileUrl">The URL of the file to download.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the downloaded file content or an error.</returns>
    Task<Result<DownloadedFile>> DownloadFileAsync(
        string fileUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the browser session cleanly.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> CloseBrowserAsync(CancellationToken cancellationToken = default);
}

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
    public FileFormat Format { get; set; }
}

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
    public FileFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the file content as a byte array.
    /// </summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize => Content.Length;
}

/// <summary>
/// Represents the format of a file.
/// </summary>
public enum FileFormat
{
    /// <summary>
    /// PDF format.
    /// </summary>
    Pdf,

    /// <summary>
    /// XML format.
    /// </summary>
    Xml,

    /// <summary>
    /// DOCX format.
    /// </summary>
    Docx,

    /// <summary>
    /// ZIP format.
    /// </summary>
    Zip
}


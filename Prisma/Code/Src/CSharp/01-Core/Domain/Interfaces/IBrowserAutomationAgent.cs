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

    /// <summary>
    /// Fills a text input field with the specified value.
    /// </summary>
    /// <param name="selector">CSS selector or text to locate the input field.</param>
    /// <param name="value">The value to fill into the input field.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> FillInputAsync(string selector, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clicks an element on the page.
    /// </summary>
    /// <param name="selector">CSS selector or text to locate the element.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ClickElementAsync(string selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for an element to be visible on the page.
    /// </summary>
    /// <param name="selector">CSS selector to locate the element.</param>
    /// <param name="timeoutMs">Timeout in milliseconds. If null, uses default page timeout.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default);
}
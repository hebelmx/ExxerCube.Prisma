using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation;

/// <summary>
/// Playwright-based implementation of browser automation agent for downloading regulatory documents.
/// </summary>
public class PlaywrightBrowserAutomationAdapter : IBrowserAutomationAgent
{
    private readonly ILogger<PlaywrightBrowserAutomationAdapter> _logger;
    private readonly BrowserAutomationOptions _options;
    private IBrowser? _browser;
    private IPage? _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaywrightBrowserAutomationAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The browser automation options.</param>
    public PlaywrightBrowserAutomationAdapter(
        ILogger<PlaywrightBrowserAutomationAdapter> logger,
        IOptions<BrowserAutomationOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result> LaunchBrowserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Launching browser session");

            var playwright = await Playwright.CreateAsync();
            _browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _options.Headless,
                Timeout = _options.BrowserLaunchTimeoutMs
            });

            _page = await _browser.NewPageAsync();
            _page.SetDefaultTimeout(_options.PageTimeoutMs);

            _logger.LogInformation("Browser session launched successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch browser session");
            return Result.WithFailure($"Failed to launch browser: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> NavigateToAsync(string url, CancellationToken cancellationToken = default)
    {
        if (_page == null)
        {
            return Result.WithFailure("Browser session not launched. Call LaunchBrowserAsync first.");
        }

        try
        {
            _logger.LogInformation("Navigating to URL: {Url}", url);
            await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            _logger.LogInformation("Successfully navigated to URL: {Url}", url);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to URL: {Url}", url);
            return Result.WithFailure($"Failed to navigate to {url}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<DownloadableFile>>> IdentifyDownloadableFilesAsync(
        string[] filePatterns,
        CancellationToken cancellationToken = default)
    {
        if (_page == null)
        {
            return Result<List<DownloadableFile>>.WithFailure("Browser session not launched. Call LaunchBrowserAsync first.");
        }

        try
        {
            _logger.LogInformation("Identifying downloadable files matching patterns: {Patterns}", string.Join(", ", filePatterns));

            var downloadableFiles = new List<DownloadableFile>();

            // Find all links on the page
            var links = await _page.Locator("a[href]").AllAsync();

            foreach (var link in links)
            {
                var href = await link.GetAttributeAsync("href");
                if (string.IsNullOrEmpty(href))
                    continue;

                var absoluteUrl = new Uri(new Uri(_page.Url), href).ToString();
                var fileName = href.Split('/').LastOrDefault() ?? string.Empty;

                // Check if file matches any pattern
                foreach (var pattern in filePatterns)
                {
                    if (MatchesPattern(fileName, pattern))
                    {
                        var format = DetermineFileFormat(fileName);
                        downloadableFiles.Add(new DownloadableFile
                        {
                            Url = absoluteUrl,
                            FileName = fileName,
                            Format = format
                        });
                        break;
                    }
                }
            }

            _logger.LogInformation("Found {Count} downloadable files", downloadableFiles.Count);
            return Result<List<DownloadableFile>>.Success(downloadableFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to identify downloadable files");
            return Result<List<DownloadableFile>>.WithFailure($"Failed to identify downloadable files: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<DownloadedFile>> DownloadFileAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        if (_page == null)
        {
            return Result<DownloadedFile>.WithFailure("Browser session not launched. Call LaunchBrowserAsync first.");
        }

        try
        {
            _logger.LogInformation("Downloading file from URL: {Url}", fileUrl);

            var response = await _page.GotoAsync(fileUrl);
            if (response == null)
            {
                return Result<DownloadedFile>.WithFailure($"Failed to download file from {fileUrl}: No response received");
            }

            var content = await response.BodyAsync();
            var fileName = fileUrl.Split('/').LastOrDefault() ?? "download";
            var format = DetermineFileFormat(fileName);

            var downloadedFile = new DownloadedFile
            {
                Url = fileUrl,
                FileName = fileName,
                Format = format,
                Content = content
            };

            _logger.LogInformation("Successfully downloaded file: {FileName} ({Size} bytes)", fileName, content.Length);
            return Result<DownloadedFile>.Success(downloadedFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from URL: {Url}", fileUrl);
            return Result<DownloadedFile>.WithFailure($"Failed to download file from {fileUrl}: {ex.Message}", default, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> CloseBrowserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_page != null)
            {
                await _page.CloseAsync();
                _page = null;
            }

            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _logger.LogInformation("Browser session closed successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close browser session");
            return Result.WithFailure($"Failed to close browser: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> FillInputAsync(string selector, string value, CancellationToken cancellationToken = default)
    {
        if (_page == null)
        {
            return Result.WithFailure("Browser session not launched. Call LaunchBrowserAsync first.");
        }

        try
        {
            _logger.LogInformation("Filling input field: {Selector} with value (length: {Length})", selector, value.Length);
            await _page.FillAsync(selector, value);
            _logger.LogInformation("Successfully filled input field: {Selector}", selector);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill input field: {Selector}", selector);
            return Result.WithFailure($"Failed to fill input {selector}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ClickElementAsync(string selector, CancellationToken cancellationToken = default)
    {
        if (_page == null)
        {
            return Result.WithFailure("Browser session not launched. Call LaunchBrowserAsync first.");
        }

        try
        {
            _logger.LogInformation("Clicking element: {Selector}", selector);
            await _page.ClickAsync(selector);
            _logger.LogInformation("Successfully clicked element: {Selector}", selector);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to click element: {Selector}", selector);
            return Result.WithFailure($"Failed to click element {selector}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default)
    {
        if (_page == null)
        {
            return Result.WithFailure("Browser session not launched. Call LaunchBrowserAsync first.");
        }

        try
        {
            _logger.LogInformation("Waiting for selector: {Selector} (timeout: {Timeout}ms)", selector, timeoutMs ?? _options.PageTimeoutMs);
            await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                Timeout = timeoutMs ?? _options.PageTimeoutMs
            });
            _logger.LogInformation("Successfully found selector: {Selector}", selector);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find selector: {Selector}", selector);
            return Result.WithFailure($"Failed to find selector {selector}: {ex.Message}", ex);
        }
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        // Simple pattern matching: supports *.ext format
        if (pattern.StartsWith("*."))
        {
            var extension = pattern.Substring(1);
            return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        // Exact match
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static FileFormat DetermineFileFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => FileFormat.Pdf,
            ".xml" => FileFormat.Xml,
            ".docx" => FileFormat.Docx,
            ".zip" => FileFormat.Zip,
            _ => FileFormat.Pdf // Default to PDF
        };
    }
}

/// <summary>
/// Configuration options for browser automation.
/// </summary>
public class BrowserAutomationOptions
{
    /// <summary>
    /// Gets or sets whether to run browser in headless mode.
    /// </summary>
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Gets or sets the browser launch timeout in milliseconds.
    /// </summary>
    public int BrowserLaunchTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the page navigation timeout in milliseconds.
    /// </summary>
    public int PageTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the default regulatory website URL to navigate to.
    /// </summary>
    public string DefaultWebsiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of file patterns to match for downloadable files (e.g., "*.pdf", "*.xml", "*.docx").
    /// </summary>
    public List<string> FilePatterns { get; set; } = new() { "*.pdf", "*.xml", "*.docx" };
}

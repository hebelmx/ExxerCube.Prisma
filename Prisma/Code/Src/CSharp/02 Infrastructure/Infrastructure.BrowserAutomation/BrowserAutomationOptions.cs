namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation;

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
    /// Gets or sets whether to ignore TLS certificate errors. Default <c>false</c> (production SIARA has a
    /// valid certificate). Set <c>true</c> only against a dev/sim host with a self-signed certificate — the
    /// browser may trust a locally-installed dev cert via the OS store, but Playwright's API-request stack
    /// (used to fetch document bytes) validates TLS independently and would otherwise reject it.
    /// </summary>
    public bool IgnoreHttpsErrors { get; set; }

    /// <summary>
    /// Gets or sets the default regulatory website URL to navigate to.
    /// </summary>
    public string DefaultWebsiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of file patterns to match for downloadable files (e.g., "*.pdf", "*.xml", "*.docx").
    /// </summary>
    public List<string> FilePatterns { get; set; } = new() { "*.pdf", "*.xml", "*.docx" };
}
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
    /// Gets or sets the Chromium launch arguments. Defaults to the flags a headed browser
    /// needs when running as root inside a container (e.g. the /browser-automation demo with
    /// X11 forwarding): <c>--no-sandbox</c> (root cannot use the sandbox), <c>--disable-dev-shm-usage</c>
    /// (small container /dev/shm), and <c>--disable-gpu</c> (no GPU in the container). Harmless
    /// on a desktop / in headless CI.
    /// </summary>
    public List<string> LaunchArgs { get; set; } = new() { "--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu" };

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
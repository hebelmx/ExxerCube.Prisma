namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;

/// <summary>
/// Configuration options for navigation targets.
/// </summary>
public class NavigationTargetOptions
{
    /// <summary>
    /// Gets or sets the <b>server-facing</b> URL for the SIARA simulator — the address the
    /// Web.UI process (and the container-hosted Playwright agent, whose browser process runs
    /// inside the compose network) uses to reach the simulator, e.g. <c>http://siara-simulator:8080</c>.
    /// Not reachable from the end-user's browser.
    /// </summary>
    public string? SiaraUrl { get; set; }

    /// <summary>
    /// Gets or sets the <b>browser-facing</b> SIARA URL — the host-reachable address used when the
    /// UI redirects the <i>user's</i> browser (e.g. Home "Open SIARA"), e.g. <c>http://localhost:8084</c>
    /// (container 8080 published to host 8084). Distinct from <see cref="SiaraUrl"/>: the Docker-internal
    /// DNS name <c>siara-simulator</c> does not resolve from the user's browser. Falls back to
    /// <see cref="SiaraUrl"/> when unset (correct for non-container/dev where both are the same host).
    /// </summary>
    public string? SiaraBrowserUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL for Internet Archive.
    /// </summary>
    public string? ArchiveUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL for Project Gutenberg.
    /// </summary>
    public string? GutenbergUrl { get; set; }
}

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;

/// <summary>
/// Configuration options for navigation targets.
/// </summary>
public class NavigationTargetOptions
{
    /// <summary>
    /// Gets or sets the URL for the SIARA simulator.
    /// </summary>
    public string? SiaraUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL for Internet Archive.
    /// </summary>
    public string? ArchiveUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL for Project Gutenberg.
    /// </summary>
    public string? GutenbergUrl { get; set; }
}

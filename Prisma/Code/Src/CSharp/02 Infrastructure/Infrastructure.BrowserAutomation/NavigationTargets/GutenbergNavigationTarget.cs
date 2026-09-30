namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;

/// <summary>
/// Navigation target for Project Gutenberg (gutenberg.org).
/// </summary>
public class GutenbergNavigationTarget : INavigationTarget
{
    private readonly ILogger<GutenbergNavigationTarget> _logger;
    private readonly NavigationTargetOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GutenbergNavigationTarget"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The navigation target options.</param>
    public GutenbergNavigationTarget(
        ILogger<GutenbergNavigationTarget> logger,
        IOptions<NavigationTargetOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Id => "gutenberg";

    /// <inheritdoc />
    public string DisplayName => "Project Gutenberg";

    /// <inheritdoc />
    public string Description => "Free eBooks - Over 70,000 free books available";

    /// <inheritdoc />
    public string BaseUrl => _options.GutenbergUrl ?? "https://www.gutenberg.org";

    /// <inheritdoc />
    public async Task<Result> NavigateAsync(IBrowserAutomationAgent agent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating to Project Gutenberg at {Url}", BaseUrl);

        var launchResult = await agent.LaunchBrowserAsync(cancellationToken);
        if (!launchResult.IsSuccess)
        {
            return launchResult;
        }

        var navigateResult = await agent.NavigateToAsync(BaseUrl, cancellationToken);
        if (!navigateResult.IsSuccess)
        {
            await agent.CloseBrowserAsync(cancellationToken);
            return navigateResult;
        }

        _logger.LogInformation("Successfully navigated to Project Gutenberg");
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<List<DownloadableFile>>> RetrieveDocumentsAsync(
        IBrowserAutomationAgent agent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving documents from Project Gutenberg");

        // Project Gutenberg has various text formats
        var filePatterns = new[] { "*.txt", "*.html", "*.epub", "*.pdf" };
        var result = await agent.IdentifyDownloadableFilesAsync(filePatterns, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Found {Count} documents in Project Gutenberg", result.Value?.Count ?? 0);
        }
        else
        {
            _logger.LogError("Failed to retrieve documents from Project Gutenberg: {Error}", result.Error);
        }

        return result;
    }
}

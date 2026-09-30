namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;

/// <summary>
/// Navigation target for Internet Archive (archive.org).
/// </summary>
public class InternetArchiveNavigationTarget : INavigationTarget
{
    private readonly ILogger<InternetArchiveNavigationTarget> _logger;
    private readonly NavigationTargetOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternetArchiveNavigationTarget"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The navigation target options.</param>
    public InternetArchiveNavigationTarget(
        ILogger<InternetArchiveNavigationTarget> logger,
        IOptions<NavigationTargetOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Id => "archive";

    /// <inheritdoc />
    public string DisplayName => "Internet Archive";

    /// <inheritdoc />
    public string Description => "Digital library of free books, movies, music, and more";

    /// <inheritdoc />
    public string BaseUrl => _options.ArchiveUrl ?? "https://archive.org";

    /// <inheritdoc />
    public async Task<Result> NavigateAsync(IBrowserAutomationAgent agent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating to Internet Archive at {Url}", BaseUrl);

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

        _logger.LogInformation("Successfully navigated to Internet Archive");
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<List<DownloadableFile>>> RetrieveDocumentsAsync(
        IBrowserAutomationAgent agent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving documents from Internet Archive");

        // Internet Archive has various document formats
        var filePatterns = new[] { "*.pdf", "*.xml", "*.txt", "*.epub" };
        var result = await agent.IdentifyDownloadableFilesAsync(filePatterns, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Found {Count} documents in Internet Archive", result.Value?.Count ?? 0);
        }
        else
        {
            _logger.LogError("Failed to retrieve documents from Internet Archive: {Error}", result.Error);
        }

        return result;
    }
}

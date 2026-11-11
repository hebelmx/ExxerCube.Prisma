namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;

/// <summary>
/// Navigation target for SIARA (Sistema de Atención de Requerimientos de Autoridad) simulator.
/// </summary>
public class SiaraNavigationTarget : INavigationTarget
{
    private readonly ILogger<SiaraNavigationTarget> _logger;
    private readonly NavigationTargetOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiaraNavigationTarget"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The navigation target options.</param>
    public SiaraNavigationTarget(
        ILogger<SiaraNavigationTarget> logger,
        IOptions<NavigationTargetOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Id => "siara";

    /// <inheritdoc />
    public string DisplayName => "SIARA Simulator";

    /// <inheritdoc />
    public string Description => "Sistema de Atención de Requerimientos de Autoridad (CNBV)";

    /// <inheritdoc />
    public string BaseUrl => _options.SiaraUrl ?? "https://localhost:5002";

    /// <inheritdoc />
    public async Task<Result> NavigateAsync(IBrowserAutomationAgent agent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating to SIARA simulator at {Url}", BaseUrl);

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

        _logger.LogInformation("Successfully navigated to SIARA simulator");
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<List<DownloadableFile>>> RetrieveDocumentsAsync(
        IBrowserAutomationAgent agent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving documents from SIARA simulator");

        // SIARA simulator presents documents via download links on the dashboard
        // File patterns: *.pdf, *.xml, *.docx
        var filePatterns = new[] { "*.pdf", "*.xml", "*.docx" };
        var result = await agent.IdentifyDownloadableFilesAsync(filePatterns, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Found {Count} documents in SIARA simulator", result.Value?.Count ?? 0);
        }
        else
        {
            _logger.LogError("Failed to retrieve documents from SIARA: {Error}", result.Error);
        }

        return result;
    }
}

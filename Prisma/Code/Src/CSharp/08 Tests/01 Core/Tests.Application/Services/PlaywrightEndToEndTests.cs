namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// End-to-end tests that verify basic Playwright setup, navigation, and interactions against HTML fixtures.
/// </summary>
public class PlaywrightEndToEndTests : IDisposable
{
    private readonly ILogger<PlaywrightEndToEndTests> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaywrightEndToEndTests"/> class with logging routed to xUnit output.
    /// </summary>
    /// <param name="output">xUnit output helper used for test logging.</param>
    public PlaywrightEndToEndTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<PlaywrightEndToEndTests>(output);
    }

    /// <summary>
    /// Validates that Playwright can render a simple HTML document via data URL navigation.
    /// </summary>
    /// <returns>A task that completes after navigation and assertions are executed.</returns>
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Playwright_CanNavigateToPage_ShouldWork()
    {
        // Arrange
        _playwright = PlaywrightConfig.CreatePlaywright();
        _browser = PlaywrightConfig.GetBrowser(_playwright);
        _context = PlaywrightConfig.GetBrowserContext(_browser);
        _page = PlaywrightConfig.GetPage(_context);

        // Act
        var htmlContent = "<html><head><title>Test Page</title></head><body><h1>Test Page</h1></body></html>";
        // Use GotoAsync with data URL instead of SetContentAsync to ensure proper HTML parsing
        var dataUrl = $"data:text/html,{Uri.EscapeDataString(htmlContent)}";
        await _page.GotoAsync(dataUrl);
        var title = await _page.TitleAsync();
        var heading = await _page.TextContentAsync("h1");

        // Assert
        title.ShouldBe("Test Page");
        heading.ShouldBe("Test Page");

        _logger.LogInformation("Playwright end-to-end test completed successfully");
    }

    /// <summary>
    /// Validates basic DOM interaction by simulating a button click and verifying input state.
    /// </summary>
    /// <returns>A task that completes after DOM interactions and assertions are executed.</returns>
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Playwright_CanHandleBasicInteractions_ShouldWork()
    {
        // Arrange
        _playwright = PlaywrightConfig.CreatePlaywright();
        _browser = PlaywrightConfig.GetBrowser(_playwright);
        _context = PlaywrightConfig.GetBrowserContext(_browser);
        _page = PlaywrightConfig.GetPage(_context);

        var html = @"
            <html>
                <body>
                    <input id='testInput' type='text' value='' />
                    <button id='testButton' onclick='document.getElementById(""testInput"").value=""Hello World""'>Click Me</button>
                </body>
            </html>";

        // Act
        await _page.GotoAsync($"data:text/html,{html}");
        await _page.ClickAsync("#testButton");
        var inputValue = await _page.InputValueAsync("#testInput");

        // Assert
        inputValue.ShouldBe("Hello World");

        _logger.LogInformation("Playwright interaction test completed successfully");
    }

    /// <summary>
    /// Disposes test resources (page, context, browser, Playwright runtime) in creation order.
    /// </summary>
    public void Dispose()
    {
        _page?.CloseAsync().Wait();
        _context?.CloseAsync().Wait();
        _browser?.CloseAsync().Wait();
        _playwright?.Dispose();
    }
}

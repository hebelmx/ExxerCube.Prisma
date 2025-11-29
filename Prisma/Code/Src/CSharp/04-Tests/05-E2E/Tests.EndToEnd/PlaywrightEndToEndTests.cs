namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// End-to-end tests using Playwright for web interface testing.
/// </summary>
public class PlaywrightEndToEndTests : IDisposable
{
    private readonly ILogger<PlaywrightEndToEndTests> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaywrightEndToEndTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public PlaywrightEndToEndTests(ITestOutputHelper output)
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PlaywrightEndToEndTests>();
    }

    /// <summary>
    /// Tests that Playwright can navigate to a simple page.
    /// </summary>
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
        // Use GotoAsync with data URL and proper encoding to ensure HTML parsing works correctly
        var dataUrl = $"data:text/html,{Uri.EscapeDataString(htmlContent)}";
        await _page.GotoAsync(dataUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        var title = await _page.TitleAsync();
        var heading = await _page.TextContentAsync("h1");

        // Assert
        title.ShouldBe("Test Page");
        heading.ShouldBe("Test Page");

        _logger.LogInformation("Playwright navigation test completed successfully");
    }

    /// <summary>
    /// Tests that Playwright can handle basic interactions.
    /// </summary>
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
    /// Disposes the test resources.
    /// </summary>
    public void Dispose()
    {
        _page?.CloseAsync().Wait();
        _context?.CloseAsync().Wait();
        _browser?.CloseAsync().Wait();
        _playwright?.Dispose();
    }
}
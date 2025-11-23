using System;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using ExxerCube.Prisma.Tests.UI.Infrastructure;

namespace ExxerCube.Prisma.Tests.UI.Navigation;

public class NavigationSmokeTests : IAsyncLifetime
{
    private const string BaseUrlEnvironmentVariable = "PRISMA_UI_BASEURL";
    private PrismaWebApplicationFactory? _factory;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string? _baseUrl;

    private string BaseUrl => _baseUrl ??
        Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable)?.TrimEnd('/') ??
        throw new InvalidOperationException("Base URL not initialized");

    public async ValueTask InitializeAsync()
    {
        // Start the web application
        _factory = new PrismaWebApplicationFactory();

        // Get the server URL - WebApplicationFactory uses HTTP by default for test server
        var server = _factory.Server;
        _baseUrl = _factory.Server.BaseAddress.ToString().TrimEnd('/');

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task DrawerLinkNavigatesToDocumentProcessing()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(BaseUrl);
        await EnsureDrawerOpenAsync(page);

        await page.GetByRole(AriaRole.Link, new() { Name = "Upload & Process" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/document-processing/?$", RegexOptions.IgnoreCase));

        var activeLink = page.GetByRole(AriaRole.Link, new() { Name = "Upload & Process" });
        await Expect(activeLink).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task UnknownRouteShowsHelpfulNotFoundPanel()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/definitely-not-a-real-route");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Let's get you back on track" }))
            .ToBeVisibleAsync();

        await page.GetByPlaceholder("Search pages (e.g., audit, manual review)").FillAsync("audit");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Open Audit Trail" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Go home" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/?$"));
    }

    private async Task<IBrowserContext> NewContextAsync()
    {
        EnsureBrowser();
        return await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
    }

    private async Task EnsureDrawerOpenAsync(IPage page)
    {
        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Open navigation menu" });
        if (await toggle.IsVisibleAsync())
        {
            await toggle.ClickAsync();
        }
    }

    private void EnsureBrowser()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser is not initialized. Ensure InitializeAsync ran correctly.");
        }
    }
}

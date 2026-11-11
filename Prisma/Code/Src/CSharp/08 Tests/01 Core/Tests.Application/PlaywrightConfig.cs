namespace ExxerCube.Prisma.Tests.Application;

/// <summary>
/// Centralized Playwright helpers used by end-to-end tests to create browsers, contexts, and pages with consistent options.
/// </summary>
public static class PlaywrightConfig
{
    /// <summary>
    /// Creates a Playwright runtime instance for test execution.
    /// </summary>
    /// <returns>An initialized <see cref="IPlaywright"/> runtime.</returns>
    /// <remarks>This helper blocks on async initialization because most tests are synchronous factory calls.</remarks>
    public static IPlaywright CreatePlaywright()
    {
        return Playwright.CreateAsync().Result;
    }

    /// <summary>
    /// Launches a Chromium browser configured for CI-friendly execution.
    /// </summary>
    /// <param name="playwright">The Playwright instance used to launch the browser.</param>
    /// <returns>A launched <see cref="IBrowser"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the browser executable is missing; advises installing via Playwright scripts.</exception>
    public static IBrowser GetBrowser(IPlaywright playwright)
    {
        try
        {
            return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                SlowMo = 100
            }).Result;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist") || ex.Message.Contains("Looks like Playwright was just installed"))
        {
            throw new InvalidOperationException(
                "Playwright browsers are not installed. Please run: pwsh install-playwright-browsers.ps1 " +
                "or manually install browsers using: playwright install chromium",
                ex);
        }
    }

    /// <summary>
    /// Creates a browser context with stable viewport and user agent for deterministic screenshots and navigation.
    /// </summary>
    /// <param name="browser">The browser instance that owns the context.</param>
    /// <returns>A configured <see cref="IBrowserContext"/> ready for page creation.</returns>
    public static IBrowserContext GetBrowserContext(IBrowser browser)
    {
        return browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            UserAgent = "ExxerCube.Prisma.TestRunner/1.0"
        }).Result;
    }

    /// <summary>
    /// Creates a new page within the provided browser context.
    /// </summary>
    /// <param name="context">The browser context used to create the page.</param>
    /// <returns>A new <see cref="IPage"/> instance.</returns>
    public static IPage GetPage(IBrowserContext context)
    {
        return context.NewPageAsync().Result;
    }
}


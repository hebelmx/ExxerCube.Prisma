namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// Playwright configuration for end-to-end testing.
/// </summary>
public class PlaywrightConfig
{
    /// <summary>
    /// Gets the Playwright configuration for testing.
    /// </summary>
    /// <returns>The Playwright configuration.</returns>
    public static IPlaywright CreatePlaywright()
    {
        return Playwright.CreateAsync().Result;
    }

    /// <summary>
    /// Gets the browser configuration for testing.
    /// </summary>
    /// <param name="playwright">The Playwright instance.</param>
    /// <returns>The browser configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when browser executable is not found. Install browsers using: pwsh install-playwright-browsers.ps1</exception>
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
    /// Gets the browser context configuration for testing.
    /// </summary>
    /// <param name="browser">The browser instance.</param>
    /// <returns>The browser context configuration.</returns>
    public static IBrowserContext GetBrowserContext(IBrowser browser)
    {
        return browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            UserAgent = "ExxerCube.Prisma.TestRunner/1.0"
        }).Result;
    }

    /// <summary>
    /// Gets the page configuration for testing.
    /// </summary>
    /// <param name="context">The browser context.</param>
    /// <returns>The page configuration.</returns>
    public static IPage GetPage(IBrowserContext context)
    {
        return context.NewPageAsync().Result;
    }
}
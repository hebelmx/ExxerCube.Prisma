using Microsoft.Playwright;

namespace ExxerCube.Prisma.Tests;

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
    public static IBrowser GetBrowser(IPlaywright playwright)
    {
        return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 100
        }).Result;
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

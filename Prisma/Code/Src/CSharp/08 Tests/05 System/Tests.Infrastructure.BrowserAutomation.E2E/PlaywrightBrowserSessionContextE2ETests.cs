using ExxerCube.Prisma.Infrastructure.BrowserAutomation;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E;

/// <summary>
/// Real-browser E2E coverage for the <see cref="IBrowserSessionContext"/> happy paths on the
/// <see cref="PlaywrightBrowserAutomationAdapter"/> (the parts the cross-impl contract cannot assert
/// without a live browser). Headless and offline — requires only a Chromium install
/// (<c>playwright install chromium</c>), no network.
/// </summary>
public sealed class PlaywrightBrowserSessionContextE2ETests
{
    private static PlaywrightBrowserAutomationAdapter CreateAdapter() =>
        new(
            Substitute.For<ILogger<PlaywrightBrowserAutomationAdapter>>(),
            Options.Create(new BrowserAutomationOptions { Headless = true }));

    /// <summary>
    /// A launched session can be captured as storage-state and re-hydrated into a fresh context —
    /// the export/restore round-trip the SIARA InteractiveLogin strategy depends on.
    /// </summary>
    [Fact]
    public async Task ExportThenLoadStorageState_AfterLaunch_RoundTripsSuccessfully()
    {
        var adapter = CreateAdapter();
        var launch = await adapter.LaunchBrowserAsync(TestContext.Current.CancellationToken);
        launch.IsSuccess.ShouldBeTrue($"launch failed: {launch.Error}");

        try
        {
            // Export the (empty but valid) authenticated context — no navigation/network needed.
            var export = await adapter.ExportStorageStateAsync(TestContext.Current.CancellationToken);
            export.IsSuccess.ShouldBeTrue($"export failed: {export.Error}");
            export.Value.ShouldNotBeNullOrEmpty();
            export.Value!.ShouldContain("cookies"); // Playwright storage-state JSON shape: {"cookies":[],"origins":[]}

            // Re-hydrate it into a brand-new context on the same browser.
            var load = await adapter.LoadStorageStateAsync(export.Value, TestContext.Current.CancellationToken);
            load.IsSuccess.ShouldBeTrue($"load failed: {load.Error}");
        }
        finally
        {
            await adapter.CloseBrowserAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// After launch, probing for a selector that is absent on a blank page returns success-with-false
    /// (a clean negative), not a failure — only missing/invalid input or a probe error fails.
    /// </summary>
    [Fact]
    public async Task IsAuthenticatedAsync_AfterLaunch_AbsentSelector_ReturnsSuccessFalse()
    {
        var adapter = CreateAdapter();
        var launch = await adapter.LaunchBrowserAsync(TestContext.Current.CancellationToken);
        launch.IsSuccess.ShouldBeTrue($"launch failed: {launch.Error}");

        try
        {
            var probe = await adapter.IsAuthenticatedAsync("#a-selector-that-does-not-exist", TestContext.Current.CancellationToken);

            probe.IsSuccess.ShouldBeTrue($"probe failed: {probe.Error}");
            probe.Value.ShouldBeFalse();
        }
        finally
        {
            await adapter.CloseBrowserAsync(TestContext.Current.CancellationToken);
        }
    }
}

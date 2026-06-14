using System.Net.Http;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using IndQuestResults;

namespace ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E;

/// <summary>
/// MVP-PATH 1.2 acceptance: discovers real documents from the live SIARA simulator through the WHOLE real
/// discovery path the watch loop uses — <see cref="ISiaraSessionProviderResolver"/> →
/// <see cref="SessionPassthroughSiaraSessionProvider"/> → <see cref="SiaraDocumentSource"/> — with a real
/// headless Playwright browser, and asserts the source lists at least one document id (a SIARA document
/// URL) off a single warm session.
/// </summary>
/// <remarks>
/// Headless. Starts the published simulator (<c>Deployments/Siara.Simulator/app</c>) if it is not already
/// running on <c>http://localhost:5001</c>. Requires a Chromium install (<c>playwright install chromium</c>).
/// </remarks>
[Collection("SiaraSimulator")]
public sealed class SiaraDocumentSourceE2ETests : IAsyncLifetime
{
    private const string SimulatorUrl = "http://localhost:5001";
    private const string ValidUsername = "BANAMEX";
    private const string ValidPassword = "password123";
    private const string DashboardSelector = "#arrivalRateSlider"; // present only on the authenticated dashboard

    private readonly ITestOutputHelper _output;
    private Process? _simulatorProcess;
    private bool _startedSim;

    public SiaraDocumentSourceE2ETests(ITestOutputHelper output) => _output = output;

    public async ValueTask InitializeAsync()
    {
        if (!await IsSimulatorRunningAsync())
        {
            await StartSimulatorAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_startedSim && _simulatorProcess is { HasExited: false } proc)
        {
            try
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch
            {
                // best-effort teardown
            }
        }

        _simulatorProcess?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DiscoverCasesAsync_ThroughPassthroughProvider_ListsRealCasesFromSimulator()
    {
        var ct = TestContext.Current.CancellationToken;

        // 1) Log into the simulator with a real headless browser and capture an authenticated storage-state
        //    (the credential-free artifact SessionPassthrough rides — no credentials reach the source).
        var authAdapter = CreateAdapter();
        (await authAdapter.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();
        (await authAdapter.NavigateToAsync($"{SimulatorUrl}/login", ct)).IsSuccess.ShouldBeTrue();
        (await authAdapter.WaitForSelectorAsync("#username", 15000, ct)).IsSuccess.ShouldBeTrue();
        (await authAdapter.FillInputAsync("#username", ValidUsername, ct)).IsSuccess.ShouldBeTrue();
        (await authAdapter.FillInputAsync("#password", ValidPassword, ct)).IsSuccess.ShouldBeTrue();
        (await authAdapter.ClickElementAsync("button[type='submit']", ct)).IsSuccess.ShouldBeTrue();
        (await authAdapter.WaitForSelectorAsync(DashboardSelector, 15000, ct)).IsSuccess.ShouldBeTrue("login should reach the dashboard");

        // Wait for the InteractiveServer circuit to render at least one document link before capturing the
        // storage-state, so the simulator definitely has cases to discover.
        var linkAppeared = await authAdapter.WaitForSelectorAsync("a[href$='.pdf']", 90000, ct);
        linkAppeared.IsSuccess.ShouldBeTrue("the simulator dashboard should render at least one PDF document link within 90s");

        var authState = await authAdapter.ExportStorageStateAsync(ct);
        authState.IsSuccess.ShouldBeTrue($"storage-state capture failed: {authState.Error}");
        authState.Value.ShouldNotBeNullOrEmpty();
        await authAdapter.CloseBrowserAsync(ct);

        // 2) Discover documents through the REAL path on a FRESH browser: resolver -> passthrough provider ->
        //    SiaraDocumentSource, configured to authenticate against the simulator via the storage-state.
        var sutAdapter = CreateAdapter();
        try
        {
            var options = Options.Create(new SiaraAuthOptions
            {
                AuthMode = SiaraAuthMode.SessionPassthrough,
                Actor = new SiaraActorOptions { ActorId = "orion-e2e-service-account" },
                Passthrough = new SiaraPassthroughOptions
                {
                    Transport = SiaraPassthroughTransport.StorageState,
                    StorageStateRef = authState.Value,
                    DashboardUrl = $"{SimulatorUrl}/",
                    PostLoginSelector = DashboardSelector,
                },
            });

            var actorIdentity = new ConfiguredSiaraActorIdentityProvider(
                options, XUnitLogger.CreateLogger<ConfiguredSiaraActorIdentityProvider>(_output));

            var provider = new SessionPassthroughSiaraSessionProvider(
                sutAdapter, sutAdapter, actorIdentity, options,
                XUnitLogger.CreateLogger<SessionPassthroughSiaraSessionProvider>(_output));

            var resolver = Substitute.For<ISiaraSessionProviderResolver>();
            resolver.Resolve().Returns(Result<ISiaraSessionProvider>.Success(provider));

            var navigationTarget = new SiaraNavigationTarget(
                XUnitLogger.CreateLogger<SiaraNavigationTarget>(_output),
                Options.Create(new NavigationTargetOptions { SiaraUrl = $"{SimulatorUrl}/" }));

            var sut = new SiaraDocumentSource(
                resolver, sutAdapter, sutAdapter, navigationTarget, options,
                XUnitLogger.CreateLogger<SiaraDocumentSource>(_output));

            // The source keeps one warm session; re-listing re-navigates the live DOM, so poll a few cycles
            // (exactly as the watch loop does) until the InteractiveServer circuit has rendered a case row.
            IReadOnlyList<SiaraCase>? cases = null;
            for (var attempt = 0; attempt < 18 && (cases is null || cases.Count == 0); attempt++)
            {
                var discovered = await sut.DiscoverCasesAsync(ct);
                discovered.IsSuccess.ShouldBeTrue($"discovery failed: {discovered.Error}");
                cases = discovered.Value;
                if (cases is { Count: > 0 })
                {
                    break;
                }

                await Task.Delay(5000, ct);
            }

            cases.ShouldNotBeNull();
            cases!.ShouldNotBeEmpty("the warm SIARA discovery session should list at least one case within ~90s");
            cases.ShouldAllBe(c => !string.IsNullOrWhiteSpace(c.CaseId));

            _output.WriteLine($"Discovered {cases.Count} SIARA case(s); first: {cases[0].CaseId}");

            await sut.DisposeAsync();
        }
        finally
        {
            await sutAdapter.CloseBrowserAsync(ct);
        }
    }

    private PlaywrightBrowserAutomationAdapter CreateAdapter() =>
        new(
            XUnitLogger.CreateLogger<PlaywrightBrowserAutomationAdapter>(_output),
            Options.Create(new BrowserAutomationOptions
            {
                Headless = true,
                BrowserLaunchTimeoutMs = 60000,
                PageTimeoutMs = 30000,
                IgnoreHttpsErrors = true, // the simulator serves https with a self-signed dev certificate
            }));

    private static async Task<bool> IsSimulatorRunningAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await http.GetAsync(SimulatorUrl);
            return response.IsSuccessStatusCode || (int)response.StatusCode == 302; // redirect to /login counts as up
        }
        catch
        {
            return false;
        }
    }

    private async Task StartSimulatorAsync()
    {
        var exePath = ResolveSimulatorPath()
            ?? throw new FileNotFoundException(
                "Siara.Simulator.exe not found under any 'Deployments/Siara.Simulator/app'. Publish the simulator first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _simulatorProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the SIARA simulator process.");
        _startedSim = true;

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(500);
            if (await IsSimulatorRunningAsync())
            {
                return;
            }
        }

        throw new TimeoutException($"Simulator did not become ready within 20s on {SimulatorUrl}.");
    }

    private static string? ResolveSimulatorPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Deployments", "Siara.Simulator", "app", "Siara.Simulator.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var sibling = Path.Combine(dir.FullName, "ExxerCube.Prisma", "Deployments", "Siara.Simulator", "app", "Siara.Simulator.exe");
            if (File.Exists(sibling))
            {
                return sibling;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

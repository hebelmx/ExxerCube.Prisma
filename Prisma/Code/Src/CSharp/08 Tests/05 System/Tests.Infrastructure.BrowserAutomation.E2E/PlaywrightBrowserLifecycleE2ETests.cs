using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.NavigationTargets;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E;

/// <summary>
/// Regression guard for the Orion Chromium leak (2026-10-01): every SIARA poll left a headless browser
/// behind (24 after two hours, 15 cores, 2,712 PIDs), because the scoped adapter was never disposed and a
/// re-acquire relaunched over the live browser. These tests count the real browser processes this test
/// host spawned, so a missing disposal or an orphaning relaunch fails here, not in production.
/// </summary>
/// <remarks>
/// Linux only (reads <c>/proc</c>); requires a Chromium install (<c>playwright install chromium</c>).
/// The SIARA poll test additionally needs a running simulator: set <c>PRISMA_SIARA_SIMULATOR_URL</c>
/// (e.g. <c>http://localhost:8084</c> for the Docker dev stack); it is skipped otherwise.
/// Serialized with the other simulator classes because browser counts are per test host.
/// </remarks>
[Collection("SiaraSimulator")]
public sealed class PlaywrightBrowserLifecycleE2ETests
{
    private const string EmptyStorageState = """{"cookies":[],"origins":[]}""";

    [Fact]
    public async Task LaunchBrowserAsync_CalledTwiceOnSameInstance_KeepsOneBrowser()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();

        try
        {
            (await adapter.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();
            (await adapter.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();

            (await WaitForBrowserCountAsync(1, ct)).ShouldBe(1, "a relaunch must close the previous browser, not orphan it");
        }
        finally
        {
            await adapter.DisposeAsync();
        }

        (await WaitForBrowserCountAsync(0, ct)).ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_AfterLaunch_ClosesBrowserAndIsIdempotent()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        (await adapter.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();
        (await WaitForBrowserCountAsync(1, ct)).ShouldBe(1);

        await adapter.DisposeAsync();
        await adapter.DisposeAsync();

        (await WaitForBrowserCountAsync(0, ct)).ShouldBe(0, "disposal must close the browser");
        (await adapter.CloseBrowserAsync(ct)).IsSuccess.ShouldBeTrue("closing after disposal is a no-op");
    }

    [Fact]
    public async Task LoadStorageStateAsync_RepeatedEachCycle_ClosesTheContextItReplaces()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();

        try
        {
            (await adapter.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();
            (await adapter.LoadStorageStateAsync(EmptyStorageState, ct)).IsSuccess.ShouldBeTrue();
            var renderersAfterOne = await CountRendererProcessesAsync(ct);

            // Discovery hydrates twice per poll (re-validate + scrape); simulate many polls.
            for (var cycle = 0; cycle < 8; cycle++)
            {
                (await adapter.LoadStorageStateAsync(EmptyStorageState, ct)).IsSuccess.ShouldBeTrue();
            }

            var renderersAfterMany = await CountRendererProcessesAsync(ct);
            renderersAfterMany.ShouldBeLessThanOrEqualTo(
                renderersAfterOne + 1,
                $"replaced contexts must be closed (renderers: {renderersAfterOne} after 1 hydration, {renderersAfterMany} after 9)");
        }
        finally
        {
            await adapter.DisposeAsync();
        }
    }

    [Fact]
    public async Task ScopedAdapter_TwoScopesThatNeverCloseTheBrowser_LeaveNoBrowserBehind()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddBrowserAutomationServices(o => o.Headless = true)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        for (var cycle = 1; cycle <= 2; cycle++)
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var agent = scope.ServiceProvider.GetRequiredService<IBrowserAutomationAgent>();
                var context = scope.ServiceProvider.GetRequiredService<IBrowserSessionContext>();
                (await agent.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();
                (await context.LoadStorageStateAsync(EmptyStorageState, ct)).IsSuccess.ShouldBeTrue();
                (await WaitForBrowserCountAsync(1, ct)).ShouldBe(1);

                // Deliberately no CloseBrowserAsync: scope teardown alone must close the browser.
            }

            (await WaitForBrowserCountAsync(0, ct)).ShouldBe(0, $"scope {cycle} left a browser running");
        }
    }

    [Fact]
    public async Task SiaraDiscovery_AutomatedLoginOverSeveralPolls_HoldsOneBrowserAndScopeTeardownClosesIt()
    {
        SkipUnlessLinux();
        var simulatorUrl = Environment.GetEnvironmentVariable("PRISMA_SIARA_SIMULATOR_URL");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(simulatorUrl), "Set PRISMA_SIARA_SIMULATOR_URL to run against a SIARA simulator.");
        simulatorUrl = simulatorUrl!.TrimEnd('/');
        var ct = TestContext.Current.CancellationToken;

        // The Orion Worker's composition for the dev stack: AutomatedLogin against the simulator.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Siara:AuthMode"] = nameof(SiaraAuthMode.AutomatedLogin),
                ["Siara:BaseUrl"] = simulatorUrl,
                ["Siara:Automated:LoginUrl"] = $"{simulatorUrl}/login",
                ["Siara:Automated:DashboardUrl"] = $"{simulatorUrl}/",
                ["Siara:Automated:PostLoginSelector"] = ".case-list",
                ["Siara:Actor:ActorId"] = "orion-lifecycle-test",
                ["Siara:Credentials:Username"] = "BANEJEMPLO",
                ["Siara:Credentials:Password"] = "password123",
                ["NavigationTargets:SiaraUrl"] = simulatorUrl,
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddBrowserAutomationServices(o => o.Headless = true);
        services.Configure<NavigationTargetOptions>(configuration.GetSection("NavigationTargets"));
        services.AddSiaraAuthentication(configuration);
        services.AddScoped<ISiaraDocumentSource, SiaraDocumentSource>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using (var scope = provider.CreateAsyncScope())
        {
            var source = scope.ServiceProvider.GetRequiredService<ISiaraDocumentSource>();

            for (var poll = 1; poll <= 3; poll++)
            {
                var discovered = await source.DiscoverCasesAsync(ct);
                discovered.IsSuccess.ShouldBeTrue($"poll {poll} failed: {discovered.Error}");
                (await WaitForBrowserCountAsync(1, ct)).ShouldBe(1, $"poll {poll} must reuse the warm browser, not add one");
            }
        }

        (await WaitForBrowserCountAsync(0, ct)).ShouldBe(0, "discovery scope teardown must close the browser");
    }

    private static PlaywrightBrowserAutomationAdapter CreateAdapter() =>
        new(
            Substitute.For<ILogger<PlaywrightBrowserAutomationAdapter>>(),
            Options.Create(new BrowserAutomationOptions { Headless = true }));

    private static void SkipUnlessLinux() =>
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Counts browser processes through /proc (Linux only).");

    /// <summary>
    /// Polls until this host's browser count equals <paramref name="expected"/> or a short deadline passes,
    /// then returns the last count (process exit lags the awaited close by a few milliseconds).
    /// </summary>
    private static async Task<int> WaitForBrowserCountAsync(int expected, CancellationToken ct)
    {
        var count = -1;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            count = ChromiumDescendants().Count(p => !p.CommandLine.Contains("--type=", StringComparison.Ordinal));
            if (count == expected)
            {
                return count;
            }

            await Task.Delay(100, ct);
        }

        return count;
    }

    private static async Task<int> CountRendererProcessesAsync(CancellationToken ct)
    {
        await Task.Delay(500, ct); // let Chromium settle its renderer pool
        return ChromiumDescendants().Count(p => p.CommandLine.Contains("--type=renderer", StringComparison.Ordinal));
    }

    /// <summary>Chromium processes (browser + children) descended from this test host.</summary>
    private static List<(int Pid, string CommandLine)> ChromiumDescendants()
    {
        var parents = new Dictionary<int, int>();
        var commandLines = new Dictionary<int, string>();
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
            {
                continue;
            }

            try
            {
                // /proc/<pid>/stat: "pid (comm) state ppid ..." — comm may contain spaces, so split after ')'.
                var stat = File.ReadAllText(Path.Combine(dir, "stat"));
                var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                parents[pid] = int.Parse(fields[1], global::System.Globalization.CultureInfo.InvariantCulture);
                commandLines[pid] = File.ReadAllText(Path.Combine(dir, "cmdline")).Replace('\0', ' ');
            }
            catch (IOException)
            {
                // the process exited while we were reading it
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var self = Environment.ProcessId;
        bool DescendsFromSelf(int pid)
        {
            for (var hops = 0; hops < 64 && parents.TryGetValue(pid, out var parent); hops++)
            {
                if (parent == self)
                {
                    return true;
                }

                pid = parent;
            }

            return false;
        }

        return commandLines
            .Where(kv => (kv.Value.Contains("headless_shell", StringComparison.Ordinal) || kv.Value.Contains("/chrome", StringComparison.Ordinal))
                && DescendsFromSelf(kv.Key))
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}

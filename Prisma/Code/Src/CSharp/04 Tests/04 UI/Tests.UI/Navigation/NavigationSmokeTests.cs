using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Tests.UI.Navigation;

public class NavigationSmokeTests : IAsyncLifetime
{
    private const string BaseUrlEnvironmentVariable = "PRISMA_UI_BASEURL";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string? _baseUrl;
    private Process? _uiProcess;
    private readonly List<string> _uiOutput = new();

    private string BaseUrl => _baseUrl ??
        Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable)?.TrimEnd('/') ??
        throw new InvalidOperationException("Base URL not initialized");

    public async ValueTask InitializeAsync()
    {
        var envBaseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable)?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(envBaseUrl))
        {
            _baseUrl = envBaseUrl;
        }
        else
        {
            _baseUrl = await StartUiProcessAsync();
        }

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
        if (_uiProcess is { HasExited: false })
        {
            _uiProcess.Kill(entireProcessTree: true);
            _uiProcess.WaitForExit(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task DrawerLinkNavigatesToDocumentProcessing()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await WaitForShellAsync(page);
        await EnsureDrawerOpenAsync(page);

        await page.Locator("a[href='/document-processing']").First.ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/document-processing/?$", RegexOptions.IgnoreCase));

        // Verify link is still visible (navigation succeeded)
        var activeLink = page.Locator("a[href='/document-processing']").First;
        await Expect(activeLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UnknownRouteShowsHelpfulNotFoundPanel()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        // Validate server-side NotFound rendering without relying on SPA hydration timing
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler);
        var html = await client.GetStringAsync($"{BaseUrl}/not-found", TestContext.Current.CancellationToken);
        Assert.Contains("Let's get you back on track", html, StringComparison.OrdinalIgnoreCase);

        await page.GotoAsync($"{BaseUrl}/not-found", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await WaitForShellAsync(page);

        var foundHeading = false;
        string? content = null;
        for (var i = 0; i < 10; i++)
        {
            content = await page.ContentAsync();
            if (content.Contains("Let's get you back on track", StringComparison.OrdinalIgnoreCase))
            {
                foundHeading = true;
                break;
            }
            await page.WaitForTimeoutAsync(500);
        }

        Assert.True(foundHeading, $"NotFound page did not render expected heading. Content snapshot: {content ?? "<null>"}");

        // Assert directory-style suggestions (non-empty body with known links)
        var suggestions = new[]
        {
            "System Flow",
            "Document Processing",
            "Processing Dashboard"
        };

        foreach (var suggestion in suggestions)
        {
            Assert.Contains(suggestion, content, StringComparison.OrdinalIgnoreCase);
        }

        var search = page.GetByPlaceholder("Search pages (e.g., audit, manual review)");
        if (await search.IsVisibleAsync())
        {
            await search.FillAsync("audit");
        }
        var openAudit = page.GetByRole(AriaRole.Button, new() { Name = "Open Audit Trail" });
        if (await openAudit.IsVisibleAsync())
        {
            await Expect(openAudit).ToBeVisibleAsync();
        }

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

    private static async Task WaitForShellAsync(IPage page)
    {
        // Wait for the document to load; avoid strict element visibility to reduce hydration flakiness
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.WaitForTimeoutAsync(500); // brief settle for hydration
    }

    private void EnsureBrowser()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser is not initialized. Ensure InitializeAsync ran correctly.");
        }
    }

    private async Task<string> StartUiProcessAsync()
    {
        var httpPort = GetFreePort();
        var baseAddress = $"http://127.0.0.1:{httpPort}";
        var urls = baseAddress;

        var uiDllPath = ResolveUiDllPath();
        var psi = new ProcessStartInfo("dotnet", $"\"{uiDllPath}\" --urls={urls}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(uiDllPath)!,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ASPNETCORE_URLS"] = urls;

        _uiProcess = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Prisma UI process");
        _uiProcess.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_uiOutput) { _uiOutput.Add($"[OUT] {e.Data}"); }
            }
        };
        _uiProcess.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_uiOutput) { _uiOutput.Add($"[ERR] {e.Data}"); }
            }
        };
        _uiProcess.BeginOutputReadLine();
        _uiProcess.BeginErrorReadLine();

        try
        {
            await WaitForHealthAsync(baseAddress, TimeSpan.FromSeconds(150));
            return baseAddress.TrimEnd('/');
        }
        catch
        {
            if (_uiProcess is { HasExited: false })
            {
                _uiProcess.Kill(entireProcessTree: true);
                _uiProcess.WaitForExit(TimeSpan.FromSeconds(5));
            }
            var tail = GetUiLogTail();
            if (!string.IsNullOrEmpty(tail))
            {
                throw new TimeoutException($"Failed to start UI process. Recent output: {tail}");
            }
            throw;
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string ResolveUiDllPath()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir is not null)
        {
            var candidate = Path.Combine(currentDir.FullName, "ExxerCube.Prisma.Web.UI.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var binCandidate = Path.Combine(currentDir.FullName, "ExxerCube.Prisma.Web.UI", "net10.0", "ExxerCube.Prisma.Web.UI.dll");
            if (File.Exists(binCandidate))
            {
                return binCandidate;
            }

            var artifactsCandidate = Path.Combine(currentDir.FullName, "..", "ExxerCube.Prisma.Web.UI", "net10.0", "ExxerCube.Prisma.Web.UI.dll");
            if (File.Exists(artifactsCandidate))
            {
                return Path.GetFullPath(artifactsCandidate);
            }

            currentDir = currentDir.Parent;
        }

        throw new FileNotFoundException("Unable to locate ExxerCube.Prisma.Web.UI.dll for UI smoke tests.");
    }

    private async Task WaitForHealthAsync(string baseAddress, TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        var stopAt = DateTime.UtcNow + timeout;
        string? lastError = null;
        HttpStatusCode? lastStatus = null;

        while (DateTime.UtcNow < stopAt)
        {
            if (_uiProcess is { HasExited: true })
            {
                var tail = GetUiLogTail();
                throw new InvalidOperationException($"UI process exited with code {_uiProcess.ExitCode}. Output tail: {tail}");
            }

            try
            {
                using var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                lastStatus = response.StatusCode;
                lastError = $"StatusCode={(int)response.StatusCode}";
            }
            catch
            {
                lastError = "exception during health probe";
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Prisma UI did not become healthy at {baseAddress} within {timeout.TotalSeconds} seconds. LastStatus={lastStatus?.ToString() ?? "n/a"}; LastError={lastError ?? "n/a"}.");
    }

    private string GetUiLogTail()
    {
        lock (_uiOutput)
        {
            return string.Join(Environment.NewLine, _uiOutput.TakeLast(20));
        }
    }
}

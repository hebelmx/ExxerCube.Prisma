using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

/// <summary>
/// Abstract base for the max-fidelity live end-to-end gate scenarios (issue #16).
/// </summary>
/// <remarks>
/// <para>
/// This base class holds all shared infrastructure used by the max-fidelity gate scenarios:
/// consts, per-instance fields, the <see cref="IAsyncLifetime"/> fixture lifecycle, and every
/// shared helper. Concrete subclasses each own a single <c>[Fact]</c> scenario.
/// </para>
/// <para>
/// <strong>Environment requirements (this machine satisfies all):</strong> Docker (Testcontainers SQL),
/// a Chromium install (<c>playwright install chromium</c>), native Tesseract + tessdata on PATH, and the
/// published simulator under <c>Deployments/Siara.Simulator/app</c>. The test starts the simulator if it
/// is not already running on <c>http://localhost:5001</c>.
/// </para>
/// <para>
/// <strong>What is real here that the fast <see cref="AllRealWireThreeHostE2ETests"/> stubs:</strong> the
/// SIARA browser download, OCR, image quality, fusion, classification, and SQL persistence. Unlike the fast
/// harness (which uses the in-memory TestServer SignalR seam), this gate uses a REAL Kestrel TCP transport
/// for Orion and Athena — SignalR keepalive runs and multi-minute idle download windows cannot silently drop
/// the cross-process <c>DocumentDownloadedEvent</c>.
/// </para>
/// <para>
/// <strong>CI strategy (issue #16):</strong> CI runs ONE gate scenario per <c>dotnet test</c> invocation
/// via <c>--filter-query</c> (each scenario boots its own SQL container + Playwright + 3 hosts, ~6–20 min).
/// Per issue #16, validate the critical full-pipeline scenario alone while iterating. Filter queries:
/// <c>/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit</c>
/// and
/// <c>/*/*/MaxFidelityGatePartialCaseE2ETests/PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete</c>.
/// Both classes share the serialized <c>MaxFidelityGate</c> collection so a full-assembly run never
/// executes them in parallel.
/// </para>
/// </remarks>
public abstract class MaxFidelityGateE2EBase : IAsyncLifetime
{
    protected const string SimulatorUrl = "http://localhost:5001";
    protected const string ValidUsername = "BANAMEX";
    protected const string ValidPassword = "password123";
    protected const string DashboardSelector = "#arrivalRateSlider"; // present only on the authenticated dashboard

    // Shared JWT secret — all three workers sign/validate clearance tokens with this key.
    protected const string SharedJwtSecret = "MAX-FIDELITY-GATE-E2E-SHARED-JWT-SECRET-FOR-TESTS-ONLY-32+";

    // Shared temp directory that acts as the cross-process "shared volume".
    protected readonly string _sharedStorageDir =
        Path.Combine(Path.GetTempPath(), "prisma-maxfidelity-gate-" + Guid.NewGuid().ToString("N"));

    // Per-run isolated journal so the simulator's repeated cases are not deduped across runs.
    protected string JournalPath => Path.Combine(_sharedStorageDir, "journal.txt");

    protected SqlServerContainerFixture? _sql;
    protected string _connectionString = string.Empty;

    // ── Docker-free opt-in: target a LOCAL SQL Server instead of Testcontainers ──
    // When the box has no Docker (e.g. WSL2/Hyper-V down), set PRISMA_GATE_LOCAL_SQL to a *master*
    // connection string for a local SQL instance, e.g.
    //   Server=DESKTOP-FB2ES22\SQL2025;Database=master;Integrated Security=True;TrustServerCertificate=True
    // and the gate provisions/drops an isolated database on that instance instead of a container.
    // When the variable is unset (the default / CI path), the Testcontainers path runs unchanged.
    private static string? LocalSqlMaster => Environment.GetEnvironmentVariable("PRISMA_GATE_LOCAL_SQL");
    private bool _usingLocalSql;
    private string _localIsolatedDbName = string.Empty;

    protected Process? _simulatorProcess;
    protected bool _startedSim;

    // These fields use 'internal' (not 'protected') because GateOrionHost / GateAthenaHost /
    // GateReconciliatorHost are internal types; C# disallows 'protected' fields of internal
    // types on a public class (CS0052). All concrete subclasses live in the same assembly, so
    // 'internal' is identical to 'protected internal' in practice for this test assembly.
    internal GateOrionHost? _orionApp;
    internal GateAthenaHost? _athenaApp;
    internal GateReconciliatorHost? _reconciliatorApp;

    // ── Fixture lifecycle ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(_sharedStorageDir);

        // 1) Start the published SIARA simulator if it is not already up.
        if (!await IsSimulatorRunningAsync())
        {
            await StartSimulatorAsync();
        }

        // 2) Provision a SQL Server + an isolated database for this run.
        //    Default: a real SQL Server via Testcontainers (Docker). Opt-in (Docker-free): a local SQL
        //    instance via PRISMA_GATE_LOCAL_SQL, used when Docker/WSL2 is unavailable on the box.
        var localMaster = LocalSqlMaster;
        if (!string.IsNullOrWhiteSpace(localMaster))
        {
            _usingLocalSql = true;
            _connectionString = await CreateLocalIsolatedDatabaseAsync(localMaster, GetType().Name, ct);
        }
        else
        {
            _sql = new SqlServerContainerFixture();
            await _sql.InitializeAsync();
            _connectionString = await _sql.CreateIsolatedDatabaseAsync(GetType().Name, ct);
        }

        // 3) Provision the schema (AddDatabaseServices does NOT auto-create it).
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        await using var ctx = new PrismaDbContext(options);
        await ctx.Database.EnsureCreatedAsync(ct);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_reconciliatorApp is not null)
        {
            await _reconciliatorApp.DisposeAsync();
        }

        if (_athenaApp is not null)
        {
            await _athenaApp.DisposeAsync();
        }

        if (_orionApp is not null)
        {
            await _orionApp.DisposeAsync();
        }

        if (_sql is not null)
        {
            await _sql.DisposeAsync();
        }

        if (_usingLocalSql)
        {
            await DropLocalIsolatedDatabaseAsync();
        }

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

        try
        {
            if (Directory.Exists(_sharedStorageDir))
            {
                Directory.Delete(_sharedStorageDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    // ── Docker-free local-SQL isolation (mirrors SqlServerContainerFixture) ────────

    /// <summary>
    /// Creates an isolated database on a LOCAL SQL Server instance using the same drop-and-create
    /// isolation pattern as <see cref="SqlServerContainerFixture.CreateIsolatedDatabaseAsync"/>, so the
    /// gate can run end-to-end without Docker. A unique GUID suffix guarantees a clean DB even if a prior
    /// run crashed before teardown.
    /// </summary>
    private async Task<string> CreateLocalIsolatedDatabaseAsync(
        string masterConnectionString, string name, CancellationToken ct)
    {
        var safe = new string((name ?? "Test").Where(char.IsLetterOrDigit).ToArray());
        if (safe.Length == 0)
        {
            safe = "Test";
        }
        if (safe.Length > 64)
        {
            safe = safe[..64];
        }
        _localIsolatedDbName = $"PrismaGate_{safe}_{Guid.NewGuid():N}";

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(masterConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{_localIsolatedDbName}')
            BEGIN
                ALTER DATABASE [{_localIsolatedDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_localIsolatedDbName}];
            END;
            CREATE DATABASE [{_localIsolatedDbName}];";
        await cmd.ExecuteNonQueryAsync(ct);

        return new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = _localIsolatedDbName,
        }.ConnectionString;
    }

    /// <summary>Drops the local isolated database created by <see cref="CreateLocalIsolatedDatabaseAsync"/>.</summary>
    private async Task DropLocalIsolatedDatabaseAsync()
    {
        var localMaster = LocalSqlMaster;
        if (string.IsNullOrWhiteSpace(localMaster) || string.IsNullOrEmpty(_localIsolatedDbName))
        {
            return;
        }

        try
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(localMaster);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{_localIsolatedDbName}')
                BEGIN
                    ALTER DATABASE [{_localIsolatedDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_localIsolatedDbName}];
                END;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort teardown — a leftover PrismaGate_* DB is harmless and GUID-unique.
        }
    }

    // ── Host boot helper (shared by both gate scenarios) ───────────────────────────

    /// <summary>
    /// Starts the three real worker hosts (Orion → Athena → Reconciliator) as single Kestrel hosts
    /// wired to the gate SQL database and the live sim. Each worker receives the connection string via
    /// <c>configureEarly</c>'s <c>AddInMemoryCollection</c>, which fires immediately after
    /// <c>WebApplication.CreateBuilder</c> — BEFORE any inline config reads in <c>Program.BuildApp</c>.
    /// The old process-level env-var hack (<c>ConnectionStrings__DefaultConnection</c>) is no longer needed
    /// because the new <c>BuildApp</c> seam passes configuration in before the eager reads run.
    /// </summary>
    protected async Task BuildThreeHostsWithDbAsync(
        string storageState,
        bool runAthenaPipeline = true,
        CancellationToken ct = default)
    {
        _orionApp = await GateOrionHost.StartAsync(
            SharedJwtSecret, _sharedStorageDir, _connectionString, storageState, JournalPath, ct)
            .ConfigureAwait(false);

        _athenaApp = await GateAthenaHost.StartAsync(
            SharedJwtSecret, _sharedStorageDir, _connectionString, _orionApp.BaseAddress,
            runExtractionPipeline: runAthenaPipeline, ct)
            .ConfigureAwait(false);

        _reconciliatorApp = await GateReconciliatorHost.StartAsync(
            SharedJwtSecret, _sharedStorageDir, _connectionString, _athenaApp.BaseAddress, ct)
            .ConfigureAwait(false);
    }

    // ── Discovery helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Polls the REAL <see cref="ISiaraDocumentSource"/> (resolved from the Orion host) until it lists a case
    /// bundling all three companion formats (PDF + DOCX + XML), exactly as the watch loop does.
    /// </summary>
    protected async Task<SiaraCase> DiscoverFullCompanionCaseAsync(CancellationToken ct)
    {
        await using var discoveryScope = _orionApp!.Services.CreateAsyncScope();
        var source = discoveryScope.ServiceProvider.GetRequiredService<ISiaraDocumentSource>();

        SiaraCase? fullPackage = null;
        for (var attempt = 0; attempt < 18 && fullPackage is null; attempt++)
        {
            var discovered = await source.DiscoverCasesAsync(ct);
            discovered.IsSuccess.ShouldBeTrue($"discovery failed: {string.Join(", ", discovered.Errors)}");
            fullPackage = discovered.Value?.FirstOrDefault(IsFullCompanionPackage);
            if (fullPackage is not null)
            {
                break;
            }

            await Task.Delay(5000, ct);
        }

        fullPackage.ShouldNotBeNull(
            "the live sim should expose at least one case bundling all three companion files (PDF + DOCX + XML) within ~90s");
        return fullPackage!;
    }

    /// <summary>True when a discovered case bundles all three SIARA companion formats (PDF + DOCX + XML).</summary>
    protected static bool IsFullCompanionPackage(SiaraCase c) =>
        c.Files.Any(f => f.Url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) &&
        c.Files.Any(f => f.Url.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) &&
        c.Files.Any(f => f.Url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

    // ── Audit polling ──────────────────────────────────────────────────────────--

    protected async Task<List<AuditRecord>> PollAuditRowsAsync(
        Guid fileId, int minimumRows, TimeSpan timeout, CancellationToken ct)
    {
        var fileIdStr = fileId.ToString();
        var deadline = DateTime.UtcNow + timeout;
        var options = new DbContextOptionsBuilder<PrismaDbContext>().UseSqlServer(_connectionString).Options;

        List<AuditRecord> rows = new();
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await using var ctx = new PrismaDbContext(options);
            rows = await ctx.AuditRecords.Where(a => a.FileId == fileIdStr).ToListAsync(ct);
            if (rows.Count >= minimumRows)
            {
                return rows;
            }

            await Task.Delay(1000, ct);
        }

        return rows;
    }

    // ── Real headless login (captures the credential-free storage-state) ──────────

    protected async Task<string> LoginAndCaptureStorageStateAsync(CancellationToken ct)
    {
        // The headless Playwright login is occasionally flaky on a REPEAT login in the same test process
        // (e.g. NavigateToAsync returns IsSuccess=false) — a transient browser/sim hiccup, not a product
        // failure. Retry a bounded number of times with a fresh browser adapter each attempt; on the final
        // attempt let the Shouldly assertions throw with full detail. Cancellation is never retried.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryLoginAndCaptureStorageStateAsync(ct);
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    protected async Task<string> TryLoginAndCaptureStorageStateAsync(CancellationToken ct)
    {
        var adapter = CreateLoginAdapter();
        try
        {
            (await adapter.LaunchBrowserAsync(ct)).IsSuccess.ShouldBeTrue();
            (await adapter.NavigateToAsync($"{SimulatorUrl}/login", ct)).IsSuccess.ShouldBeTrue();
            (await adapter.WaitForSelectorAsync("#username", 15000, ct)).IsSuccess.ShouldBeTrue();
            (await adapter.FillInputAsync("#username", ValidUsername, ct)).IsSuccess.ShouldBeTrue();
            (await adapter.FillInputAsync("#password", ValidPassword, ct)).IsSuccess.ShouldBeTrue();
            (await adapter.ClickElementAsync("button[type='submit']", ct)).IsSuccess.ShouldBeTrue();
            (await adapter.WaitForSelectorAsync(DashboardSelector, 15000, ct)).IsSuccess.ShouldBeTrue(
                "login should reach the dashboard");

            // Wait for the InteractiveServer circuit to render at least one document link so the sim
            // definitely has cases to discover/download.
            (await adapter.WaitForSelectorAsync("a[href$='.pdf']", 90000, ct)).IsSuccess.ShouldBeTrue(
                "the simulator dashboard should render at least one PDF document link within 90s");

            var authState = await adapter.ExportStorageStateAsync(ct);
            authState.IsSuccess.ShouldBeTrue($"storage-state capture failed: {string.Join(", ", authState.Errors)}");
            return authState.Value!;
        }
        finally
        {
            await adapter.CloseBrowserAsync(ct);
        }
    }

    protected static PlaywrightBrowserAutomationAdapter CreateLoginAdapter() =>
        new(
            NullLogger<PlaywrightBrowserAutomationAdapter>.Instance,
            Options.Create(new BrowserAutomationOptions
            {
                Headless = true,
                BrowserLaunchTimeoutMs = 60000,
                PageTimeoutMs = 30000,
                IgnoreHttpsErrors = true, // the simulator serves https with a self-signed dev certificate
            }));

    // ── Bounded await helper ─────────────────────────────────────────────────────

    protected static async Task<T> AwaitOrFailAsync<T>(Task<T> task, TimeSpan timeout, string what, CancellationToken ct)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout, ct));
        winner.ShouldBe(task, $"{what} must arrive within {timeout.TotalSeconds:N0}s");
        return await task;
    }

    // ── Bounded connect-wait (real TCP SignalR transport) ────────────────────────

    protected async Task WaitUntilHubClientsConnectedAsync(CancellationToken ct)
    {
        // Probe the REAL Kestrel loopback addresses — no HttpMessageHandlerFactory needed.
        var orionIngestionUrl = new Uri(_orionApp!.BaseAddress, "hubs/ingestion").ToString();
        var athenaReconciliationUrl = new Uri(_athenaApp!.BaseAddress, "hubs/reconciliation").ToString();

        await ProbeHubUntilConnectedAsync(
            hubUrl: orionIngestionUrl,
            jwtToken: MintToken("athena-extractor-gate-probe", "Extract"),
            ct: ct);

        await ProbeHubUntilConnectedAsync(
            hubUrl: athenaReconciliationUrl,
            jwtToken: MintToken("reconciliator-gate-probe", "Reconcile"),
            ct: ct);

        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
    }

    protected static async Task ProbeHubUntilConnectedAsync(
        string hubUrl,
        string jwtToken,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            var conn = new HubConnectionBuilder()
                .WithUrl(hubUrl, opts =>
                {
                    // Real TCP transport — no HttpMessageHandlerFactory override.
                    // JWT is supplied via the access_token query-string hook, which works over
                    // real loopback WebSocket connections exactly as it does in production.
                    opts.AccessTokenProvider = () => Task.FromResult<string?>(jwtToken);
                })
                .Build();

            try
            {
                await conn.StartAsync(timeoutCts.Token).ConfigureAwait(false);
                await conn.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                await conn.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch
            {
                await conn.DisposeAsync().ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(150, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // ── JWT helper ────────────────────────────────────────────────────────────────

    internal static string MintToken(string actorId, string clearance, Guid? fileId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SharedJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, actorId),
            new Claim("actor_type", "ServiceAccount"),
            new Claim("clearance", clearance),
            new Claim("file_id", (fileId ?? Guid.Empty).ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
        };

        var token = new JwtSecurityToken(
            issuer: "prisma-pipeline",
            audience: "prisma-pipeline",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Simulator lifecycle ───────────────────────────────────────────────────────

    protected static async Task<bool> IsSimulatorRunningAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await http.GetAsync(SimulatorUrl);
            return response.IsSuccessStatusCode || (int)response.StatusCode == 302;
        }
        catch
        {
            return false;
        }
    }

    protected async Task StartSimulatorAsync()
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

    protected static string? ResolveSimulatorPath()
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

/// <summary>
/// xUnit collection definition that serializes the two heavy gate scenarios so they never run in parallel
/// when the full assembly is executed. Each scenario still gets its own fixture instance (and its own
/// <c>SqlServerContainerFixture</c>), so they remain fully isolated; the collection merely prevents
/// simultaneous startup contention that caused the 15-min timeout flake (issue #16).
/// </summary>
/// <remarks>
/// The preferred CI path is one scenario per <c>dotnet test</c> invocation — see the CI doc on
/// <see cref="MaxFidelityGateE2EBase"/>. This collection is the safety net for full-assembly runs.
/// </remarks>
[CollectionDefinition("MaxFidelityGate", DisableParallelization = true)]
public sealed class MaxFidelityGateCollection { }

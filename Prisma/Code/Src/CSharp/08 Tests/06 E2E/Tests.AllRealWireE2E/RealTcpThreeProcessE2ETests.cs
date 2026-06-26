using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

// ── OS-process worker launcher ────────────────────────────────────────────────────────────────────
// Helper that starts a single worker as a child process via `dotnet <dll>`, captures its stdout/stderr
// for diagnostics, and polls /health/live until the worker is accepting connections.

/// <summary>
/// Launches a single Prisma worker as a child <c>dotnet &lt;dll&gt;</c> process and manages its
/// lifecycle. Stdout/stderr are captured asynchronously so failed assertions can surface worker logs.
/// </summary>
/// <remarks>
/// Used exclusively by <see cref="RealTcpThreeProcessE2ETests"/> to prove the three-process pipeline
/// works over real TCP between real OS processes.  The existing max-fidelity gate uses in-process
/// Kestrel hosts; this launcher provides the OS-process boundary required by E2-S1 (gaps P6/D10).
/// </remarks>
internal sealed class WorkerProcessLauncher : IAsyncDisposable
{
    private Process? _process;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly object _bufferLock = new();

    /// <summary>Gets the loopback port this worker is listening on.</summary>
    public int Port { get; }

    /// <summary>Gets a short display name used in diagnostic output.</summary>
    public string WorkerName { get; }

    private WorkerProcessLauncher(int port, string workerName)
    {
        Port = port;
        WorkerName = workerName;
    }

    /// <summary>
    /// Allocates a free loopback TCP port by temporarily binding a <see cref="TcpListener"/> on
    /// port 0, reading the kernel-assigned port, then stopping the listener.  There is a small
    /// TOCTOU window between release and the child process binding; this is acceptable for test use
    /// where loopback-port collisions are very rare.
    /// </summary>
    public static int AllocateFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Resolves the absolute path to a worker DLL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Since the test project has <c>ProjectReference</c> entries for all three worker projects, their
    /// DLLs are copied into the test output directory (<see cref="AppContext.BaseDirectory"/>).  This
    /// is the fast path.  A walk-up directory scan is also attempted so the method works when run from
    /// an IDE that does not copy referenced-project outputs into the base directory.
    /// </para>
    /// <para>
    /// DLL names match the project file names (the default <c>AssemblyName</c>):
    /// <list type="bullet">
    ///   <item><c>ExxerCube.Prisma.Orion.Worker.dll</c></item>
    ///   <item><c>ExxerCube.Prisma.Athena.Worker.dll</c></item>
    ///   <item><c>ExxerCube.Prisma.Reconciliator.Worker.dll</c></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="dllName">File name including extension.</param>
    /// <returns>Absolute path to the resolved DLL.</returns>
    /// <exception cref="FileNotFoundException">DLL not found anywhere reachable.</exception>
    public static string ResolveWorkerDllPath(string dllName)
    {
        // Fast path: copied alongside the test assembly by the ProjectReference build step.
        var direct = Path.Combine(AppContext.BaseDirectory, dllName);
        if (File.Exists(direct))
        {
            return direct;
        }

        // Walk-up fallback: mirrors MaxFidelityGateE2EBase.ResolveSimulatorPath.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var config in new[] { "Release", "Debug" })
            {
                var candidate = Path.Combine(dir.FullName, "bin", config, "net10.0", dllName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Worker DLL '{dllName}' not found in AppContext.BaseDirectory ('{AppContext.BaseDirectory}') or " +
            $"any ancestor bin/ directory.  Ensure the solution has been built and the test project has a " +
            $"<ProjectReference> to the worker project.");
    }

    /// <summary>
    /// Starts the worker as a child process and waits until <c>/health/live</c> returns HTTP 200.
    /// </summary>
    /// <param name="dllPath">Absolute path to the worker DLL.</param>
    /// <param name="port">Loopback port to bind (passed via <c>ASPNETCORE_URLS</c>).</param>
    /// <param name="envVars">
    /// Worker-specific configuration overrides (ASP.NET Core <c>__</c>-separated key–value pairs).
    /// These are merged on top of the inherited current-process environment.
    /// </param>
    /// <param name="workerName">Short display name used in captured logs and diagnostics.</param>
    /// <param name="workingDirectory">
    /// Process working directory.  For Orion this is the per-run subdirectory that receives
    /// <c>journal.txt</c>; for Athena/Reconciliator it is the test output directory.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<WorkerProcessLauncher> StartAsync(
        string dllPath,
        int port,
        IReadOnlyDictionary<string, string> envVars,
        string workerName,
        string workingDirectory,
        CancellationToken ct)
    {
        var launcher = new WorkerProcessLauncher(port, workerName);
        await launcher.LaunchAsync(dllPath, envVars, workingDirectory, ct).ConfigureAwait(false);
        return launcher;
    }

    private async Task LaunchAsync(
        string dllPath,
        IReadOnlyDictionary<string, string> envVars,
        string workingDirectory,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        startInfo.ArgumentList.Add(dllPath);

        // ProcessStartInfo.Environment is lazily initialized from the CURRENT process's environment
        // (see .NET source).  We only need to add/overwrite the keys that differ for this worker.
        // This preserves PATH, LD_LIBRARY_PATH, TESSDATA_PREFIX, etc. which the native OCR engine needs.
        foreach (var (key, value) in envVars)
        {
            startInfo.Environment[key] = value;
        }

        // Bind Kestrel to the allocated loopback port.
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{Port}";

        // Ensure Serilog emits to the console so captured stdout surfaces worker activity.
        // These env-var keys are read by Serilog.Settings.Configuration via ASP.NET Core's __ convention.
        // If the worker's appsettings.json is not in the content root (common for ProjectReference builds),
        // this is the only Serilog configuration present and gives us at least warning-level output.
        startInfo.Environment["Serilog__Using__0"] = "Serilog.Sinks.Console";
        startInfo.Environment["Serilog__WriteTo__0__Name"] = "Console";
        startInfo.Environment["Serilog__MinimumLevel__Default"] = "Warning";

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_bufferLock)
                {
                    _stdout.AppendLine($"[{WorkerName}] {e.Data}");
                }
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_bufferLock)
                {
                    _stderr.AppendLine($"[{WorkerName}:ERR] {e.Data}");
                }
            }
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException($"Failed to start worker process for '{WorkerName}'.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await PollHealthAsync(Port, timeout: TimeSpan.FromSeconds(120), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls <c>http://127.0.0.1:{port}/health/live</c> until it returns HTTP 200 or the timeout
    /// elapses.  Returns normally on success; throws <see cref="TimeoutException"/> on failure.
    /// </summary>
    internal static async Task PollHealthAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var url = $"http://127.0.0.1:{port}/health/live";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            try
            {
                var response = await http.GetAsync(url, timeoutCts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Worker not yet accepting HTTP connections — keep polling.
                _ = ex;
            }

            try
            {
                await Task.Delay(500, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"Worker '{url}' did not return HTTP 200 within {timeout.TotalSeconds:N0} s.");
    }

    /// <summary>
    /// Sends SIGKILL to the process tree and waits up to 5 s for exit.  Best-effort; exceptions are
    /// swallowed.
    /// </summary>
    public void Kill()
    {
        try
        {
            if (_process is { HasExited: false } proc)
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5_000);
            }
        }
        catch
        {
            // Best-effort teardown.
        }
    }

    /// <summary>
    /// Returns a formatted string containing all captured stdout and stderr lines. Include this in
    /// Shouldly assertion failure messages so the orchestrator can diagnose remote failures without a
    /// live debugging session.
    /// </summary>
    public string GetCapturedLogs()
    {
        lock (_bufferLock)
        {
            return $"=== {WorkerName} stdout ===\n{_stdout}\n=== {WorkerName} stderr ===\n{_stderr}";
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Kill();
        _process?.Dispose();
        _process = null;
        return ValueTask.CompletedTask;
    }
}

// ── OS-process gate tests ─────────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies the three-process Prisma pipeline over <strong>real TCP</strong> between
/// <strong>real OS processes</strong> — each worker has its own address space, PID, and signal
/// boundary. Closes gaps P6 (OS-process separation) and D10 (hub reconnect after OS-level failure).
/// </summary>
/// <remarks>
/// <para>
/// The existing <see cref="MaxFidelityGateFullPipelineE2ETests"/> and
/// <see cref="MaxFidelityGatePartialCaseE2ETests"/> boot each worker via
/// <c>Program.BuildApp()</c> — same OS process, three in-process Kestrel hosts. This class goes
/// one step further by launching the workers as separate <c>dotnet &lt;dll&gt;</c> child processes
/// so process-level isolation, PID-stamped audit rows, and hub reconnect after kill are genuinely
/// exercised.
/// </para>
/// <para>
/// <strong>DLL resolution:</strong> The test project has <c>ProjectReference</c> entries for all
/// three worker projects, so their DLLs are copied into the test output directory alongside the
/// test assembly. <see cref="WorkerProcessLauncher.ResolveWorkerDllPath"/> locates them there.
/// </para>
/// <para>
/// <strong>Configuration:</strong> Every worker receives its configuration exclusively through
/// environment variables using ASP.NET Core's <c>__</c>-separator convention
/// (e.g. <c>ConnectionStrings__DefaultConnection</c>).  No <c>appsettings.json</c> file is
/// required in the child process's content root (the default values from appsettings are overridden
/// completely by env vars).
/// </para>
/// <para>
/// <strong>Watch-loop scan interval:</strong> <c>OrionWatchLoop__PollInterval = "00:02:00"</c>
/// (two minutes).  A long interval gives Athena time to connect and reconnect between scans,
/// avoiding race conditions in the reconnect scenario.
/// </para>
/// <para>
/// <strong>Running these tests:</strong> the same pre-conditions apply as for
/// <see cref="MaxFidelityGateFullPipelineE2ETests"/> — Docker (Testcontainers SQL), Chromium
/// (<c>playwright install chromium</c>), native Tesseract + tessdata on PATH, the published
/// SIARA simulator.  Use the filter:
/// <c>/*/*/RealTcpThreeProcessE2ETests/*</c>
/// </para>
/// </remarks>
[Trait("category", "slow")]
[Trait("category", "e2e")]
[Trait("Category", "MaxFidelityGate")]
[Collection("MaxFidelityGate")]
public sealed class RealTcpThreeProcessE2ETests : MaxFidelityGateE2EBase
{
    // ── Worker DLL names ─────────────────────────────────────────────────────────
    // These match the .csproj file names (the default AssemblyName).
    private const string OrionDllName = "ExxerCube.Prisma.Orion.Worker.dll";
    private const string AthenaDllName = "ExxerCube.Prisma.Athena.Worker.dll";
    private const string ReconciliatorDllName = "ExxerCube.Prisma.Reconciliator.Worker.dll";

    // ── Watch-loop cadence ───────────────────────────────────────────────────────
    // Two-minute scan interval gives a generous window for Athena to reconnect before Orion re-scans,
    // keeping the reconnect test reliable without timing sensitivity.
    private const string OrionPollInterval = "00:02:00";

    // ── Tracked OS-process launchers ─────────────────────────────────────────────
    // OnExtendedDisposeAsync kills these before the base class removes _sharedStorageDir.
    private readonly List<WorkerProcessLauncher> _workers = new();

    // ── Lifetime hook ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override async ValueTask OnExtendedDisposeAsync()
    {
        // Kill all OS worker processes BEFORE the base class deletes _sharedStorageDir so
        // the workers release their file handles on the shared volume before we remove it.
        foreach (var w in _workers)
        {
            await w.DisposeAsync().ConfigureAwait(false);
        }

        _workers.Clear();
    }

    // ── Environment-variable builders ─────────────────────────────────────────────

    /// <summary>
    /// Builds the env-var dictionary keys shared by all three workers.
    /// Each key uses the ASP.NET Core <c>__</c>-separator convention so the child process reads
    /// them as structured config (e.g. <c>ProcessIdentity__JwtSecret</c> → <c>ProcessIdentity:JwtSecret</c>).
    /// </summary>
    private Dictionary<string, string> BuildCommonEnvVars(string actorId, string clearance) =>
        new()
        {
            // Database ──────────────────────────────────────────────────────────────
            ["ConnectionStrings__DefaultConnection"] = _connectionString,

            // JWT process identity ──────────────────────────────────────────────────
            ["ProcessIdentity__JwtSecret"]    = SharedJwtSecret,
            ["ProcessIdentity__JwtIssuer"]    = "prisma-pipeline",
            ["ProcessIdentity__JwtAudience"]  = "prisma-pipeline",
            ["ProcessIdentity__TokenLifetime"] = "01:00:00",
            ["ProcessIdentity__Clearance"]    = clearance,

            // Actor identity ────────────────────────────────────────────────────────
            ["Siara__Actor__ActorId"]     = actorId,
            ["Siara__Actor__DisplayName"] = actorId,

            // Shared filesystem volume (ADR-011) ────────────────────────────────────
            ["Storage__BasePath"] = _sharedStorageDir,
        };

    private Dictionary<string, string> BuildOrionEnvVars(string storageState)
    {
        var v = BuildCommonEnvVars("orion-downloader-proctcp", "Download");

        // SIARA browser auth passthrough (mirrors GateOrionHost configuration)
        v["Siara__AuthMode"]                        = "SessionPassthrough";
        v["Siara__Passthrough__Transport"]          = "StorageState";
        v["Siara__Passthrough__StorageStateRef"]    = storageState;
        v["Siara__Passthrough__DashboardUrl"]       = SimulatorUrl + "/";
        v["Siara__Passthrough__PostLoginSelector"]  = DashboardSelector;
        v["NavigationTargets__SiaraUrl"]            = SimulatorUrl + "/";

        // Headless Playwright (same as gate hosts)
        v["BrowserAutomation__Headless"]           = "true";
        v["BrowserAutomation__IgnoreHttpsErrors"]  = "true";
        v["BrowserAutomation__PageTimeoutMs"]      = "60000";

        // I/O-race mitigation: 250 ms flush delay after writing downloaded bytes (ADR-011)
        v["Ingestion__PostWriteFlushDelayMs"] = "250";

        // Watch-loop cadence: 2-minute interval between autonomous scan cycles
        v["OrionWatchLoop__PollInterval"] = OrionPollInterval;

        return v;
    }

    private Dictionary<string, string> BuildAthenaEnvVars(int orionPort)
    {
        var v = BuildCommonEnvVars("athena-extractor-proctcp", "Extract");

        // Connect to Orion's real TCP ingestion hub (no HttpMessageHandlerFactory override)
        v["Ingestion__HubUrl"]      = $"http://127.0.0.1:{orionPort}/hubs/ingestion";
        v["Ingestion__ReconnectDelay"] = "00:00:00.500";

        return v;
    }

    private Dictionary<string, string> BuildReconciliatorEnvVars(int athenaPort)
    {
        var v = BuildCommonEnvVars("reconciliator-proctcp", "Reconcile");

        // Connect to Athena's real TCP reconciliation hub
        v["Reconciliation__HubUrl"]        = $"http://127.0.0.1:{athenaPort}/hubs/reconciliation";
        v["Reconciliation__ReconnectDelay"] = "00:00:00.500";

        return v;
    }

    // ── Worker start helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Starts an Orion Downloader worker process and waits until it is healthy.
    /// </summary>
    /// <param name="storageState">Playwright storage-state JSON (credential-free SIARA auth).</param>
    /// <param name="port">Pre-allocated loopback port.</param>
    /// <param name="workingDirectory">
    /// Process working directory; <c>FileIngestionJournal</c> writes <c>journal.txt</c> here
    /// (it uses <c>Directory.GetCurrentDirectory()</c> as the default journal path).
    /// Pass a unique directory per test run to ensure isolated journals.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<WorkerProcessLauncher> StartOrionAsync(
        string storageState,
        int port,
        string workingDirectory,
        CancellationToken ct)
    {
        Directory.CreateDirectory(workingDirectory);

        var dllPath = WorkerProcessLauncher.ResolveWorkerDllPath(OrionDllName);
        var launcher = await WorkerProcessLauncher.StartAsync(
            dllPath,
            port,
            BuildOrionEnvVars(storageState),
            workerName: "Orion",
            workingDirectory: workingDirectory,
            ct).ConfigureAwait(false);

        _workers.Add(launcher);
        return launcher;
    }

    /// <summary>Starts an Athena Extractor worker process and waits until it is healthy.</summary>
    private async Task<WorkerProcessLauncher> StartAthenaAsync(
        int orionPort,
        int athenaPort,
        CancellationToken ct)
    {
        var dllPath = WorkerProcessLauncher.ResolveWorkerDllPath(AthenaDllName);
        var launcher = await WorkerProcessLauncher.StartAsync(
            dllPath,
            athenaPort,
            BuildAthenaEnvVars(orionPort),
            workerName: "Athena",
            workingDirectory: AppContext.BaseDirectory,
            ct).ConfigureAwait(false);

        _workers.Add(launcher);
        return launcher;
    }

    /// <summary>Starts a Reconciliator worker process and waits until it is healthy.</summary>
    private async Task<WorkerProcessLauncher> StartReconciliatorAsync(
        int athenaPort,
        int reconciliatorPort,
        CancellationToken ct)
    {
        var dllPath = WorkerProcessLauncher.ResolveWorkerDllPath(ReconciliatorDllName);
        var launcher = await WorkerProcessLauncher.StartAsync(
            dllPath,
            reconciliatorPort,
            BuildReconciliatorEnvVars(athenaPort),
            workerName: "Reconciliator",
            workingDirectory: AppContext.BaseDirectory,
            ct).ConfigureAwait(false);

        _workers.Add(launcher);
        return launcher;
    }

    // ── SQL helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Polls <c>AuditRecords</c> (ALL rows — no FileId filter) until at least
    /// <paramref name="minimumRows"/> are present or the timeout elapses.
    /// Used in OS-process tests where the FileId is not directly accessible to the test body.
    /// </summary>
    private async Task<List<AuditRecord>> PollAllAuditRowsAsync(
        int minimumRows,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        List<AuditRecord> rows = new();
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await using var ctx = new PrismaDbContext(dbOptions);
            rows = await ctx.AuditRecords.ToListAsync(ct).ConfigureAwait(false);
            if (rows.Count >= minimumRows)
            {
                return rows;
            }

            await Task.Delay(1_000, ct).ConfigureAwait(false);
        }

        return rows;
    }

    // ── Diagnostic helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Concatenates all captured worker stdout/stderr.  Append to Shouldly assertion failure
    /// messages so the orchestrator can diagnose failures without re-running with a debugger.
    /// </summary>
    private string WorkerDiagnostics() =>
        string.Join("\n\n", _workers.Select(w => w.GetCapturedLogs()));

    // ── Poll-for-SIRO helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Polls the shared storage volume for any <c>*.siro.xml</c> file until one appears or
    /// <paramref name="timeout"/> elapses.
    /// </summary>
    private async Task<string[]> PollForSiroXmlAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var files = Directory.GetFiles(_sharedStorageDir, "*.siro.xml", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files;
            }

            await Task.Delay(5_000, ct).ConfigureAwait(false);
        }

        return Array.Empty<string>();
    }

    // ── SIRO XML structure assertion ──────────────────────────────────────────────

    private static void AssertSiroXmlStructure(string siroFilePath, string contextMessage)
    {
        System.Xml.Linq.XNamespace siroNs = "http://siro.regulatory.namespace";
        var siroDoc = System.Xml.Linq.XDocument.Load(siroFilePath);

        siroDoc.Root!.Name.ShouldBe(
            siroNs + "SiroResponse",
            $"SIRO XML root must be {{http://siro.regulatory.namespace}}SiroResponse. {contextMessage}");

        siroDoc.Root.Element(siroNs + "NumeroExpediente")!.Value
            .ShouldNotBeNullOrWhiteSpace(
                $"NumeroExpediente must be populated in the SIRO XML. {contextMessage}");

        siroDoc.Root.Element(siroNs + "NumeroOficio")!.Value
            .ShouldNotBeNullOrWhiteSpace(
                $"NumeroOficio must be populated in the SIRO XML. {contextMessage}");
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // Fact 1 — Happy-path: three real OS processes drive a case through the pipeline
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proves that a real SIARA case (live download → OCR → fusion → classification → SIRO export)
    /// traverses the complete three-process pipeline when all three workers are launched as
    /// <strong>separate OS processes</strong> communicating over <strong>real TCP SignalR</strong>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gap closed: <strong>P6 — OS-process separation</strong>.  The existing max-fidelity gate
    /// runs in-process Kestrel; this test uses <c>Process.Start("dotnet", …)</c> so each worker
    /// has its own address space, OS PID, and signal boundary — closer to production than in-process.
    /// </para>
    /// <para>
    /// <strong>Startup order:</strong> Orion → Athena → Reconciliator.  Starting Athena immediately
    /// after Orion means Athena's <c>SiaraIngestionHubClient</c> is connected before Orion's
    /// autonomous watch loop completes its first scan.  With a 2-minute poll interval, if the first
    /// scan races ahead of Athena's connection, the second scan (2 min later) delivers the broadcast
    /// to the already-connected Athena — still well within the 25-minute test timeout.
    /// </para>
    /// <para>
    /// <strong>Key assertions:</strong>
    /// <list type="bullet">
    ///   <item>A <c>*.siro.xml</c> file appears in the shared storage volume.</item>
    ///   <item>The SIRO XML has valid structure: root <c>SiroResponse</c>, non-empty
    ///   <c>NumeroExpediente</c> and <c>NumeroOficio</c> elements.</item>
    ///   <item>At least two distinct <c>ProcessId</c> values appear in the SQL audit table —
    ///   proving the pipeline traversed real OS-process boundaries.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [Fact(Timeout = 1_500_000)] // 25-min hard cap
    public async Task ThreeRealProcesses_DriveCaseThroughPipeline_WritesSiroXmlAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless login → credential-free storage-state ───────────
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty(
            "the simulator login must yield an authenticated storage-state before launching workers");

        // ── STEP 2: Allocate three free loopback ports ────────────────────────────
        var orionPort        = WorkerProcessLauncher.AllocateFreePort();
        var athenaPort       = WorkerProcessLauncher.AllocateFreePort();
        var reconciliatorPort = WorkerProcessLauncher.AllocateFreePort();

        // ── STEP 3: Start workers in dependency order ─────────────────────────────
        // Orion exposes the ingestion hub that Athena connects to.
        // Athena exposes the reconciliation hub that Reconciliator connects to.
        // Starting them in this order minimises hub-connect retries.
        var orionWorkDir = Path.Combine(_sharedStorageDir, "orion-work");
        await StartOrionAsync(storageState, orionPort, orionWorkDir, ct);
        await StartAthenaAsync(orionPort, athenaPort, ct);
        await StartReconciliatorAsync(athenaPort, reconciliatorPort, ct);

        // ── STEP 4: Brief settle — let hub clients connect before the first scan ──
        // /health/live returning 200 proves Kestrel is bound; the SignalR hub clients
        // connect asynchronously in background hosted services a moment later.
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        // ── STEP 5: Poll shared storage for a SIRO XML ───────────────────────────
        // Orion's watch loop discovers cases from the live sim, downloads them, and
        // broadcasts DocumentDownloadedEvent over the TCP ingestion hub.  Athena runs
        // the OCR → fusion pipeline and broadcasts ExtractionCompletedEvent over the
        // TCP reconciliation hub.  Reconciliator classifies and exports, writing the
        // SIRO XML to the shared volume.
        //
        // Generous timeout accounts for: Playwright browser login + sim discovery +
        // OCR (Tesseract) + 2-min watch-loop scan interval + Testcontainers SQL write.
        var siroFiles = await PollForSiroXmlAsync(TimeSpan.FromMinutes(15), ct);

        siroFiles.Length.ShouldBeGreaterThanOrEqualTo(1,
            "the Reconciliator OS process must have written a SIRO XML file to shared storage — " +
            "check that all three workers started cleanly and that the watch loop completed a scan.\n\n" +
            WorkerDiagnostics());

        // ── STEP 6: SIRO XML structure assertions ─────────────────────────────────
        AssertSiroXmlStructure(siroFiles[0], WorkerDiagnostics());

        // ── STEP 7: Cross-process audit persistence ───────────────────────────────
        // All three workers write audit rows to the same Testcontainers SQL Server.
        // At least two distinct ProcessId values prove the pipeline crossed real OS-process
        // boundaries (Orion PID ≠ Athena PID ≠ Reconciliator PID).
        var auditRows = await PollAllAuditRowsAsync(
            minimumRows: 2,
            timeout: TimeSpan.FromSeconds(60),
            ct);

        auditRows.ShouldNotBeEmpty(
            "real audit rows must have been persisted to SQL by the OS-process workers.\n\n" +
            WorkerDiagnostics());

        var distinctProcesses = auditRows
            .Where(a => !string.IsNullOrWhiteSpace(a.ProcessId))
            .Select(a => a.ProcessId)
            .Distinct()
            .Count();

        distinctProcesses.ShouldBeGreaterThanOrEqualTo(2,
            "audit rows must carry at least two distinct ProcessId values, proving the pipeline " +
            "ran across separate OS processes (Orion PID ≠ Athena PID).\n\n" +
            WorkerDiagnostics());
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // Fact 2 — Hub reconnect: kill Athena, restart Orion (fresh journal) + Athena,
    //          verify pipeline still completes
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that the pipeline recovers from an Athena OS-process failure:
    /// Athena is killed after becoming healthy, Orion is restarted with a fresh journal, Athena is
    /// restarted and reconnects to the new Orion hub — and a complete case then flows through the
    /// entire pipeline, writing a SIRO XML and audit rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gap closed: <strong>D10 — hub reconnect after OS-level process failure</strong>.
    /// </para>
    /// <para>
    /// <strong>Design rationale:</strong> <c>FileIngestionJournal</c> keeps a <c>ConcurrentDictionary</c>
    /// in-memory cache that is loaded from disk at startup but never reloaded at runtime.  Truncating
    /// the journal file on disk therefore does NOT cause a live Orion process to re-download already-seen
    /// cases.  The only reliable way to clear the in-memory cache is a process restart.  This test
    /// therefore restarts BOTH Athena AND Orion (on the same ports) after the initial kill, giving each
    /// a fresh journal and proving the full hub-reconnect path:
    /// Athena-new → connect → Orion-new hub → broadcast → Athena-new → OCR → Reconciliator → SIRO XML.
    /// </para>
    /// <para>
    /// <strong>Reconnect proof:</strong> Athena's <c>SiaraIngestionHubClient</c> reconnects to Orion's
    /// hub using the configured <c>Ingestion:HubUrl</c>.  After the restart the URL is unchanged
    /// (same loopback port), so the hub client connects immediately with the 500 ms
    /// <c>ReconnectDelay</c> — no manual reconnect trigger is needed.
    /// </para>
    /// <para>
    /// <strong>Key assertions (post-reconnect):</strong>
    /// <list type="bullet">
    ///   <item>A new <c>*.siro.xml</c> appears after the restart sequence completes.</item>
    ///   <item>At least two distinct <c>ProcessId</c> values appear in audit — proving the post-reconnect
    ///   pipeline ran in real processes, not in-process stubs.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [Fact(Timeout = 1_500_000)] // 25-min hard cap
    public async Task AthenaProcessRestartMidRun_PipelineStillCompletes()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless login → storage-state ───────────────────────────
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty(
            "the simulator login must yield an authenticated storage-state before launching workers");

        // ── STEP 2: Allocate fixed loopback ports ─────────────────────────────────
        // Ports are reused across the kill/restart cycle so the hub URLs are stable.
        var orionPort         = WorkerProcessLauncher.AllocateFreePort();
        var athenaPort        = WorkerProcessLauncher.AllocateFreePort();
        var reconciliatorPort = WorkerProcessLauncher.AllocateFreePort();

        // ── STEP 3: Start the initial three workers ───────────────────────────────
        // Orion's journal.txt goes into a unique subdirectory ("orion-work-1") so it
        // is easy to identify and so the second Orion instance gets its own directory.
        var orionWorkDir1 = Path.Combine(_sharedStorageDir, "orion-work-1");
        var orionLauncher = await StartOrionAsync(storageState, orionPort, orionWorkDir1, ct);
        var athenaLauncher = await StartAthenaAsync(orionPort, athenaPort, ct);
        await StartReconciliatorAsync(athenaPort, reconciliatorPort, ct);

        // All three workers are healthy: Kestrel is bound and background hosted services are
        // starting.  Athena's SiaraIngestionHubClient is connecting to Orion's hub over TCP.

        // ── STEP 4: Kill Athena (mid-startup, before any case completes) ──────────
        // We remove Athena from the _workers list before killing it so we can add the
        // restarted Athena as a new tracked launcher below.
        _workers.Remove(athenaLauncher);
        athenaLauncher.Kill();
        await athenaLauncher.DisposeAsync();

        // Brief wait: gives the OS time to release the loopback port and process-tree
        // resources before the new process binds to the same address.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        // ── STEP 5: Kill Orion and restart it with a FRESH journal ────────────────
        // FileIngestionJournal holds its entire state in an in-memory ConcurrentDictionary
        // loaded at startup.  Truncating the file has no effect on the live process cache.
        // Restarting Orion with a NEW working directory (orion-work-2, which is empty) gives
        // it a clean journal so the next watch-loop scan re-downloads and re-broadcasts the
        // case to the reconnected Athena.
        _workers.Remove(orionLauncher);
        orionLauncher.Kill();
        await orionLauncher.DisposeAsync();

        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        // ── STEP 6: Restart Orion (same port, fresh journal directory) ────────────
        var orionWorkDir2 = Path.Combine(_sharedStorageDir, "orion-work-2");
        await StartOrionAsync(storageState, orionPort, orionWorkDir2, ct);

        // ── STEP 7: Restart Athena (same port) → reconnect to new Orion hub ───────
        // SiaraIngestionHubClient inside the new Athena process connects to
        //   http://127.0.0.1:{orionPort}/hubs/ingestion
        // which is now served by the freshly restarted Orion.  The ReconnectDelay of
        // 500 ms means the connection is established very quickly after process start.
        await StartAthenaAsync(orionPort, athenaPort, ct);

        // The new Athena is healthy (STEP 7 polls /health/live): this proves the
        // SiaraIngestionHubClient RECONNECTED to the Orion hub.  If hub connection
        // were broken, the health probe would eventually timeout and the test would
        // fail with a clear TimeoutException from PollHealthAsync.

        // ── STEP 8: Brief settle ──────────────────────────────────────────────────
        // Give Athena's hub client a moment to complete the SignalR handshake before
        // Orion's first scan cycle runs and broadcasts.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        // ── STEP 9: Poll for a SIRO XML written after the reconnect ──────────────
        // Orion's first scan (runs immediately on startup) discovers the SIARA case,
        // downloads it (fresh journal → no duplicate), and broadcasts
        // DocumentDownloadedEvent over the TCP ingestion hub to the reconnected Athena.
        // Athena runs OCR → fusion → handoff.  Reconciliator classifies and exports.
        //
        // We poll the shared volume that BOTH the first and second Orion runs use
        // (same _sharedStorageDir / Storage:BasePath).  Any *.siro.xml appearing here
        // after the restart sequence was written post-reconnect.
        var siroFiles = await PollForSiroXmlAsync(TimeSpan.FromMinutes(15), ct);

        siroFiles.Length.ShouldBeGreaterThanOrEqualTo(1,
            "the pipeline must complete (writing a SIRO XML) after Athena reconnects to the " +
            "restarted Orion hub.  If this fails, check: hub reconnect in SiaraIngestionHubClient, " +
            "fresh-journal re-download in Orion, and ExtractionCompletedEvent delivery to Reconciliator.\n\n" +
            WorkerDiagnostics());

        // ── STEP 10: SIRO XML structure sanity ───────────────────────────────────
        AssertSiroXmlStructure(siroFiles[0], WorkerDiagnostics());

        // ── STEP 11: Audit rows with distinct ProcessIds ──────────────────────────
        // The restarted Orion and Athena have different PIDs from the original processes.
        // At least two distinct ProcessId values prove the pipeline crossed real OS-process
        // boundaries after the restart-and-reconnect sequence.
        var auditRows = await PollAllAuditRowsAsync(
            minimumRows: 2,
            timeout: TimeSpan.FromSeconds(60),
            ct);

        auditRows.ShouldNotBeEmpty(
            "audit rows must be persisted to SQL by the restarted OS-process workers.\n\n" +
            WorkerDiagnostics());

        var distinctProcesses = auditRows
            .Where(a => !string.IsNullOrWhiteSpace(a.ProcessId))
            .Select(a => a.ProcessId)
            .Distinct()
            .Count();

        distinctProcesses.ShouldBeGreaterThanOrEqualTo(2,
            "audit rows must carry at least two distinct ProcessId values even after the restart — " +
            "proving Orion and Athena ran as separate OS processes.\n\n" +
            WorkerDiagnostics());
    }
}

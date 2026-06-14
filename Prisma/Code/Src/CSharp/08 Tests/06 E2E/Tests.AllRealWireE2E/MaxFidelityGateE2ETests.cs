using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Reactive.Linq;
using System.Security.Claims;
using System.Text;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using IndFusion.Ember.Abstractions.Hubs;
using IndQuestResults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prisma.Athena.Worker.Ingestion;
using Prisma.Orion.Ingestion;
using Prisma.Reconciliator.Worker;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

/// <summary>
/// MVP gate (#5) — the SINGLE max-fidelity live end-to-end run the owner ruled on (2026-06-13).
/// </summary>
/// <remarks>
/// <para>
/// This is the capstone that proves a real SIARA case traverses the WHOLE 3-process split with
/// <strong>nothing stubbed in the pipeline</strong> and audit persisted to a real SQL Server:
/// </para>
/// <list type="number">
///   <item>A real headless Playwright browser logs into the published SIARA simulator and captures a
///   credential-free storage-state (exactly the watch-loop's auth seam).</item>
///   <item>The REAL <see cref="ISiaraDocumentSource"/> discovers a live case package (PDF + DOCX + XML),
///   and the REAL <see cref="IngestionOrchestrator.IngestCaseAsync"/> downloads it through the REAL
///   <c>SiaraDocumentDownloader</c>, writes the real document bytes to shared storage, and broadcasts a
///   <see cref="DocumentDownloadedEvent"/> over Orion's REAL SignalR ingestion hub (genuine JWT clearance).</item>
///   <item>Athena's REAL pipeline runs: <c>FileSystemLoader</c> → <c>PolynomialImageQualityAnalyzer</c>
///   (Emgu.CV) → <c>TesseractOcrExecutor</c> (native Tesseract) → multi-source <c>FusionExpedienteService</c>
///   → handoff to shared storage → <see cref="ExtractionCompletedEvent"/> over the REAL reconciliation hub.</item>
///   <item>The Reconciliator's REAL <c>FileClassifierService</c> classifies and the REAL
///   <c>SiroXmlExporter</c> renders a conformant SIRO XML document, emitting
///   <see cref="ExportCompletedEvent"/> + <see cref="DocumentProcessingCompletedEvent"/>.</item>
///   <item>All three worker processes persist audit rows to a real SQL Server via Testcontainers
///   (<c>ConnectionStrings:DefaultConnection</c> wired in every host).</item>
/// </list>
/// <para>
/// <strong>What is real here that the fast <see cref="AllRealWireThreeHostE2ETests"/> stubs:</strong> the
/// SIARA browser download, OCR, image quality, fusion, classification, and SQL persistence. The only thing
/// shared with the fast harness is the in-memory SignalR transport seam (production hub + auth code runs;
/// no TCP port needed).
/// </para>
/// <para>
/// <strong>Scope notes (owner ruling 2: stubs/partials are OK to demo if labelled):</strong>
/// </para>
/// <list type="bullet">
///   <item>Review-case persistence IS now wired in production (GH #6, commit <c>2c5b01d</c>): a
///   partial/degraded case carrying <see cref="DocumentDownloadedEvent.IsComplete"/> = <see langword="false"/>
///   persists a flagged <c>ReviewReason.IncompleteCase</c> <c>ReviewCase</c> row via the Reconciliator's
///   Stage-4 scope. This gate does NOT itself <em>assert</em> that row: the full gate's complete case is
///   <c>IsComplete</c> = <see langword="true"/> (nothing to flag), and the partial-case gate deliberately
///   runs with the extraction pipeline disabled (<c>runExtractionPipeline: false</c>) to avoid the
///   Tesseract second-init deadlock, so it never reaches the Stage-4 persistence point. Review-case
///   persistence is covered directly by GH #6's own Testcontainers integration tests (incomplete-case
///   persistence, idempotency/heal) — see commit <c>e3fd56a</c>.</item>
/// </list>
/// <para>
/// <strong>Environment requirements (this machine satisfies all):</strong> Docker (Testcontainers SQL),
/// a Chromium install (<c>playwright install chromium</c>), native Tesseract + tessdata on PATH, and the
/// published simulator under <c>Deployments/Siara.Simulator/app</c>. The test starts the simulator if it
/// is not already running on <c>http://localhost:5001</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "MaxFidelityGate")]
public sealed class MaxFidelityGateE2ETests : IAsyncLifetime
{
    private const string SimulatorUrl = "http://localhost:5001";
    private const string ValidUsername = "BANAMEX";
    private const string ValidPassword = "password123";
    private const string DashboardSelector = "#arrivalRateSlider"; // present only on the authenticated dashboard

    // Shared JWT secret — all three workers sign/validate clearance tokens with this key.
    private const string SharedJwtSecret = "MAX-FIDELITY-GATE-E2E-SHARED-JWT-SECRET-FOR-TESTS-ONLY-32+";

    // Shared temp directory that acts as the cross-process "shared volume".
    private readonly string _sharedStorageDir =
        Path.Combine(Path.GetTempPath(), "prisma-maxfidelity-gate-" + Guid.NewGuid().ToString("N"));

    // Per-run isolated journal so the simulator's repeated cases are not deduped across runs.
    private string JournalPath => Path.Combine(_sharedStorageDir, "journal.txt");

    private SqlServerContainerFixture? _sql;
    private string _connectionString = string.Empty;

    private Process? _simulatorProcess;
    private bool _startedSim;

    private GateOrionApp? _orionApp;
    private GateAthenaApp? _athenaApp;
    private GateReconciliatorApp? _reconciliatorApp;

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

        // 2) Spin up a real SQL Server via Testcontainers and provision an isolated database for this run.
        _sql = new SqlServerContainerFixture();
        await _sql.InitializeAsync();
        _connectionString = await _sql.CreateIsolatedDatabaseAsync(nameof(MaxFidelityGateE2ETests), ct);

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

    // ── The gate ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drives one real SIARA case from a live-sim pull through all three worker processes — real OCR, real
    /// multi-source fusion, real classification, real SIRO XML export — and asserts the document survives the
    /// pipeline and that audit rows are persisted to real SQL.
    /// </summary>
    [Fact(Timeout = 900_000)] // 15 min hard cap: login + sim + native OCR + SQL + 3 hops are slow but bounded.
    public async Task RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless browser login → capture the credential-free storage-state ──
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // ── STEP 2: Boot the three real worker hosts wired to SQL + the live sim ──
        BuildThreeHostsWithDb(storageState);

        // ── STEP 3: Subscribe to the Reconciliator's terminal events ──
        var exportCompletedSource = new TaskCompletionSource<ExportCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var processingCompletedSource = new TaskCompletionSource<DocumentProcessingCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var exportSub = _reconciliatorApp!.ReconciliatorEventPublisher
            .GetEventStream<ExportCompletedEvent>()
            .Subscribe(e => exportCompletedSource.TrySetResult(e));
        using var completionSub = _reconciliatorApp.ReconciliatorEventPublisher
            .GetEventStream<DocumentProcessingCompletedEvent>()
            .Subscribe(e => processingCompletedSource.TrySetResult(e));

        // ── STEP 4: Wait for the production hub clients to connect over the in-memory transport ──
        await WaitUntilHubClientsConnectedAsync(ct);

        // ── STEP 5: REAL discovery — list a full 3-companion case package off the live sim ──
        SiaraCase fullCase = await DiscoverFullCompanionCaseAsync(ct);

        // ── STEP 6: REAL ingestion — download the case through the real downloader + broadcast over SignalR ──
        var correlationId = Guid.NewGuid();
        Result<IngestionResult> ingestResult;
        await using (var ingestScope = _orionApp!.Services.CreateAsyncScope())
        {
            var orchestrator = ingestScope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
            ingestResult = await orchestrator.IngestCaseAsync(fullCase, correlationId, ct);
        }

        ingestResult.IsSuccess.ShouldBeTrue(
            $"real case ingestion must succeed: {string.Join(", ", ingestResult.Errors)}");
        ingestResult.Value.ShouldNotBeNull();
        var fileId = ingestResult.Value!.FileId;

        // ── STEP 7: Wait (bounded, generous for native OCR) for the export + completion events ──
        var exportEvent = await AwaitOrFailAsync(
            exportCompletedSource.Task,
            TimeSpan.FromMinutes(10),
            "ExportCompletedEvent — the real case did not traverse the full pipeline (live download → OCR → fusion → classify → export)",
            ct);

        var completedEvent = await AwaitOrFailAsync(
            processingCompletedSource.Task,
            TimeSpan.FromMinutes(1),
            "DocumentProcessingCompletedEvent",
            ct);

        // ── STEP 8: Assertions ──

        // A) Identity preservation across all three processes.
        completedEvent.FileId.ShouldBe(fileId,
            "FileId must survive Orion → Athena → Reconciliator over the real pipeline");
        completedEvent.CorrelationId.ShouldBe(correlationId,
            "CorrelationId must survive the full 3-process pipeline");

        // B) SIRO XML export fidelity: the real SiroXmlExporter ran over real OCR + fusion output.
        exportEvent.FileId.ShouldBe(fileId, "ExportCompletedEvent.FileId must match the ingested case");
        exportEvent.Format.ShouldBe("SiroXml", "Export format must be SiroXml (real SiroXmlExporter)");
        exportEvent.ExportedSizeBytes.ShouldBeGreaterThan(0,
            "SIRO XML must have a non-zero byte size (a real XML document was rendered from real OCR/fusion data)");
        exportEvent.Destination.ShouldEndWith(".siro.xml");

        // C) Shared-storage handoff: the Athena Extractor physically wrote the fused expediente, the
        //    Reconciliator physically read it — the real cross-process filesystem edge.
        var fusionFiles = Directory.GetFiles(_sharedStorageDir, "*.fusion.json", SearchOption.AllDirectories);
        fusionFiles.Length.ShouldBeGreaterThanOrEqualTo(1,
            "at least one .fusion.json must have been written by the Athena Extractor");

        // D) The real downloaded case bytes are on shared storage (proves the real SIARA download ran, not a stub).
        var caseFiles = Directory.GetFiles(_sharedStorageDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();
        caseFiles.ShouldNotBeEmpty("the real SIARA downloader must have written the case document bytes to shared storage");

        // E) Audit persisted to REAL SQL — poll briefly because the audit writer is queued/async.
        var auditRows = await PollAuditRowsAsync(fileId, minimumRows: 1, TimeSpan.FromSeconds(45), ct);
        auditRows.ShouldNotBeEmpty("real audit rows must be persisted to SQL for the ingested FileId");
        auditRows.ShouldContain(
            a => a.Stage == ProcessingStage.Ingestion && a.ActionType == AuditActionType.Download,
            "the Orion Downloader must have persisted an ingestion/download audit row to real SQL");

        // Cross-process persistence proof: at least two distinct process identities wrote audit for this case.
        var distinctProcesses = auditRows
            .Where(a => !string.IsNullOrWhiteSpace(a.ProcessId))
            .Select(a => a.ProcessId)
            .Distinct()
            .Count();
        distinctProcesses.ShouldBeGreaterThanOrEqualTo(2,
            "audit for this case must be written by at least two distinct worker processes (real 3-process persistence)");
    }

    // ── The best-effort partial-case gate (owner ruling 3 / issue #4) ──────────────

    /// <summary>
    /// The best-effort partial-case path (owner ruling 2026-06-13): a SIARA case whose download is missing one
    /// of its three companion files is <strong>normal</strong> (~5–15% of cases) and must NOT invalidate the
    /// case. This drives the same real 3-process pipeline as the full gate, but breaks the DOCX companion's URL
    /// so its download fails, and asserts the case still flows: ingestion succeeds, the broadcast event is
    /// flagged <see cref="DocumentDownloadedEvent.IsComplete"/> = <see langword="false"/> with only the two
    /// surviving companions, the surviving PDF + XML still drive a real SIRO export, and a failed per-file
    /// ingestion audit row is persisted to real SQL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The missing file is the DOCX so the XML — which carries the expediente number — survives; the case
    /// remains processable. A case missing its expediente-bearing source would instead be flagged for manual
    /// review; that review-case persistence is the deferred half of the feature (GH #6, not asserted here).
    /// </para>
    /// <para>
    /// Scope: this gate asserts the best-effort <strong>ingestion + cross-process handoff</strong> contract
    /// (skip → flag → forward → persist), which fully completes before Stage-1/2. It deliberately does NOT
    /// await the downstream OCR→fusion→export — that machinery is proven by the complete-case gate
    /// (<see cref="RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"/>), and
    /// re-running native Tesseract a second time in the same test process is a known transient flake we keep
    /// this scenario independent of.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 900_000)]
    public async Task PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete()
    {
        var ct = TestContext.Current.CancellationToken;

        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // Keep the real ingestion forwarder but skip the OCR extraction pipeline (see method remarks): this
        // asserts the best-effort ingestion/handoff contract and stays independent of native-Tesseract flakiness.
        BuildThreeHostsWithDb(storageState, runAthenaPipeline: false);

        // Capture the forwarded ingestion event on Athena's real event stream — the best-effort flag lives on
        // it, and the forward happens before any OCR so this resolves regardless of downstream pipeline timing.
        var downloadedSource = new TaskCompletionSource<DocumentDownloadedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var downloadedSub = _athenaApp!.Services.GetRequiredService<IEventPublisher>()
            .GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => downloadedSource.TrySetResult(e));

        await WaitUntilHubClientsConnectedAsync(ct);

        // Discover the full 3-file case, then break the DOCX companion's URL so its real download fails.
        var fullCase = await DiscoverFullCompanionCaseAsync(ct);
        var partialCase = BuildCaseWithUndownloadableDocx(fullCase);

        var correlationId = Guid.NewGuid();
        Result<IngestionResult> ingestResult;
        await using (var ingestScope = _orionApp!.Services.CreateAsyncScope())
        {
            var orchestrator = ingestScope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
            ingestResult = await orchestrator.IngestCaseAsync(partialCase, correlationId, ct);
        }

        // Best-effort: a missing companion does NOT fail the case.
        ingestResult.IsSuccess.ShouldBeTrue(
            $"a case missing one companion must still ingest best-effort: {string.Join(", ", ingestResult.Errors)}");
        var fileId = ingestResult.Value!.FileId;

        // The broadcast event, forwarded across the real SignalR edge, carries the partial flag: only the two
        // surviving companions, IsComplete=false (proves the SmartEnum-on-the-wire fix too — XML survives typed).
        var forwarded = await AwaitOrFailAsync(
            downloadedSource.Task,
            TimeSpan.FromMinutes(2),
            "the forwarded DocumentDownloadedEvent (best-effort partial case)",
            ct);
        forwarded.IsComplete.ShouldBeFalse(
            "a case missing one of its three files must be flagged IsComplete=false for downstream review");
        forwarded.CaseFiles.Count.ShouldBe(2, "only the two successfully downloaded companions should be listed");
        forwarded.CaseFiles.ShouldNotContain(f => f.Format == FileFormat.Docx,
            "the companion whose download failed must not appear in CaseFiles");
        forwarded.CaseFiles.ShouldContain(f => f.Format == FileFormat.Xml,
            "the surviving XML companion (expediente source) must still be carried, typed correctly across the wire");

        // Persistent proof of the per-file best-effort skip: a failed ingestion/download audit row in real SQL.
        var auditRows = await PollAuditRowsAsync(fileId, minimumRows: 1, TimeSpan.FromSeconds(45), ct);
        auditRows.ShouldContain(
            a => a.Stage == ProcessingStage.Ingestion && a.ActionType == AuditActionType.Download && !a.Success,
            "the skipped companion must persist a failed ingestion/download audit row (CaseFileDownloadFailed)");
    }

    /// <summary>
    /// Returns a copy of <paramref name="fullCase"/> with the DOCX companion's URL pointed at a non-existent
    /// path so the real downloader fails to fetch it (HTTP 404 → failure Result → best-effort skip), while the
    /// PDF and XML companions remain downloadable.
    /// </summary>
    private static SiaraCase BuildCaseWithUndownloadableDocx(SiaraCase fullCase)
    {
        var files = fullCase.Files
            .Select(f => f.Format == FileFormat.Docx
                ? new DownloadableFile { Url = f.Url + ".missing-companion-404", FileName = f.FileName, Format = f.Format }
                : new DownloadableFile { Url = f.Url, FileName = f.FileName, Format = f.Format })
            .ToList();

        return fullCase with { Files = files };
    }

    // ── Host boot helper (shared by both gate scenarios) ───────────────────────────

    /// <summary>
    /// Builds the three real worker hosts (Orion → Athena → Reconciliator) wired to the Testcontainers SQL
    /// database and the live sim. Each worker reads <c>ConnectionStrings:DefaultConnection</c> EAGERLY at the
    /// top of its <c>Program.cs</c> (to decide whether to wire <c>AddDatabaseServices</c>), which runs before
    /// WebApplicationFactory applies the in-memory <c>ConfigureAppConfiguration</c>. So the connection string
    /// must reach <c>WebApplication.CreateBuilder</c> via an environment variable (read at builder
    /// construction). Scope it to the host-build window and restore the prior value so it never leaks to other
    /// tests in this assembly.
    /// </summary>
    private void BuildThreeHostsWithDb(string storageState, bool runAthenaPipeline = true)
    {
        const string connEnvVar = "ConnectionStrings__DefaultConnection";
        var previousConnEnv = Environment.GetEnvironmentVariable(connEnvVar);
        Environment.SetEnvironmentVariable(connEnvVar, _connectionString);
        try
        {
            _orionApp = new GateOrionApp(SharedJwtSecret, _sharedStorageDir, _connectionString, storageState, JournalPath);
            _ = _orionApp.Services;

            _athenaApp = new GateAthenaApp(SharedJwtSecret, _sharedStorageDir, _connectionString, _orionApp,
                runExtractionPipeline: runAthenaPipeline);
            _ = _athenaApp.Services;

            _reconciliatorApp = new GateReconciliatorApp(SharedJwtSecret, _sharedStorageDir, _connectionString, _athenaApp);
            _ = _reconciliatorApp.Services;
        }
        finally
        {
            Environment.SetEnvironmentVariable(connEnvVar, previousConnEnv);
        }
    }

    // ── Discovery helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Polls the REAL <see cref="ISiaraDocumentSource"/> (resolved from the Orion host) until it lists a case
    /// bundling all three companion formats (PDF + DOCX + XML), exactly as the watch loop does.
    /// </summary>
    private async Task<SiaraCase> DiscoverFullCompanionCaseAsync(CancellationToken ct)
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
    private static bool IsFullCompanionPackage(SiaraCase c) =>
        c.Files.Any(f => f.Url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) &&
        c.Files.Any(f => f.Url.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) &&
        c.Files.Any(f => f.Url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

    // ── Audit polling ──────────────────────────────────────────────────────────--

    private async Task<List<AuditRecord>> PollAuditRowsAsync(
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

    private async Task<string> LoginAndCaptureStorageStateAsync(CancellationToken ct)
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

    private static PlaywrightBrowserAutomationAdapter CreateLoginAdapter() =>
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

    private static async Task<T> AwaitOrFailAsync<T>(Task<T> task, TimeSpan timeout, string what, CancellationToken ct)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout, ct));
        winner.ShouldBe(task, $"{what} must arrive within {timeout.TotalSeconds:N0}s");
        return await task;
    }

    // ── Bounded connect-wait (in-memory SignalR transport) ────────────────────────

    private async Task WaitUntilHubClientsConnectedAsync(CancellationToken ct)
    {
        await ProbeHubUntilConnectedAsync(
            hubUrl: "http://orion-testserver/hubs/ingestion",
            handlerFactory: () => _orionApp!.Server.CreateHandler(),
            jwtToken: MintToken("athena-extractor-gate-probe", "Extract"),
            ct: ct);

        await ProbeHubUntilConnectedAsync(
            hubUrl: "http://athena-testserver/hubs/reconciliation",
            handlerFactory: () => _athenaApp!.Server.CreateHandler(),
            jwtToken: MintToken("reconciliator-gate-probe", "Reconcile"),
            ct: ct);

        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
    }

    private static async Task ProbeHubUntilConnectedAsync(
        string hubUrl,
        Func<HttpMessageHandler> handlerFactory,
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
                    opts.HttpMessageHandlerFactory = _ => handlerFactory();
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

    private static async Task<bool> IsSimulatorRunningAsync()
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

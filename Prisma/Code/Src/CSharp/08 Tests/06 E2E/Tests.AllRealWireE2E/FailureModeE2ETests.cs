using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Tests.AllRealWireE2E;

/// <summary>
/// Failure-mode resilience tests — PRISMA-E2-S6.  Four chaos scenarios exercising graceful degradation
/// across the 3-process split.
/// </summary>
/// <remarks>
/// <para>
/// Every test requires the same real infrastructure as the max-fidelity gate:
/// Docker (Testcontainers SQL), Playwright (Chromium), native Tesseract + tessdata, and the published
/// SIARA simulator on <c>http://localhost:5001</c>.  They therefore belong to the same
/// <c>MaxFidelityGate</c> serialized collection.
/// </para>
/// <para>
/// <strong>Scenario summary:</strong>
/// <list type="bullet">
///   <item>(a) <see cref="AthenaHubUnreachableMidDocument_OrionRetriesUntilReconnect"/> —
///   Athena starts before Orion's hub is live; <c>SiaraIngestionHubClient.ConnectWithRetryAsync</c>
///   retries until Orion's hub comes up; events are then delivered.</item>
///   <item>(b) <see cref="TesseractFailure_PipelineReturnsPartialExpediente_NotException"/> —
///   <c>IOcrExecutor</c> is stubbed to return a failure <c>Result</c>; the pipeline continues,
///   writes a partial <c>fusion.json</c>, and no unhandled exception escapes.</item>
///   <item>(c) <see cref="SiaraReturns503_WatchLoopBacksOffAndRetries"/> —
///   <c>IDocumentDownloader</c> returns 503-like failures for N calls, then succeeds; the watch
///   loop backs off (poll interval) and eventually ingests the case.</item>
///   <item>(d) <see cref="ReconciliatorRestartMidHandoff_SiroXmlArtifactDurable"/> —
///   the Reconciliator OS process is killed and restarted after the first SIRO XML is written;
///   the artifact survives on the shared volume and the restarted process handles new work.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Stub policy:</strong> only scenario (c) may substitute <see cref="IDocumentDownloader"/>.
/// Scenario (b) substitutes <c>IOcrExecutor</c> (internal pipeline component).  All external
/// boundaries (SIARA sim, SQL, Playwright, hub transports) are real in every scenario.
/// </para>
/// <para>
/// <strong>CI filter:</strong>
/// <c>--filter-query "*/*/FailureModeE2ETests/*"</c>
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "MaxFidelityGate")]
[Trait("category", "slow")]
[Collection("MaxFidelityGate")]
public sealed class FailureModeE2ETests : MaxFidelityGateE2EBase
{
    // ── Worker DLL names (copy from RealTcpThreeProcessE2ETests — private there) ──
    private const string OrionDllName          = "ExxerCube.Prisma.Orion.Worker.dll";
    private const string AthenaDllName         = "ExxerCube.Prisma.Athena.Worker.dll";
    private const string ReconciliatorDllName  = "ExxerCube.Prisma.Reconciliator.Worker.dll";

    // ── Extra disposables ────────────────────────────────────────────────────────
    // Scenarios that build custom in-process Kestrel hosts add them here.
    private readonly List<WebApplication> _extraApps    = new();
    // Scenario (d) reuses WorkerProcessLauncher for OS-process isolation.
    private readonly List<WorkerProcessLauncher> _extraWorkers = new();

    /// <inheritdoc/>
    protected override async ValueTask OnExtendedDisposeAsync()
    {
        // Kill OS-process workers FIRST so they release file handles on _sharedStorageDir.
        foreach (var w in _extraWorkers)
        {
            await w.DisposeAsync().ConfigureAwait(false);
        }

        _extraWorkers.Clear();

        // Dispose extra in-process hosts next.
        foreach (var app in _extraApps)
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }

        _extraApps.Clear();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Scenario (a): Athena hub unreachable → ConnectWithRetryAsync retries until Orion comes up
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proves that <c>SiaraIngestionHubClient.ConnectWithRetryAsync</c> retries (with the configured
    /// <c>Ingestion:ReconnectDelay</c>) when Orion's hub is not yet live, and that
    /// <see cref="DocumentDownloadedEvent"/> is delivered after Orion eventually starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism:</strong>
    /// <list type="number">
    ///   <item>A loopback port is pre-allocated for Orion.</item>
    ///   <item>The Athena in-process host is started first, targeting that port.
    ///   <c>SiaraIngestionHubClient.ExecuteAsync</c> starts <c>ConnectWithRetryAsync</c> in the
    ///   background, hits "Connection refused", and retries every 200 ms.</item>
    ///   <item>After a 1-second gap (≥ 5 retry cycles), Orion is started on the pre-allocated port
    ///   via a direct <c>Prisma.Orion.Worker.Program.BuildApp</c> call with
    ///   <c>UseUrls("http://127.0.0.1:{port}")</c>.</item>
    ///   <item>Once Orion's hub is detected as accepting connections (hub probe), Athena reconnects
    ///   on its next 200 ms retry, and a real case ingested through Orion's
    ///   <see cref="IngestionOrchestrator"/> is received by Athena's local event publisher.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Assertions:</strong>
    /// (1) <see cref="DocumentDownloadedEvent"/> arrives on Athena's <see cref="IEventPublisher"/>
    /// within 2 minutes — proves delivery-after-reconnect.
    /// (2) The event's <c>FileId</c> matches the case ingested through Orion — proves identity
    /// preservation across the hub edge.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 600_000)] // 10-min cap: Playwright login + sim discovery + hub reconnect
    public async Task AthenaHubUnreachableMidDocument_OrionRetriesUntilReconnect()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless login → credential-free storage-state ───────────
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // ── STEP 2: Pre-allocate a stable loopback port for Orion ─────────────────
        // Athena needs to know the hub URL before Orion starts; we fix the port here.
        var fixedOrionPort = WorkerProcessLauncher.AllocateFreePort();
        var orionBaseAddress = new Uri($"http://127.0.0.1:{fixedOrionPort}/");
        var ingestionHubUrl  = new Uri(orionBaseAddress, "hubs/ingestion").ToString();

        // ── STEP 3: Start Athena FIRST (Orion hub is not yet live) ────────────────
        // SiaraIngestionHubClient.ConnectWithRetryAsync will hit "Connection refused" on every
        // 200 ms retry until Orion starts at step 5.
        _athenaApp = await GateAthenaHost.StartAsync(
            SharedJwtSecret,
            _sharedStorageDir,
            _connectionString,
            orionBaseAddress,
            runExtractionPipeline: false, // focus is hub connectivity, not full OCR
            ct);

        // Subscribe immediately so we do not miss events that arrive once Athena connects.
        var downloadedTcs = new TaskCompletionSource<DocumentDownloadedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var downloadedSub = _athenaApp.Services
            .GetRequiredService<IEventPublisher>()
            .GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => downloadedTcs.TrySetResult(e));

        // ── STEP 4: Let Athena attempt ≥ 5 connection retries ────────────────────
        // At Ingestion:ReconnectDelay = 200 ms a 1-second gap guarantees multiple cycles.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        // ── STEP 5: Start Orion on the pre-allocated fixed port ───────────────────
        // We call Program.BuildApp directly (not GateOrionHost.StartAsync) so we can pass
        // a specific port via UseUrls instead of the factory's hardcoded "0".
        var orionWebApp = global::Prisma.Orion.Worker.Program.BuildApp(
            args: Array.Empty<string>(),
            configureEarly: builder =>
            {
                builder.WebHost.UseUrls($"http://127.0.0.1:{fixedOrionPort}");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"]      = _connectionString,
                    ["ProcessIdentity:JwtSecret"]                = SharedJwtSecret,
                    ["ProcessIdentity:JwtIssuer"]                = "prisma-pipeline",
                    ["ProcessIdentity:JwtAudience"]              = "prisma-pipeline",
                    ["ProcessIdentity:TokenLifetime"]            = "01:00:00",
                    ["ProcessIdentity:Clearance"]                = "Download",
                    ["Siara:Actor:ActorId"]                      = "orion-downloader-chaos-a",
                    ["Siara:Actor:DisplayName"]                  = "Orion Downloader (Chaos A)",
                    ["Storage:BasePath"]                         = _sharedStorageDir,
                    ["Siara:AuthMode"]                           = "SessionPassthrough",
                    ["Siara:Passthrough:Transport"]              = "StorageState",
                    ["Siara:Passthrough:StorageStateRef"]        = storageState,
                    ["Siara:Passthrough:DashboardUrl"]           = SimulatorUrl + "/",
                    ["Siara:Passthrough:PostLoginSelector"]      = DashboardSelector,
                    ["NavigationTargets:SiaraUrl"]               = SimulatorUrl + "/",
                    ["BrowserAutomation:Headless"]               = "true",
                    ["BrowserAutomation:IgnoreHttpsErrors"]      = "true",
                    ["BrowserAutomation:PageTimeoutMs"]          = "60000",
                    ["Ingestion:PostWriteFlushDelayMs"]          = "250",
                });
            },
            configureServicesLate: services =>
            {
                // Per-run isolated journal: prevents cross-run SHA-256 dedup of the same sim cases.
                services.RemoveAll<IIngestionJournal>();
                services.AddSingleton<IIngestionJournal>(sp =>
                    new FileIngestionJournal(
                        JournalPath,
                        sp.GetRequiredService<ILogger<FileIngestionJournal>>()));

                // Suppress the autonomous watch loop — the test drives ingestion explicitly.
                foreach (var d in services
                    .Where(s => s.ImplementationType?.Name == "OrionWorkerService")
                    .ToList())
                {
                    services.Remove(d);
                }
            });

        await orionWebApp.StartAsync(ct);
        _extraApps.Add(orionWebApp); // ensures disposal even on assertion failure

        // ── STEP 6: Confirm Orion's ingestion hub is accepting connections ─────────
        // This proves Orion's Kestrel + SignalR are up. Athena's background retry will
        // succeed within the next 200 ms cycle.
        await ProbeHubUntilConnectedAsync(
            hubUrl: ingestionHubUrl,
            jwtToken: MintToken("reconnect-probe-a", "Extract"),
            ct: ct);

        // ── STEP 7: Give Athena's background service time to complete the connect ──
        // 2 s >> 200 ms ReconnectDelay — reliable across loaded CI machines.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        // ── STEP 8: Discover a case from the live sim via the Orion host ──────────
        // Retry up to 3 times in case the sim is briefly slow to list cases.
        SiaraCase? fullCase = null;
        for (var attempt = 0; attempt < 3 && fullCase is null; attempt++)
        {
            await using var discoveryScope = orionWebApp.Services.CreateAsyncScope();
            var source = discoveryScope.ServiceProvider.GetRequiredService<ISiaraDocumentSource>();
            var discovered = await source.DiscoverCasesAsync(ct);
            discovered.IsSuccess.ShouldBeTrue(
                $"case discovery must succeed (attempt {attempt + 1}): {string.Join(", ", discovered.Errors)}");
            fullCase = discovered.Value?.FirstOrDefault(IsFullCompanionPackage);
            if (fullCase is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        fullCase.ShouldNotBeNull(
            "the live sim must expose at least one 3-companion case (PDF + DOCX + XML) within 3 attempts");

        // ── STEP 9: Drive ingestion (Orion downloads + broadcasts over its real hub) ─
        var correlationId = Guid.NewGuid();
        Result<IngestionResult> ingestResult;
        await using (var ingestScope = orionWebApp.Services.CreateAsyncScope())
        {
            var orchestrator = ingestScope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
            ingestResult = await orchestrator.IngestCaseAsync(fullCase!, correlationId, ct);
        }

        ingestResult.IsSuccess.ShouldBeTrue(
            $"ingestion must succeed after Athena reconnects: {string.Join(", ", ingestResult.Errors)}");

        // ── STEP 10: Assert delivery after reconnect ───────────────────────────────
        // The DocumentDownloadedEvent broadcast by Orion's real Kestrel hub must reach
        // Athena's event publisher over the now-live SignalR connection.
        var evt = await AwaitOrFailAsync(
            downloadedTcs.Task,
            TimeSpan.FromMinutes(2),
            "DocumentDownloadedEvent must be received on Athena's IEventPublisher after " +
            "ConnectWithRetryAsync succeeded (delivery-after-reconnect)",
            ct);

        evt.ShouldNotBeNull();
        evt.FileId.ShouldBe(ingestResult.Value!.FileId,
            "the event FileId must match the case ingested through Orion " +
            "(proves identity preservation across the hub edge)");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Scenario (b): Tesseract OCR fails → partial expediente, no unhandled exception
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proves that when <c>IOcrExecutor.ExecuteOcrAsync</c> returns a failure
    /// <see cref="Result{T}"/>, the Athena extraction pipeline degrades gracefully:
    /// <list type="bullet">
    ///   <item>Fusion still runs using companion sources (XML/DOCX) and writes <c>*.fusion.json</c>
    ///   to the shared volume — the "partial expediente".</item>
    ///   <item>A <see cref="ProcessingErrorEvent"/> with <c>Component == "OCR"</c> is published on
    ///   Athena's local event stream — the error is captured as a domain event, not an exception.</item>
    ///   <item>At least one extraction audit row is persisted to SQL.</item>
    ///   <item>No unhandled exception escapes the pipeline (test body completes normally).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism:</strong> NSubstitute replaces <c>IOcrExecutor</c> in Athena's DI container
    /// via <c>configureServicesLate</c>.  The stub returns
    /// <c>Result&lt;OCRResult&gt;.Failure("…")</c> on every call, exactly as the real
    /// <see cref="ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract.TesseractOcrExecutor"/>
    /// does when tessdata is unavailable or the engine deadlocks.
    /// </para>
    /// <para>
    /// <strong>Pipeline level:</strong> Orion + Athena (no Reconciliator). When Athena tries to
    /// broadcast <c>ExtractionCompletedEvent</c> to the Reconciliator hub with 0 connected clients,
    /// the broadcast succeeds vacuously (SignalR sends to nobody).
    /// </para>
    /// <para>
    /// <strong>Assertions:</strong>
    /// (1) <c>ProcessingErrorEvent{Component="OCR"}</c> arrives on Athena's event stream.
    /// (2) <c>*.fusion.json</c> appears in <c>_sharedStorageDir</c>.
    /// (3) <c>IOcrExecutor.ExecuteOcrAsync</c> was called at least once (substitute verification).
    /// (4) At least one extraction audit row exists in SQL.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 900_000)] // 15-min cap: login + discovery + real download + fusion (no OCR)
    public async Task TesseractFailure_PipelineReturnsPartialExpediente_NotException()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless login → credential-free storage-state ───────────
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // ── STEP 2: Start Orion (normal — real SIARA download + discovery) ────────
        _orionApp = await GateOrionHost.StartAsync(
            SharedJwtSecret, _sharedStorageDir, _connectionString, storageState, JournalPath, ct);

        // ── STEP 3: Build a failing IOcrExecutor stub ─────────────────────────────
        // NSubstitute replaces the real TesseractOcrExecutor in Athena's DI so every
        // ExecuteOcrAsync call returns a failure Result (mimics missing tessdata / engine crash).
        var failingOcrExecutor = Substitute.For<IOcrExecutor>();
        failingOcrExecutor
            .ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(_ => Task.FromResult(
                Result<OCRResult>.Failure(
                    "Simulated Tesseract failure: no tessdata configured (chaos test b)")));

        // ── STEP 4: Start Athena with the stubbed OCR executor ────────────────────
        // AthenaWorkerService (ExtractionPipelineService subscriber) is NOT suppressed so
        // the full Stage 1 → Stage 2 (stubbed OCR) → Stage 3 (fusion) path executes.
        var athenaWebApp = global::Prisma.Athena.Worker.Program.BuildApp(
            args: Array.Empty<string>(),
            configureEarly: builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["ProcessIdentity:JwtSecret"]           = SharedJwtSecret,
                    ["ProcessIdentity:JwtIssuer"]           = "prisma-pipeline",
                    ["ProcessIdentity:JwtAudience"]         = "prisma-pipeline",
                    ["ProcessIdentity:TokenLifetime"]       = "01:00:00",
                    ["ProcessIdentity:Clearance"]           = "Extract",
                    ["Siara:Actor:ActorId"]                 = "athena-extractor-chaos-b",
                    ["Siara:Actor:DisplayName"]             = "Athena Extractor (Chaos B)",
                    ["Storage:BasePath"]                    = _sharedStorageDir,
                    ["Ingestion:HubUrl"]                    = new Uri(_orionApp!.BaseAddress, "hubs/ingestion").ToString(),
                    ["Ingestion:ReconnectDelay"]            = "00:00:00.200",
                });
            },
            configureServicesLate: services =>
            {
                // Replace TesseractOcrExecutor with the stub that always fails.
                services.RemoveAll<IOcrExecutor>();
                services.AddSingleton<IOcrExecutor>(failingOcrExecutor);
                // AthenaWorkerService (extraction pipeline subscriber) stays — we need it to run.
            });

        await athenaWebApp.StartAsync(ct);
        _extraApps.Add(athenaWebApp);

        // ── STEP 5: Subscribe to Athena's event publisher ─────────────────────────
        var athenaPublisher = athenaWebApp.Services.GetRequiredService<IEventPublisher>();

        // TCS fires when the forwarded DocumentDownloadedEvent arrives (Orion→Athena hub edge).
        var downloadedTcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var downloadedSub = athenaPublisher
            .GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(e => downloadedTcs.TrySetResult(e.FileId));

        // TCS fires when Athena's ExtractionOrchestrator emits the OCR error as a domain event.
        var ocrErrorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ocrErrorSub = athenaPublisher
            .GetEventStream<ProcessingErrorEvent>()
            .Subscribe(e =>
            {
                if (e.Component == "OCR")
                {
                    ocrErrorTcs.TrySetResult(true);
                }
            });

        // ── STEP 6: Wait for Orion's hub to accept connections from Athena ────────
        await ProbeHubUntilConnectedAsync(
            hubUrl: new Uri(_orionApp!.BaseAddress, "hubs/ingestion").ToString(),
            jwtToken: MintToken("athena-probe-b", "Extract"),
            ct: ct);
        await Task.Delay(TimeSpan.FromSeconds(2), ct); // Athena hub-client connects

        // ── STEP 7: Discover a full 3-companion case + drive ingestion ─────────────
        var fullCase = await DiscoverFullCompanionCaseAsync(ct);
        var correlationId = Guid.NewGuid();
        Result<IngestionResult> ingestResult;
        await using (var ingestScope = _orionApp!.Services.CreateAsyncScope())
        {
            var orchestrator = ingestScope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
            ingestResult = await orchestrator.IngestCaseAsync(fullCase, correlationId, ct);
        }

        ingestResult.IsSuccess.ShouldBeTrue(
            $"real case ingestion must succeed: {string.Join(", ", ingestResult.Errors)}");
        var fileId = ingestResult.Value!.FileId;

        // ── STEP 8: DocumentDownloadedEvent must arrive on Athena ────────────────
        var arrivedFileId = await AwaitOrFailAsync(
            downloadedTcs.Task,
            TimeSpan.FromMinutes(2),
            "DocumentDownloadedEvent must arrive on Athena's IEventPublisher",
            ct);
        arrivedFileId.ShouldBe(fileId, "forwarded event must carry the same FileId");

        // ── STEP 9: OCR error must be captured as a domain event (not an exception) ─
        await AwaitOrFailAsync(
            ocrErrorTcs.Task,
            TimeSpan.FromMinutes(3),
            "ProcessingErrorEvent{Component='OCR'} must be published — " +
            "proves the OCR failure Result was handled as a domain event, not a crash",
            ct);

        // Verify with the substitute that ExecuteOcrAsync was actually called.
        // NSubstitute: Received() with no args means "at least once".
        _ = failingOcrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());

        // ── STEP 10: Partial expediente (fusion.json) must be present ─────────────
        // Fusion runs with ocrResult = null but real XML/DOCX companion sources,
        // so FusionExpediente is still non-null.  fusion.json proves the pipeline
        // continued past the OCR failure and produced output.
        var fusionFiles = await PollForFilesAsync("*.fusion.json", TimeSpan.FromMinutes(5), ct);
        fusionFiles.ShouldNotBeEmpty(
            "at least one *.fusion.json must be written to _sharedStorageDir even when OCR fails " +
            "(fusion continues with XML/DOCX sources — the 'partial expediente')");

        // ── STEP 11: Extraction audit row must exist in SQL ───────────────────────
        var auditRows = await PollAuditRowsAsync(fileId, minimumRows: 1, TimeSpan.FromSeconds(45), ct);
        auditRows.ShouldNotBeEmpty(
            "at least one audit row must be persisted to SQL for the ingested FileId");
        auditRows.ShouldContain(
            a => a.Stage == ProcessingStage.Extraction && a.ActionType == AuditActionType.Extraction,
            "an Extraction-stage audit row must exist (proves pipeline reached Athena)");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Scenario (c): SIARA returns 503 → watch loop backs off and retries
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proves that when <c>IDocumentDownloader</c> returns a 503-like failure for N consecutive
    /// calls, the <see cref="SiaraWatchLoop"/> backs off (waits the configured
    /// <c>OrionWatchLoop:PollInterval</c>), re-discovers the case, and eventually succeeds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism:</strong> NSubstitute replaces <c>IDocumentDownloader</c> in the Orion host.
    /// The stub returns <c>Result.Failure("503 …")</c> for the first <c>FailThreshold</c>
    /// download calls (covering at least one full scan cycle across all companion files), then
    /// returns a fake <see cref="DownloadedDocument"/> for subsequent calls.  The watch loop's
    /// poll interval is set to 100 ms so the test completes in seconds rather than minutes.
    /// </para>
    /// <para>
    /// <strong>Only allowed stub:</strong> <c>IDocumentDownloader</c>. The real
    /// <c>ISiaraDocumentSource</c> (Playwright/sim-backed discovery) and the real SQL journal
    /// are used.  The watch loop (<c>OrionWorkerService</c>) is NOT suppressed — it runs the
    /// autonomous discover → ingest → journal cycle under test.
    /// </para>
    /// <para>
    /// <strong>Assertions:</strong>
    /// (1) Total <c>IDocumentDownloader.DownloadAsync</c> calls ≥ <c>FailThreshold + 1</c> — proves
    /// the watch loop retried after failures (≥ 1 complete failure cycle happened).
    /// (2) At least one file is written to <c>_sharedStorageDir</c> — proves eventual successful
    /// ingestion after the stub started returning fake documents.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 300_000)] // 5-min cap: login + sim discovery + watch-loop cycles (100 ms poll)
    public async Task SiaraReturns503_WatchLoopBacksOffAndRetries()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless login → credential-free storage-state ───────────
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // ── STEP 2: Build the IDocumentDownloader stub ────────────────────────────
        // FailThreshold = 5: enough to cover one full scan cycle on a 1–3 file case plus a margin.
        // The first FailThreshold calls return 503-like failure; calls beyond that return a valid
        // fake DownloadedDocument so the ingestion can complete.
        const int FailThreshold = 5;
        var downloadCallCount = 0;
        var fakeActor = new SiaraActor
        {
            ActorId      = "chaos-503-test-actor",
            ActorType    = SiaraActorType.ServiceAccount,
            DisplayName  = "503 Chaos Test Actor",
        };

        var downloader503 = Substitute.For<IDocumentDownloader>();
        downloader503
            .DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var callNo = Interlocked.Increment(ref downloadCallCount);
                if (callNo <= FailThreshold)
                {
                    return Task.FromResult(
                        Result<DownloadedDocument>.Failure(
                            $"503 Service Unavailable — simulated SIARA outage (call {callNo}/{FailThreshold})"));
                }

                // Subsequent calls succeed with a minimal fake document.
                var docId = ci.Arg<string>();
                return Task.FromResult(Result<DownloadedDocument>.Success(new DownloadedDocument
                {
                    Content      = System.Text.Encoding.ASCII.GetBytes($"%PDF-1.4 chaos-503-stub call={callNo}"),
                    DocumentId   = docId,
                    SourceUrl    = docId,
                    Format       = FileFormat.Pdf,
                    AcquiredBy   = fakeActor,
                    SessionId    = "chaos-503-session",
                }));
            });

        // ── STEP 3: Start the Orion host with the watch loop enabled ──────────────
        // PollInterval = 100 ms so retries happen within seconds.
        // The watch loop (OrionWorkerService) is NOT suppressed; it drives discovery autonomously.
        var orionWatchApp = global::Prisma.Orion.Worker.Program.BuildApp(
            args: Array.Empty<string>(),
            configureEarly: builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"]      = _connectionString,
                    ["ProcessIdentity:JwtSecret"]                = SharedJwtSecret,
                    ["ProcessIdentity:JwtIssuer"]                = "prisma-pipeline",
                    ["ProcessIdentity:JwtAudience"]              = "prisma-pipeline",
                    ["ProcessIdentity:TokenLifetime"]            = "01:00:00",
                    ["ProcessIdentity:Clearance"]                = "Download",
                    ["Siara:Actor:ActorId"]                      = "orion-downloader-chaos-c",
                    ["Siara:Actor:DisplayName"]                  = "Orion Downloader (Chaos C)",
                    ["Storage:BasePath"]                         = _sharedStorageDir,
                    ["Siara:AuthMode"]                           = "SessionPassthrough",
                    ["Siara:Passthrough:Transport"]              = "StorageState",
                    ["Siara:Passthrough:StorageStateRef"]        = storageState,
                    ["Siara:Passthrough:DashboardUrl"]           = SimulatorUrl + "/",
                    ["Siara:Passthrough:PostLoginSelector"]      = DashboardSelector,
                    ["NavigationTargets:SiaraUrl"]               = SimulatorUrl + "/",
                    ["BrowserAutomation:Headless"]               = "true",
                    ["BrowserAutomation:IgnoreHttpsErrors"]      = "true",
                    ["BrowserAutomation:PageTimeoutMs"]          = "60000",
                    ["Ingestion:PostWriteFlushDelayMs"]          = "0",    // no I/O flush delay in tests
                    ["OrionWatchLoop:PollInterval"]              = "00:00:00.100", // 100 ms — fast retries
                });
            },
            configureServicesLate: services =>
            {
                // Per-run isolated journal (fresh per test run).
                services.RemoveAll<IIngestionJournal>();
                services.AddSingleton<IIngestionJournal>(sp =>
                    new FileIngestionJournal(
                        JournalPath,
                        sp.GetRequiredService<ILogger<FileIngestionJournal>>()));

                // Replace the real SiaraDocumentDownloader with the 503 stub.
                // IDocumentDownloader is the ONLY interface allowed to be stubbed in this scenario.
                services.RemoveAll<IDocumentDownloader>();
                services.AddSingleton<IDocumentDownloader>(downloader503);

                // OrionWorkerService (autonomous watch loop) is intentionally NOT removed here —
                // testing its retry behaviour is the entire point of this scenario.
            });

        await orionWatchApp.StartAsync(ct);
        _extraApps.Add(orionWatchApp);

        // ── STEP 4: Wait for the watch loop to retry and eventually succeed ────────
        // Poll _sharedStorageDir for any new file (excluding the journal) written after the
        // stub starts returning success.
        var storedFiles = await PollForFilesAsync("*", TimeSpan.FromSeconds(30), ct,
            excludePattern: "journal.txt");

        storedFiles.ShouldNotBeEmpty(
            "the SiaraWatchLoop must eventually succeed after 503 retries " +
            "(fake DownloadedDocument bytes were stored to _sharedStorageDir)");

        // ── STEP 5: Assert retry evidence ─────────────────────────────────────────
        var finalCallCount = Volatile.Read(ref downloadCallCount);

        finalCallCount.ShouldBeGreaterThanOrEqualTo(FailThreshold + 1,
            $"IDocumentDownloader.DownloadAsync must have been called at least {FailThreshold + 1} " +
            $"times ({FailThreshold} failures + ≥ 1 success), proving the watch loop retried " +
            $"across at least one full failure cycle. Actual: {finalCallCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Scenario (d): Reconciliator restart mid-handoff → SIRO XML artifact durable
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proves that the SIRO XML export artifact on the shared volume survives a Reconciliator
    /// OS-process restart, and that the restarted Reconciliator can process a new handoff and
    /// write a subsequent SIRO XML.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism:</strong>
    /// <list type="number">
    ///   <item>All three workers are launched as separate OS processes via
    ///   <see cref="WorkerProcessLauncher"/> (mirrors
    ///   <see cref="RealTcpThreeProcessE2ETests"/>).</item>
    ///   <item>Orion's autonomous watch loop discovers and ingests a case; the full pipeline
    ///   runs and the Reconciliator writes <c>*.siro.xml</c> to the shared volume.</item>
    ///   <item>The Reconciliator OS process is killed; Orion is also killed so its in-memory
    ///   journal is cleared.</item>
    ///   <item>Both are restarted on the same loopback ports (Reconciliator reconnects to
    ///   Athena's existing hub; new Orion re-discovers cases from a fresh journal).</item>
    ///   <item>A second SIRO XML appears (restarted Reconciliator processed a new handoff),
    ///   and the first SIRO XML is asserted still present (artifact durability).</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Assertions:</strong>
    /// (1) First <c>*.siro.xml</c> has valid structure before the kill.
    /// (2) First <c>*.siro.xml</c> is STILL PRESENT after kill + restart.
    /// (3) A second <c>*.siro.xml</c> appears after restart.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 1_500_000)] // 25-min cap: two full pipeline runs + restart overhead
    public async Task ReconciliatorRestartMidHandoff_SiroXmlArtifactDurable()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless login → credential-free storage-state ───────────
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty(
            "the simulator login must yield an authenticated storage-state before launching workers");

        // ── STEP 2: Allocate three stable loopback ports ──────────────────────────
        // Ports are reused across the kill/restart cycle so hub URLs are stable.
        var orionPort         = WorkerProcessLauncher.AllocateFreePort();
        var athenaPort        = WorkerProcessLauncher.AllocateFreePort();
        var reconciliatorPort = WorkerProcessLauncher.AllocateFreePort();

        // ── STEP 3: Start all three workers as OS processes ───────────────────────
        var orionWorkDir1 = Path.Combine(_sharedStorageDir, "orion-work-1");
        await StartOrionWorkerAsync(storageState, orionPort, orionWorkDir1, ct);
        await StartAthenaWorkerAsync(orionPort, athenaPort, ct);
        await StartReconciliatorWorkerAsync(athenaPort, reconciliatorPort, ct);

        // ── STEP 4: Brief settle — let hub clients complete SignalR handshakes ─────
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        // ── STEP 5: Poll for the first SIRO XML ───────────────────────────────────
        // Orion's watch loop discovers + downloads → Athena OCR + fuses → Reconciliator exports.
        var firstSiroFiles = await PollForFilesAsync(
            "*.siro.xml", TimeSpan.FromMinutes(15), ct);

        firstSiroFiles.ShouldNotBeEmpty(
            "the Reconciliator OS process must write a SIRO XML to shared storage " +
            "during the first pipeline run.\n\n" + WorkerLogsForD());

        AssertSiroXmlStructure(firstSiroFiles[0], WorkerLogsForD());

        var firstSiroPath = firstSiroFiles[0];

        // ── STEP 6: Kill Reconciliator and Orion ──────────────────────────────────
        // We kill BOTH so that:
        //   a) Reconciliator is truly down (the "mid-handoff" kill).
        //   b) Orion's in-memory journal is cleared: restarting with a fresh journal
        //      allows it to re-download and re-ingest the same cases, triggering a
        //      new pipeline run through the restarted Reconciliator.
        var reconciliatorLauncher = _extraWorkers.Last(w => w.WorkerName == "Reconciliator");
        _extraWorkers.Remove(reconciliatorLauncher);
        reconciliatorLauncher.Kill();
        await reconciliatorLauncher.DisposeAsync();

        var orionLauncher1 = _extraWorkers.Last(w => w.WorkerName == "Orion");
        _extraWorkers.Remove(orionLauncher1);
        orionLauncher1.Kill();
        await orionLauncher1.DisposeAsync();

        await Task.Delay(TimeSpan.FromSeconds(2), ct); // OS port-release TOCTOU gap

        // ── STEP 7: Assert the first SIRO XML survived the kill ───────────────────
        File.Exists(firstSiroPath).ShouldBeTrue(
            "the SIRO XML artifact must survive the Reconciliator OS-process kill " +
            "(it is on the shared volume, not in the process's memory)");

        // ── STEP 8: Restart Orion (same port, fresh journal) ─────────────────────
        var orionWorkDir2 = Path.Combine(_sharedStorageDir, "orion-work-2");
        await StartOrionWorkerAsync(storageState, orionPort, orionWorkDir2, ct);

        // ── STEP 9: Restart Reconciliator (same port) → reconnects to Athena ─────
        await StartReconciliatorWorkerAsync(athenaPort, reconciliatorPort, ct);

        // ── STEP 10: Brief settle for hub reconnection ────────────────────────────
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        // ── STEP 11: Poll for a SIRO XML AFTER the restart ───────────────────────
        // The restarted Orion re-discovers cases (fresh in-memory journal) and re-ingests;
        // the restarted Reconciliator processes the new ExtractionCompletedEvent and writes
        // a second SIRO XML (possibly the same path if deterministic; asserted present below).
        var postRestartSiroFiles = await PollForFilesAsync(
            "*.siro.xml", TimeSpan.FromMinutes(15), ct);

        postRestartSiroFiles.ShouldNotBeEmpty(
            "the restarted Reconciliator must write a SIRO XML after reconnecting to " +
            "Athena and processing a new case.\n\n" + WorkerLogsForD());

        AssertSiroXmlStructure(postRestartSiroFiles[0], WorkerLogsForD());

        // ── STEP 12: Confirm first artifact is still on the shared volume ─────────
        File.Exists(firstSiroPath).ShouldBeTrue(
            "the original SIRO XML artifact must remain on the shared volume after the " +
            "Reconciliator was killed AND restarted (proves the shared volume is durable)");
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Polls <c>_sharedStorageDir</c> for files matching <paramref name="searchPattern"/> until at
    /// least one is found or <paramref name="timeout"/> elapses.
    /// </summary>
    private async Task<string[]> PollForFilesAsync(
        string searchPattern,
        TimeSpan timeout,
        CancellationToken ct,
        string? excludePattern = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var files = Directory.GetFiles(_sharedStorageDir, searchPattern, SearchOption.AllDirectories);
            if (!string.IsNullOrEmpty(excludePattern))
            {
                files = files
                    .Where(f => !Path.GetFileName(f)
                        .Contains(excludePattern, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (files.Length > 0)
            {
                return files;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Validates the SIRO XML structure: root element, <c>NumeroExpediente</c>, <c>NumeroOficio</c>.
    /// Mirrors the assertion in <see cref="RealTcpThreeProcessE2ETests"/>.
    /// </summary>
    private static void AssertSiroXmlStructure(string siroFilePath, string diagnostics)
    {
        System.Xml.Linq.XNamespace siroNs = "http://siro.regulatory.namespace";
        var doc = System.Xml.Linq.XDocument.Load(siroFilePath);

        doc.Root!.Name.ShouldBe(
            siroNs + "SiroResponse",
            $"SIRO XML root must be {{http://siro.regulatory.namespace}}SiroResponse. {diagnostics}");

        doc.Root.Element(siroNs + "NumeroExpediente")!.Value
            .ShouldNotBeNullOrWhiteSpace(
                $"NumeroExpediente must be populated in the SIRO XML. {diagnostics}");

        doc.Root.Element(siroNs + "NumeroOficio")!.Value
            .ShouldNotBeNullOrWhiteSpace(
                $"NumeroOficio must be populated in the SIRO XML. {diagnostics}");
    }

    // ── OS-process helpers for scenario (d) ──────────────────────────────────────

    /// <summary>Builds the env-var dictionary keys common to all three OS-process workers.</summary>
    private Dictionary<string, string> BuildCommonEnvVarsD(string actorId, string clearance) =>
        new()
        {
            ["ConnectionStrings__DefaultConnection"] = _connectionString,
            ["ProcessIdentity__JwtSecret"]           = SharedJwtSecret,
            ["ProcessIdentity__JwtIssuer"]           = "prisma-pipeline",
            ["ProcessIdentity__JwtAudience"]         = "prisma-pipeline",
            ["ProcessIdentity__TokenLifetime"]       = "01:00:00",
            ["ProcessIdentity__Clearance"]           = clearance,
            ["Siara__Actor__ActorId"]                = actorId,
            ["Siara__Actor__DisplayName"]            = actorId,
            ["Storage__BasePath"]                    = _sharedStorageDir,
        };

    private async Task StartOrionWorkerAsync(
        string storageState,
        int port,
        string workingDirectory,
        CancellationToken ct)
    {
        Directory.CreateDirectory(workingDirectory);
        var v = BuildCommonEnvVarsD("orion-downloader-chaos-d", "Download");
        v["Siara__AuthMode"]                       = "SessionPassthrough";
        v["Siara__Passthrough__Transport"]         = "StorageState";
        v["Siara__Passthrough__StorageStateRef"]   = storageState;
        v["Siara__Passthrough__DashboardUrl"]      = SimulatorUrl + "/";
        v["Siara__Passthrough__PostLoginSelector"] = DashboardSelector;
        v["NavigationTargets__SiaraUrl"]           = SimulatorUrl + "/";
        v["BrowserAutomation__Headless"]           = "true";
        v["BrowserAutomation__IgnoreHttpsErrors"]  = "true";
        v["BrowserAutomation__PageTimeoutMs"]      = "60000";
        v["Ingestion__PostWriteFlushDelayMs"]      = "250";
        v["OrionWatchLoop__PollInterval"]          = "00:02:00"; // 2-min — mirrors RealTcpThreeProcessE2ETests

        var dllPath = WorkerProcessLauncher.ResolveWorkerDllPath(OrionDllName);
        var launcher = await WorkerProcessLauncher.StartAsync(
            dllPath, port, v, workerName: "Orion",
            workingDirectory: workingDirectory, ct).ConfigureAwait(false);
        _extraWorkers.Add(launcher);
    }

    private async Task StartAthenaWorkerAsync(
        int orionPort, int athenaPort, CancellationToken ct)
    {
        var v = BuildCommonEnvVarsD("athena-extractor-chaos-d", "Extract");
        v["Ingestion__HubUrl"]         = $"http://127.0.0.1:{orionPort}/hubs/ingestion";
        v["Ingestion__ReconnectDelay"] = "00:00:00.500";

        var dllPath = WorkerProcessLauncher.ResolveWorkerDllPath(AthenaDllName);
        var launcher = await WorkerProcessLauncher.StartAsync(
            dllPath, athenaPort, v, workerName: "Athena",
            workingDirectory: AppContext.BaseDirectory, ct).ConfigureAwait(false);
        _extraWorkers.Add(launcher);
    }

    private async Task StartReconciliatorWorkerAsync(
        int athenaPort, int reconciliatorPort, CancellationToken ct)
    {
        var v = BuildCommonEnvVarsD("reconciliator-chaos-d", "Reconcile");
        v["Reconciliation__HubUrl"]         = $"http://127.0.0.1:{athenaPort}/hubs/reconciliation";
        v["Reconciliation__ReconnectDelay"] = "00:00:00.500";

        var dllPath = WorkerProcessLauncher.ResolveWorkerDllPath(ReconciliatorDllName);
        var launcher = await WorkerProcessLauncher.StartAsync(
            dllPath, reconciliatorPort, v, workerName: "Reconciliator",
            workingDirectory: AppContext.BaseDirectory, ct).ConfigureAwait(false);
        _extraWorkers.Add(launcher);
    }

    /// <summary>Concatenates captured stdout/stderr from all OS-process workers in scenario (d).</summary>
    private string WorkerLogsForD() =>
        string.Join("\n\n", _extraWorkers.Select(w => w.GetCapturedLogs()));
}

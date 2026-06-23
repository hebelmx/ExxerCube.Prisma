using System.Collections.Concurrent;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Prisma.Orion.Ingestion;
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
///   <c>IsComplete</c> = <see langword="true"/> (nothing to flag), and the partial-case gate asserts the
///   best-effort ingestion/handoff contract before the Stage-4 persistence point. (As of PRISMA-E2-S4,
///   2026-06-20, the partial-case gate now runs the full extraction pipeline — the Tesseract second-init
///   deadlock that previously forced <c>runExtractionPipeline: false</c> was fixed.) Review-case
///   persistence is covered directly by GH #6's own Testcontainers integration tests (incomplete-case
///   persistence, idempotency/heal) — see commit <c>e3fd56a</c>.</item>
/// </list>
/// <para>
/// Isolated from <see cref="MaxFidelityGatePartialCaseE2ETests"/> via the <c>MaxFidelityGate</c> collection
/// (serialized, never parallel) and separate fixture instances. In CI, prefer running this scenario alone:
/// <c>--filter-query "/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"</c>
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "MaxFidelityGate")]
[Collection("MaxFidelityGate")]
public sealed class MaxFidelityGateFullPipelineE2ETests : MaxFidelityGateE2EBase
{
    /// <summary>
    /// Drives one real SIARA case from a live-sim pull through all three worker processes — real OCR, real
    /// multi-source fusion, real classification, real SIRO XML export — and asserts the document survives the
    /// pipeline and that audit rows are persisted to real SQL.
    /// </summary>
    [Fact(Timeout = 1_500_000)] // 25 min hard cap: login + sim + native OCR + Testcontainers SQL + 3 hops are
                                // slow but bounded. Aligns with the class doc's "~6–20 min" worst case (the
                                // prior 15 min cap was below it and flaked on a contended/throttled box).
    public async Task RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit()
    {
        var ct = TestContext.Current.CancellationToken;

        // ── STEP 1: Real headless browser login → capture the credential-free storage-state ──
        var storageState = await LoginAndCaptureStorageStateAsync(ct);
        storageState.ShouldNotBeNullOrEmpty("the simulator login must yield an authenticated storage-state");

        // ── STEP 2: Boot the three real worker hosts wired to SQL + the live sim ──
        BuildThreeHostsWithDb(storageState);

        // ── STEP 3: Subscribe to the Reconciliator's terminal events ──
        // Stage 5 now emits TWO ExportCompletedEvents: SiroXml + DatosCargaOficioXlsx.
        // Collect all export events into a list; also gate on the Xlsx one specifically.
        var allExportEvents = new ConcurrentBag<ExportCompletedEvent>();
        var siroXmlExportSource = new TaskCompletionSource<ExportCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var datosCargaExportSource = new TaskCompletionSource<ExportCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var processingCompletedSource = new TaskCompletionSource<DocumentProcessingCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var exportSub = _reconciliatorApp!.ReconciliatorEventPublisher
            .GetEventStream<ExportCompletedEvent>()
            .Subscribe(e =>
            {
                allExportEvents.Add(e);
                if (e.Format == "SiroXml")
                    siroXmlExportSource.TrySetResult(e);
                else if (e.Format == "DatosCargaOficioXlsx")
                    datosCargaExportSource.TrySetResult(e);
            });
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
            siroXmlExportSource.Task,
            TimeSpan.FromMinutes(10),
            "ExportCompletedEvent(SiroXml) — the real case did not traverse the full pipeline (live download → OCR → fusion → classify → export)",
            ct);

        // Step 6 (checklist A #7): wait for the Datos Carga xlsx export event alongside SIRO XML.
        // The Reconciliator Program.cs registers AddDatosCargaOficioExportServices (line 77), so Stage 5
        // emits this event immediately after the SiroXml one.
        var datosCargaEvent = await AwaitOrFailAsync(
            datosCargaExportSource.Task,
            TimeSpan.FromMinutes(1),
            "ExportCompletedEvent(DatosCargaOficioXlsx) — Stage 5 must emit the Datos Carga xlsx event " +
            "in addition to the SIRO XML export (GateReconciliatorApp wires AddDatosCargaOficioExportServices)",
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
        // Content-fidelity: the SIRO XML is now persisted to shared storage by Stage 5 (IStoragePathResolver
        // wired in Prisma.Reconciliator.Worker/Program.cs). Parse it and assert key elements are present.
        var siroFiles = Directory.GetFiles(_sharedStorageDir, "*.siro.xml", SearchOption.AllDirectories);
        siroFiles.Length.ShouldBeGreaterThanOrEqualTo(1,
            "Stage 5 must have written the SIRO XML file to shared storage " +
            "(IStoragePathResolver is wired in Prisma.Reconciliator.Worker/Program.cs)");

        System.Xml.Linq.XNamespace siroNs = "http://siro.regulatory.namespace";
        var siroDoc = System.Xml.Linq.XDocument.Load(siroFiles[0]);
        siroDoc.Root!.Name.ShouldBe(siroNs + "SiroResponse",
            "SIRO XML root element must be {http://siro.regulatory.namespace}SiroResponse");
        siroDoc.Root.Element(siroNs + "NumeroExpediente")!.Value
            .ShouldNotBeNullOrWhiteSpace("NumeroExpediente must be populated in the SIRO XML from the fused expediente");
        siroDoc.Root.Element(siroNs + "NumeroOficio")!.Value
            .ShouldNotBeNullOrWhiteSpace("NumeroOficio must be populated in the SIRO XML from the fused expediente");
        // At least one descriptive element must carry a NON-EMPTY value (both are always emitted, so
        // existence proves nothing — a populated value proves real fused data reached the export).
        var areaDescripcion = siroDoc.Root.Element(siroNs + "AreaDescripcion")?.Value;
        var autoridadNombre = siroDoc.Root.Element(siroNs + "AutoridadNombre")?.Value;
        (!string.IsNullOrWhiteSpace(areaDescripcion) || !string.IsNullOrWhiteSpace(autoridadNombre))
            .ShouldBeTrue(
                "at least one descriptive element (AreaDescripcion or AutoridadNombre) must carry a " +
                "non-empty value from the fused expediente");

        // B-Step6) Datos Carga de Oficio xlsx export (checklist Step 6 / A #7).
        datosCargaEvent.FileId.ShouldBe(fileId,
            "DatosCargaOficioXlsx event FileId must match the ingested case");
        datosCargaEvent.Format.ShouldBe("DatosCargaOficioXlsx",
            "Stage 5 must emit a second ExportCompletedEvent with Format=DatosCargaOficioXlsx");
        datosCargaEvent.ExportedSizeBytes.ShouldBeGreaterThan(0,
            "the Datos Carga xlsx must have a non-zero byte size");

        // Verify the xlsx is physically present on shared storage (IStoragePathResolver wired in the Reconciliator).
        var xlsxFiles = Directory.GetFiles(_sharedStorageDir, "*.datos-carga-oficio.xlsx", SearchOption.AllDirectories);
        xlsxFiles.Length.ShouldBeGreaterThanOrEqualTo(1,
            "Stage 5 must have written the Datos Carga xlsx file to shared storage " +
            "(IStoragePathResolver is wired in Prisma.Reconciliator.Worker/Program.cs)");

        // Open the xlsx and verify the 24 mandatory headers are present.
        using (var wb = new ClosedXML.Excel.XLWorkbook(xlsxFiles[0]))
        {
            var ws = wb.Worksheets.First();
            var headerCount = Enumerable.Range(1, 30)
                .Select(c => ws.Row(1).Cell(c).GetString()?.Trim())
                .Count(h => !string.IsNullOrWhiteSpace(h));
            headerCount.ShouldBe(24,
                "the Datos Carga de Oficio xlsx on shared storage must have exactly 24 column headers");
        }

        // Both export formats must be present in the collected events.
        var formats = allExportEvents.Select(e => e.Format).ToList();
        formats.ShouldContain("SiroXml",
            "the pipeline must emit a SiroXml ExportCompletedEvent");
        formats.ShouldContain("DatosCargaOficioXlsx",
            "the pipeline must also emit a DatosCargaOficioXlsx ExportCompletedEvent (checklist Step 6)");

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

        // ProcessId adoption (PRISMA-E2-S5): every audit row persisted for this run must carry a
        // non-null, non-empty ProcessId so the audit trail is fully traceable across all three processes.
        // This assertion will fail if any LogAuditAsync call site omits or nulls the processId argument.
        var rowsMissingProcessId = auditRows
            .Where(a => string.IsNullOrWhiteSpace(a.ProcessId))
            .ToList();
        rowsMissingProcessId.ShouldBeEmpty(
            $"every audit row for FileId {fileId} must have a non-null, non-empty ProcessId " +
            $"(found {rowsMissingProcessId.Count} row(s) with null/empty ProcessId — " +
            $"check all LogAuditAsync call sites pass processId: from ISiaraActorIdentityProvider)");
    }
}

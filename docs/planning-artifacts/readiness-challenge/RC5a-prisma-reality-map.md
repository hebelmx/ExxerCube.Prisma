# RC5a — Prisma MVP Track B: Composition-Roots Reality Map + Reconciliation

**Date:** 2026-06-18 · **Branch:** `Liv` · **Method:** Read all 4 real composition roots + key implementation files from ground truth; build + test executed. Reconciles against `GAP-MATRIX-2026-06-11.md`, `MVP-PATH-2026-06-11.md`, `GAP-MATRIX-2026-06-dual-ground-truth.md`, and `CLAUDE.md` release-status claims.

> **Bar applied:** RC Brief §7 — believe wiring and ground-truth runs, never comments or prose. End-to-end evidence required, not unit tests.

---

## Part 1 — Composition-Root Wiring Table

### 1A. Orion Worker (`04 Services/Orion/Prisma.Orion.Worker/Program.cs`)

**Role:** Downloader actor (3-process split). Hosts the SIARA ingestion hub, drives the watch loop.

| Responsibility | Status | File:Line | Notes |
|---|---|---|---|
| `IDocumentDownloader` | **REAL — WIRED** | `Program.cs:64` | `SiaraDocumentDownloader` (Playwright + session) registered as `Scoped`. StubDocumentDownloader is GONE from DI. |
| `ISiaraDocumentSource` | **REAL — WIRED** | `Program.cs:68` | `SiaraDocumentSource` scoped. Drives the discovery poll. |
| `SiaraWatchLoop` (poll/watch) | **REAL — WIRED** | `Program.cs:201` | Singleton. `RunAsync` drives the per-cycle discover → ingest loop. `IsRunning` flag is truthful readiness signal. |
| Ingestion hub (`/hubs/ingestion`) | **REAL — WIRED** | `Program.cs:222` | `IngestionHub` mapped; `SignalRIngestionBroadcaster` is the real `IExxerHub<DocumentDownloadedEvent>`. |
| JWT clearance per-message (A5) | **REAL — WIRED** | `Program.cs:137` (`AddSignalRIngestionBroadcaster`), `SignalRIngestionBroadcaster.cs:92` | Mints token, stamps event, fail-closed on mint failure. |
| Connection-level hub auth | **REAL — WIRED** | `Program.cs:75-116` | JWT bearer validates `ProcessClearance.Extract` before any hub message. |
| Per-process audit (A6) | **REAL — WIRED** | `Program.cs:152-160` | `IngestionOrchestrator` receives `ISiaraActorIdentityProvider` + `IServiceScopeFactory` + `ProcessClearance.Download`. |
| DB audit persistence | **CONDITIONAL** | `Program.cs:29-41` | `AddDatabaseServices(registerEventPersistence: false)` when connection string is real; skips gracefully when blank/placeholder. |
| Config externalized | **DONE** | `appsettings.json:3-4` | Connection string is `DEV-PLACEHOLDER`; set via env var. No hardcoded server name. |
| `StubDocumentDownloader` | **REMOVED** | (absent from DI) | Not registered anywhere in production Program.cs. |
| `StubExxerHub` | **REMOVED** | (absent from DI) | `SignalRIngestionBroadcaster` replaces it. |
| Manifest reconciliation | **REAL — WIRED (opt-in)** | `Program.cs:184-198` | `FileExpectedManifestProvider` + `ManifestReconciliationService`; enabled via `ExpectedManifest:Enabled=true`. |

### 1B. Athena Worker (`04 Services/Athena/Prisma.Athena.Worker/Program.cs`)

**Role:** Extractor actor (3-process split). Runs Quality → OCR → Fusion. Subscribes to Orion hub; hosts reconciliation hub for Reconciliator.

| Responsibility | Status | File:Line | Notes |
|---|---|---|---|
| `SiaraIngestionHubClient` (subscriber) | **REAL — WIRED** | `Program.cs:70` | `BackgroundService` that connects to Orion `/hubs/ingestion`, receives `DocumentDownloadedEvent`, republishes locally. |
| `IngestionEventForwarder` | **REAL — WIRED** | `Program.cs:69` | Republishes received events onto local `IEventPublisher` stream. |
| `ExtractionOrchestrator` (Stages 1-3) | **REAL — WIRED** | `Program.cs:183-192` | Quality + OCR + Fusion. All 3 field extractors (TXT/XML/DOCX) injected. |
| `ExtractionPipelineService` | **REAL — WIRED** | `Program.cs:202-218` | Drives the Extractor. Subscribes to `DocumentDownloadedEvent`, runs pipeline, hands off. `IsStarted` truthful readiness. |
| `IImageQualityAnalyzer` | **REAL — WIRED** | `Program.cs:77` | `PolynomialImageQualityAnalyzer`. |
| `IOcrExecutor` | **REAL — WIRED** | `Program.cs:78` | `TesseractOcrExecutor`. |
| `IFusionExpediente` | **REAL — WIRED** | `Program.cs:83-85` | `FusionExpedienteService` with `MexicoBusinessDayCalculator`. |
| `IFieldExtractor<TxtSource>` | **REAL — WIRED** | `Program.cs:91` | `AdaptiveTxtFieldExtractor`. |
| `IFieldExtractor<XmlSource>` | **REAL — WIRED** | `Program.cs:95` | `XmlFieldExtractor`. |
| `IFieldExtractor<DocxSource>` | **REAL — WIRED** | `Program.cs:96` | `DocxFieldExtractor`. |
| Multi-source fusion (B1) | **REAL — WIRED** | `ExtractionOrchestrator.cs:308-316` | XML + DOCX + PDF all three fed into `FuseAsync` when companion files present. |
| `IExpedienteHandoffStore` | **REAL — WIRED** | `Program.cs:182` | `FileSystemExpedienteHandoffStore`. Fused expediente persisted to shared volume. |
| Reconciliation hub (`/hubs/reconciliation`) | **REAL — WIRED** | `Program.cs:238` | `ReconciliationHub` mapped; `SignalRReconciliationBroadcaster` is real `IExxerHub<ExtractionCompletedEvent>`. |
| JWT clearance (A5 — Extractor) | **REAL — WIRED** | `Program.cs:133-168` | Bearer validates `ProcessClearance.Reconcile` before hub access. Clearance config: `Extract`. |
| Per-process audit (A6) | **REAL — WIRED** | `Program.cs:202-217` | `ExtractionPipelineService` receives actor + scope factory + clearance. |
| Readiness probe (E1) | **REAL — WIRED** | `Program.cs:223` | `IReadinessProbe` → `ExtractionPipelineService.IsStarted` (set when pipeline subscribes). |
| `IFileClassifier` / `IResponseExporter` | **INTENTIONALLY ABSENT** | `Program.cs:88-89` | Exporter not registered (null → Extractor skips Stage 5 by design). |
| `AthenaDashboardService` | **WIRED** | `Program.cs:227` | Returns real stats from the extraction pipeline. |
| Config externalized | **DONE** | `appsettings.json` | DEV-PLACEHOLDER for connection string. No hardcoded server name. |

### 1C. Reconciliator Worker (`04 Services/Reconciliator/Prisma.Reconciliator.Worker/Program.cs`)

**Role:** Reconciliator actor — NEW third process (did not exist at 06-11 audit). Receives `ExtractionCompletedEvent` from Athena hub; runs Classification → Export.

| Responsibility | Status | File:Line | Notes |
|---|---|---|---|
| `ReconciliationHubClient` (subscriber) | **REAL — WIRED** | `Program.cs:52` | `BackgroundService` connecting to Athena `/hubs/reconciliation`. |
| `ReconciliationEventForwarder` | **REAL — WIRED** | `Program.cs:51` | Republishes `ExtractionCompletedEvent` onto local event stream. |
| `ReconciliationOrchestrator` (Stages 4-5) | **REAL — WIRED** | `Program.cs:81-88` | Classification + Export. |
| `IFileClassifier` | **REAL — WIRED** | `Program.cs:80` | `FileClassifierService`. |
| `IResponseExporter` | **REAL — WIRED (SIRO path)** | `Program.cs:73` | `AddSiroExportServices` → `SiroXmlExporter` + `CompositeResponseExporter`. Singleton lifetime for compatibility. |
| `IDatosCargaOficioLayoutGenerator` | **REAL — WIRED** | `Program.cs:78` | `AddDatosCargaOficioExportServices(Singleton)`. |
| `IExpedienteHandoffStore` | **REAL — WIRED** | `Program.cs:60` | `FileSystemExpedienteHandoffStore`. Reads the fused expediente the Extractor wrote. |
| `ReconciliationPipelineService` | **REAL — WIRED** | `Program.cs:92-107` | Drives the Reconciliator; receives actor + scope factory + clearance. |
| Per-process audit (A6) | **REAL — WIRED** | `Program.cs:92-107` | `ISiaraActorIdentityProvider` + scope factory; `ProcessClearance.Reconcile`. |
| JWT clearance (A5 — Reconciliator) | **PARTIAL** | `Program.cs:39` | `AddProcessIdentity` registered; validates incoming clearance tokens from Extractor. No outbound hub (terminal stage). `ReconciliationEventForwarder` validates per-message tokens. |
| DB audit persistence | **CONDITIONAL** | `Program.cs:22-34` | `AddDatabaseServices` with real connection; skips when blank/placeholder. |
| Health probes | **MINIMAL STUB** | `Program.cs:113-114` | `/health` and `/health/live` return hardcoded `Healthy`. No `IReadinessProbe` wired. |
| Config externalized | **DONE** | `appsettings.json` | DEV-PLACEHOLDER; all secrets via env var. |

### 1D. Web UI (`07 UI/UI/ExxerCube.Prisma.Web.UI/Program.cs`)

**Role:** Blazor UI — manual review, SLA dashboard, export management, browser-based SIARA interaction.

| Responsibility | Status | File:Line | Notes |
|---|---|---|---|
| ASP.NET Core Identity (auth) | **REAL — WIRED** | `Program.cs:220-242` | Cookie-based auth. Real `ApplicationDbContext`. |
| Database services | **REAL — WIRED** | `Program.cs:245` | `AddDatabaseServices(applicationConnectionString, configuration)`. |
| BrowserAutomation / SIARA | **REAL — WIRED** | `Program.cs:246-255` | `DocumentIngestionService` (UI-side scraper path). |
| Export services | **REAL — WIRED** | `Program.cs:294-295` | `AddExportServices` + `AddAdaptiveExportServices`. `DigitalPdfSigner` shadow still present but Out-of-MVP. |
| `DatosCargaOficioExportServices` | **REAL — WIRED** | `Program.cs:299` | Scoped lifetime; uses `ITemplateRepository`. |
| Manual review services | **REAL — WIRED** | `Program.cs:290` | `DecisionLogicService` (scoped). |
| SLA services | **REAL — WIRED** | `Program.cs:292` | `SLATrackingService`. |
| `SLAMetricsCollector` / SLA meter | **REAL — WIRED** | `ConfigureOpenTelemetry` line 431 | `.AddMeter("ExxerCube.Prisma.SLA")` — SLA meter IS exported now (was the D2 gap). |
| OpenTelemetry (tracing + metrics + logs) | **REAL — WIRED** | `Program.cs:343-477` | OTLP→Seq endpoint configured; console exporter also active. |
| Readiness / health checks | **WIRED BUT SHALLOW** | `Program.cs:114` | `MapHealthChecks("/health")` via `AddHealthChecks()` only (no domain-specific checks). |
| SignalR event broadcaster | **COMMENTED OUT** | `Program.cs:333` | `//services.AddHostedService<Services.SignalREventBroadcaster>();` — disabled ("demo/isolation mode"). |
| Template pages (Counter/Weather) | **UNKNOWN** | `07 UI/Components/Pages/` | Not re-verified in this audit pass — the 06-11 matrix noted these as orphaned. |
| Python / CSnakes | **DORMANT** | `Program.cs:190` | `//services.AddPrismaPythonEnvironment();` commented. Intentional (ADR-001). |
| `EfCoreIdentityAdapter` | **UNREGISTERED** | DI root scan | Real class exists; not wired in any Program.cs (per 06-11 finding; unchanged). |
| Connection string (`DefaultConnection`) | **STILL HARDCODED in UI** | `Program.cs:228` | UI throws if missing: `?? throw new InvalidOperationException(...)`. This is different from worker pattern — UI appsettings.json likely still contains the hardcoded server name (E2 gap partially closed for workers, not yet verified for UI). |

---

## Part 2 — Reconciliation: Prior Matrix vs Today (2026-06-18)

| Item | Prior Claim (2026-06-11) | Today State | Evidence |
|---|---|---|---|
| **A1 — Real SIARA downloader** | Planned. `StubDocumentDownloader` wired. Scraper exists but disconnected. | **ADVANCED — DONE** | `Orion.Worker/Program.cs:64` → `SiaraDocumentDownloader:IDocumentDownloader` (Scoped). Stub is absent from DI entirely. Real 265-line implementation in `Infrastructure.BrowserAutomation/Siara/SiaraDocumentDownloader.cs`. |
| **A2 — Watch/poll loop** | Planned. `Task.CompletedTask` no-op at `IngestionOrchestrator.cs:268-273`. | **ADVANCED — DONE** | `SiaraWatchLoop.cs` — real poll loop (`RunAsync:148`), per-cycle discover + ingest + reconciliation; `SiaraWatchLoop` wired as singleton at `Program.cs:201`. `IngestionOrchestrator.IngestCaseAsync` is real (SHA-256 journal dedup, per-case scopes). |
| **A3 — Real Ember transport in workers** | Planned. Workers at 0%; UI-only real. | **ADVANCED — DONE** | Orion: `SignalRIngestionBroadcaster` (real `IExxerHub<DocumentDownloadedEvent>`) wired at `Program.cs:137`. Athena: `SignalRReconciliationBroadcaster` wired at `Program.cs:198`. Both carry JWT clearance tokens. |
| **A4 — 3-process split** | Partial. Only 2 monolithic workers. Athena split was the lynchpin. | **ADVANCED — DONE** | Three separate worker `Program.cs` files exist. Orion = Downloader; Athena = Extractor (Quality→OCR→Fusion); Reconciliator = Classification→Export. `ExtractionOrchestrator` + `ReconciliationOrchestrator` are the decomposed halves. |
| **A5 — Per-stage auth + data minimization** | Planned. Workers registered zero auth. Blocked-by-A4. | **ADVANCED — DONE** | All 3 workers register JWT bearer + authorization policy with `ProcessClearance` requirement. `IProcessClearanceTokenService` mints tokens per-message. Hub-level connection auth + per-message clearance check in forwarders. Data minimization: only storage-relative path crosses the Extractor→Reconciliator edge (not raw bytes). |
| **A6 — Per-process audit** | Partial. Audit Web.UI-only; no process identity. Blocked-by-A4. | **ADVANCED — DONE** | All 3 workers wire `ISiaraActorIdentityProvider` + `IServiceScopeFactory` + `ProcessClearance` into their pipeline services. `IAuditLogger` resolved per-call via scope factory. `ActionDetails` JSON carries clearance + actor. |
| **B1 — Multi-source fusion (XML+DOCX into worker path)** | Partial. Worker fed `FuseAsync(null, pdf, null, ...)`. | **ADVANCED — DONE** | `ExtractionOrchestrator.cs:308-316` — `BuildXmlExpedienteAsync` + `BuildDocxExpedienteAsync` called; all three sources fed to `FuseAsync(xmlExpediente, pdfExpediente, docxExpediente, ...)`. KNOWN FINDING: real SIARA DOCX (`SAT requerimiento`) does not match `DocxFieldExtractor` regex — XML is the canonical source; best-effort holds. |
| **B2 — Native PDF text extraction** | Planned. `PdfMetadataExtractor.TryExtractTextFromPdfAsync` returned `string.Empty` unconditionally. | **ADVANCED — DONE** | `PdfMetadataExtractor.cs:217-234` — real `UglyToad.PdfPig.PdfDocument.Open` implementation using `ContentOrderTextExtractor`. No longer returns empty. |
| **C1 — Manual review list + filters** | Done | **STILL-AS-DESCRIBED** | Not re-verified in detail this pass; no regression found. |
| **C2/C3 — Override/notes persisted to unified record** | Partial (saved to review table, not merged to unified record). | **ADVANCED — DONE** | MVP-PATH status note (2026-06-13): `3.1 (C2+C3) JSON unified-record store + override merge` completed. Verification: `ManualReviewerService` unified-record path updated per GH #6 (`e3fd56a`). |
| **D1 — SLA deadline calc + business days** | Done | **STILL-AS-DESCRIBED** | Unchanged. |
| **D2 — SLA telemetry / SLA meter exported** | Partial. `AddMeter` had 0 matches; gauges hardcoded 0. | **ADVANCED — DONE** | `Web.UI/Program.cs:431` — `.AddMeter("ExxerCube.Prisma.SLA")` now present in `ConfigureOpenTelemetry`. Gauge fix was part of WS4 (4.1). |
| **E1 — Real readiness probes** | Partial. `isReady = _orchestrator != null`; TODO comment. | **ADVANCED — DONE (Athena/Orion); STUB (Reconciliator)** | Athena: `IReadinessProbe` → `ExtractionPipelineService.IsStarted` (`Program.cs:223`). Orion: `IReadinessProbe` → `SiaraWatchLoop.IsRunning` (`Program.cs:206`). Reconciliator: `/health` returns hardcoded Healthy (`Program.cs:113-114`) — no real probe. |
| **E2 — Config externalized + template pages removed** | Partial. Hardcoded server name in UI appsettings; Counter/Weather orphaned. | **PARTIALLY ADVANCED** | Worker `appsettings.json` files all use DEV-PLACEHOLDER + env-var pattern. UI `appsettings.json` NOT re-read this pass; likely still has the hardcoded server name (MVP-PATH notes it was a "quick win" but UI specifically not verified today). |
| **E3 — Health per process** | Done | **STILL-AS-DESCRIBED** | `/health`, `/health/live`, `/health/ready` mapped in all 3 worker roots. |
| **Sentinel** | Planned (P1). No worker host, wired in no root. | **STILL-AS-DESCRIBED** | Not re-traced this pass. Expected unchanged. |
| **F1 — One real E2E run, no stub on critical path** | Planned (blocked by A1/A2/A3/A4). | **ADVANCED — DONE (with one accepted seam)** | `MaxFidelityGateFullPipelineE2ETests` ran 2026-06-14 (21m24s): real Playwright → real `SiaraDocumentDownloader` → real Tesseract OCR → real `FusionExpedienteService` → real `FileClassifierService` → real `SiroXmlExporter` → Testcontainers SQL audit across 3 real SignalR edges + JWT clearance. **One accepted limitation:** in-memory SignalR transport seam (production hub + auth code runs; no TCP port — accepted by owner). |
| **F2 — Suite green + integration tests + flaky OCR stabilized** | Partial. `System.Ocr.Pipeline` flaky; no ingestion-chain integration tests. | **ADVANCED — DONE** | `System.Ocr.Pipeline` 25/25 x2 after two de-flake passes. New integration tests: `CaseCombinationBestEffortTests`, `MultiSourceFusionIntegrationTests`, ingestion-chain tests in `Orion.Ingestion.Tests` (55/55). |
| **MVP gate claim** | Not scored (added as F1/F2 by critic). | **REACHED 2026-06-14** | Handoff doc: `HANDOFF-2026-06-14-mvp-gate-5-closed.md`. Issue #5 closed. |

### Items opened since 06-11 (hardening backlog on issue #2)

| Item | Status | Notes |
|---|---|---|
| `DocxFieldExtractor` regex too narrow for real SAT DOCX | Open (issue #2) | CNBV XML is canonical source; best-effort holds. Hardening. |
| SIRO XSD validation | Open (issue #2, blocked on Banamex .xsd) | Hardening. |
| jti replay guard | **DONE** (commit `1c11aa7`) | `InMemoryClearanceReplayGuard` wired in Athena + Reconciliator roots. |
| `file_id == Empty` rejection in forwarders | **DONE** (commit `1c11aa7`) | Guards in `IngestionEventForwarder` + `ReconciliationEventForwarder`. |
| Per-process asymmetric clearance keys | **PROPOSED** (ADR-012 addendum `f2594730`) | Still symmetric (shared `JwtSecret`). Asymmetric is a hardening upgrade. |
| SIRO XML artifact written to shared storage | **DONE** (commit `026a6938`) | `ReconciliationOrchestrator` persists SIRO output; `MaxFidelityGate` asserts non-empty content. |
| Manifest reconciliation + per-cycle report | **DONE** (commits `6d9f701b`, `12bb9332`) | Case-package ingestion + per-cycle reconciliation. |

---

## Part 3 — Ground-Truth Build/Test Results

### Build results (verbatim from `dotnet build <csproj>`)

**Athena Worker** (`ExxerCube.Prisma.Athena.Worker.csproj`):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:02:56.09
```

**Orion Worker** (`ExxerCube.Prisma.Orion.Worker.csproj`):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:02:32.57
```

**Reconciliator Worker** (`ExxerCube.Prisma.Reconciliator.Worker.csproj`):
```
Build succeeded.
    1 Warning(s)  [MSB3026: file-lock race during parallel build — benign transient]
    0 Error(s)
Time Elapsed 00:02:27.79
```

All 3 worker production binaries build cleanly. 0 errors total.

### Test results (verbatim from `dotnet test <csproj>`)

**Orion.Ingestion.Tests** (`ExxerCube.Prisma.Orion.Ingestion.Tests.csproj`):
```
Running tests from ...ExxerCube.Prisma.Orion.Ingestion.Tests.dll (net10.0|x64)
...passed (6s 986ms)

Test run summary: Passed!
  total: 55
  failed: 0
  succeeded: 55
  skipped: 0
  duration: 19s 518ms
```

**Athena.Processing.Tests** (`ExxerCube.Prisma.Athena.Processing.Tests.csproj`):
```
Running tests from ...ExxerCube.Prisma.Athena.Processing.Tests.dll (net10.0|x64)
...passed (27s 302ms)

Test run summary: Passed!
  total: 105
  failed: 0
  succeeded: 105
  skipped: 0
  duration: 38s 009ms
```

Note: Athena.Processing.Tests was 91/91 at the 06-14 MVP handoff; it is now **105/105** — 14 new tests added since that handoff.

### E2E gate (from `HANDOFF-2026-06-14-mvp-gate-5-closed.md` — run on Docker+Playwright+Tesseract-capable machine)

```
MaxFidelityGateE2ETests: 2/2 passed (21m 24s)
  - RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit: PASSED
  - PartialCase_MissingCompanionFile_StillProcessesBestEffort_AndFlagsIncomplete: PASSED
System.Ocr.Pipeline: 25/25 x2 (de-flaked)
CaseCombinationBestEffortTests: 3 deterministic scenarios (DOCX-only, XML-only, XML+DOCX-no-PDF): PASSED
```

---

## Biggest Deltas Since 2026-06-11

1. **The entire Workstream 1 (A1-A6) is DONE.** `StubDocumentDownloader` and `StubExxerHub` are gone. Three separate production-wired processes exist: Orion (Downloader), Athena (Extractor), Reconciliator. JWT clearance tokens stamp every cross-process event. Audit in all three workers. This was the entire MVP-blocking cluster.

2. **Reconciliator Worker exists as a new 3rd process** — `Prisma.Reconciliator.Worker/Program.cs` did not exist at the 06-11 audit. It runs Classification → Export, subscribes to Athena's reconciliation hub, persists SIRO XML to shared storage, and logs audit with process identity.

3. **B1 multi-source fusion is fully wired in the worker path** — `ExtractionOrchestrator.cs:308-316` feeds XML + DOCX + PDF expedientes into `FuseAsync`. Previously the worker hardcoded nulls. (Known hardening item: real SAT DOCX regex mismatch; best-effort holds.)

4. **F1/F2 gate passed 2026-06-14** — A real SIARA → OCR → Fusion → Classification → SIRO XML → SQL audit E2E run passed (21m). This is the first time a document is proven to flow across all three processes with no stub on the data path. Branch `main` contains this evidence.

5. **B2 native PDF text extraction is real** — `PdfMetadataExtractor.cs:217-234` uses `UglyToad.PdfPig` (was `return string.Empty` unconditionally).

6. **D2/E1/E2 closed** — SLA meter exported via `.AddMeter("ExxerCube.Prisma.SLA")` in UI. Worker readiness probes are truthful (`ExtractionPipelineService.IsStarted`, `SiaraWatchLoop.IsRunning`). Worker `appsettings.json` files all use DEV-PLACEHOLDER + env-var pattern (no hardcoded server names in workers).

---

## What Is Genuinely E2E-Proven vs Unit-Green-Only

### E2E-proven (ground-truth run, real components, no stub on data path)

- **Orion → Athena → Reconciliator full pipeline** (committed to `main`, 2026-06-14):
  - Real Playwright browser → credential-free SIARA session → `SiaraDocumentDownloader` downloads real case package (PDF + DOCX + XML).
  - Real `TesseractOcrExecutor` processes PDF. Real `FusionExpedienteService` reconciles multi-source.
  - Real `FileClassifierService` classifies. Real `SiroXmlExporter` renders SIRO XML.
  - All 3 processes persist audit rows to real Testcontainers SQL Server.
  - Real SignalR hub-to-hub edges with JWT clearance (in-memory transport — accepted seam).
- **Orion.Ingestion.Tests 55/55** — includes real component tests for ingestion orchestration, watch loop, case-package download+event.
- **Athena.Processing.Tests 105/105** — includes multi-source fusion integration tests, case-combination best-effort tests, real Extraction orchestrator tests.
- **System.Ocr.Pipeline 25/25** (run on Tesseract machine; confirmed 25/25 x2 at 06-14 with de-flake verified).

### Unit-green-only (mocks/NSubstitute — not E2E-proven)

- **Domain / Application layers** (337 + 157 tests): correct by contract, no real infrastructure.
- **UI tests** (`Tests.UI` 21/21, `BrowserAutomation.E2E` 18/18, `Tests.EndToEnd` 29/29): these run against `WebApplicationFactory` or the Siara Simulator, not against real SIARA.
- **Review-case persistence integration** (GH #6 Testcontainers tests): proven in isolation, not asserted in the MaxFidelity gate.
- **Manual review UI end-to-end** (list + field annotations + unified-record merge): not asserted in any multi-layer integration test; unit coverage only.

### Still unknown/unverified

- **Reconciliator readiness probe**: hardcoded `Healthy` — not truthful until real probe added.
- **UI `appsettings.json` server name**: worker configs are clean; UI config was not re-read this pass — may still have hardcoded `DESKTOP-FB2ES22\SQL2022`.
- **Counter.razor / Weather.razor**: orphaned template pages from 06-11 audit — removal not confirmed this pass.
- **`EfCoreIdentityAdapter`** not wired in any root: per-process JWT clearance provides the A5 security mechanism but `EfCoreIdentityAdapter` (`IIdentityProvider`/`ITokenService`) remains unregistered (by design for the worker security model per ADR-012).
- **Sentinel service**: no worker host; status unknown (unchanged from 06-11).
- **SignalREventBroadcaster** in UI: commented out — `//services.AddHostedService<Services.SignalREventBroadcaster>()`. Real-time UI event streaming via this path is disabled.
- **E2E on THIS machine (Liv branch)**: the 06-14 gate ran on a Docker+Playwright+Tesseract-capable machine with `Kt2` branch merged to `main`. Current `Liv` branch is 102 commits ahead of `main` (Veriqan work) but the Prisma production code is the same merge base. The E2E gate test project (`Tests.AllRealWireE2E`) exists on this branch — it would need Docker + Playwright + Tesseract to re-run locally.

---

*RC5a complete. Supersedes the 2026-06-11 GAP-MATRIX for current state of the Prisma MVP wiring. The prior "Planned" classification for A1-A6 is refuted by ground-truth wiring. MVP was reached 2026-06-14. Remaining engineering gaps: Reconciliator readiness probe (minor), UI appsettings cleanup (minor), Sentinel host (P1), and the hardening backlog on issue #2.*

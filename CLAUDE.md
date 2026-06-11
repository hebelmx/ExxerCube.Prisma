# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ExxerCube.Prisma is an enterprise OCR document processing system for Spanish legal documents. It combines C# (.NET 10) services with Python ML/OCR modules via CSnakes interop. The system uses a 5-stage pipeline: Quality Analysis → OCR → Fusion/Reconciliation → Classification → Export.

## Build & Test Commands

```bash
# Solution is at Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln (200+ projects)
dotnet restore "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet test "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"

# Build/test a single project
dotnet build "Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Processing/ExxerCube.Prisma.Athena.Processing.csproj"
dotnet test "Prisma/Code/Src/CSharp/08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ExxerCube.Prisma.Athena.Processing.Tests.csproj"

# Run a single test (xUnit v3 filter query syntax)
dotnet test <test-project.csproj> --filter-query "/Namespace/ClassName/MethodName"
dotnet test <test-project.csproj> --filter-query "[category=fast]"
```

Build artifacts go to `E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\` (configured in Directory.Build.props).

## Architecture

**Hexagonal/Clean Architecture** with numbered folder convention:

```
Prisma/Code/Src/CSharp/
├── 01 Core/          Domain (entities, value objects, interfaces) + Application (services)
├── 02 Infrastructure/ Adapters: Database, FileStorage, OCR, Classification, Export, Imaging, Python interop
├── 03 Orchestration/  Pipeline coordination (ProcessingOrchestrator)
├── 04 Services/       Microservices: Orion (ingestion), Athena (processing), Sentinel (monitoring)
├── 07 UI/             Blazor web UI (MudBlazor)
├── 08 Tests/          Mirrors production structure (01-09 subfolders)
└── 09 Testing/        Shared test abstractions, contracts, fixtures
```

**Three Python subsystems** under `Prisma/Code/Src/Python/`:
- `prisma-ocr-pipeline/` — Tesseract-based OCR with watermark removal, deskewing
- `prisma-ai-extractors/` — Vision-Language Models (SmolVLM2, GOT-OCR2, PaddleOCR)
- `Prisma-dumy-generator-AAA/` — Synthetic test document generation (the "document generator")

> ⚠️ **Python tree duplication (known issue, do not blindly dedupe):** the C#
> build is wired (via the CSnakes `SetPythonPathForCSnakes` MSBuild target and
> `<PythonRoot>Python/python</PythonRoot>` in `02 Infrastructure/Infrastructure`)
> to **`Prisma/Code/Src/CSharp/Python/`** — so that copy must NOT be removed. It
> overlaps/diverges with `Src/Python/Prisma-dumy-generator-AAA/`. Reconciling the
> two needs a focused pass. See `docs/development/archive/root-cleanup-2026-06.md`.

## Repository Layout (top level)

```
ExxerCube.Prisma/            (git repo root)
├── Prisma/                  Main product: Code/Src/{CSharp,Python}, Fixtures, scripts, Docs
├── docs/                    Consolidated documentation (see docs/README.md)
├── scripts/                 Operational scripts: docker/ build/ db/ generators/ data-extraction/
├── tools/                   Standalone dev tools (e.g. tools/Siara.Simulator/ — external SIARA portal sim)
├── CLAUDE.md, AGENTS.md     Agent instructions
├── nuget.config, package.json, playwright.config.ts, coverage.runsettings
└── run-coverage[-ci].ps1    Root-coupled coverage runners (CI)
```

The real-time communication / SignalR hub abstraction was **extracted out** of
this repo into the published **`IndFusion.Ember`** NuGet package
(`github.com/hebelmx/IndFusion.Ember`); reference that package rather than
reviving in-repo SignalR code. See `docs/architecture/adr/ADR-009-*`.

## Critical Code Patterns

### Result<T> Pattern (mandatory — no exceptions for business logic)
```csharp
public async Task<Result<T>> ProcessAsync(..., CancellationToken cancellationToken = default)
{
    if (cancellationToken.IsCancellationRequested)
        return ResultExtensions.Cancelled<T>();

    // Validation returns Result, never throws
    if (input is null)
        return Result<T>.WithFailure("Input cannot be null");

    // Chain with functional operations: Map, Bind, Match, ThenAsync
    return Result<T>.WithSuccess(value);
}
```

### CancellationToken (required on every async method)
- Signature: `CancellationToken cancellationToken = default`
- Propagate to all downstream async calls
- Check early: `cancellationToken.IsCancellationRequested` → return `ResultExtensions.Cancelled<T>()`
- Never throw `OperationCanceledException`; catch and convert to `Result`
- In tests use `TestContext.Current.CancellationToken`, not manual tokens

### Null Safety
- Nullable reference types enabled, warnings-as-errors
- Constructor: `ArgumentNullException` for required dependencies
- Method params: validate and return `Result.WithFailure` (not throw)
- Use `IsSuccessMayBeNull` / `IsSuccessNotNull` on Result values

### Async/Await
- `ConfigureAwait(false)` on background/library code
- All async methods accept and propagate `CancellationToken`

## Testing Stack

- **Framework:** xUnit v3 (not v2)
- **Assertions:** Shouldly (`value.ShouldBe(expected)`)
- **Mocking:** NSubstitute (`Substitute.For<IService>()`)
- **Logging:** `Meziantou.Extensions.Logging.Xunit.v3` (real logging, not mocked)
- **Integration:** Testcontainers (MsSql, Ollama), WebApplicationFactory
- **Test naming:** `MethodUnderTest_Scenario_ExpectedBehavior`
- **Do NOT use:** Moq, FluentAssertions

## Logging & Observability

- Serilog structured logging: use `{PropertyName}` placeholders, never string concatenation
- Correlation IDs via `LogBeginScope` with context dictionary
- OpenTelemetry metrics: `Meter`, `Counter<long>`, `Histogram<double>`
- Health checks per service: `/health/live`, `/health/ready`
- Always measure and log durations with `Stopwatch`

## Key Configuration

- **.NET 10**, `LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors=true`
- `EnforceCodeStyleInBuild=true`, `GenerateDocumentationFile=true`
- Centralized package versions via `Directory.Packages.props`
- C#/Python interop via CSnakes.Runtime

## Event Architecture

The system uses **Rx.NET Observables** (not traditional IEventHandler registration):
- `EventPublisher` (Infrastructure) — production implementation using `Subject<DomainEvent>`
- `ProcessingOrchestrator.StartAsync()` subscribes to `GetEventStream<DocumentDownloadedEvent>()`
- `EventPersistenceWorker` subscribes to `GetAllEventsStream()` for audit trail persistence
- `SignalREventBroadcaster` subscribes to `GetAllEventsStream()` for UI real-time updates
- `InMemoryEventBus` — legacy string-based pub/sub, **not used in production** (EventPublisher is the real impl)

## Services

| Service | Role | Location |
|---------|------|----------|
| **Orion** | Document ingestion & download | `04 Services/Orion/` |
| **Athena** | Document processing pipeline | `04 Services/Athena/` |
| **Sentinel** | System monitoring & health | `04 Services/Sentinel/` |

## Release Status

> **MVP audit refreshed 2026-06-11** (post test-suite hardening). The PRD was reconciled
> against discovery, an explicit MVP bar was agreed with the owner, and the gaps were
> re-traced + adversarially verified. **Canonical now:**
> `docs/planning/gap-analysis/GAP-MATRIX-2026-06-11.md` (refreshed matrix) +
> `MVP-PATH-2026-06-11.md` (ordered work plan) +
> `MVP-DEFINITION-2026-06.md` / `PRD-RECONCILIATION-2026-06.md` (scope) +
> `SIARA-AUTH-DESIGN-2026-06.md` (Workstream-1 feature design).
> **Headline:** the core processing + manual-review + SLA surfaces are built; MVP is gated
> almost entirely by **Workstream 1 = ingestion + the security-mandated 3-process split
> (A1–A6)**, plus a short tail of partials. **Owner MVP rulings:** 3-process split IN,
> signed-PDF OUT (P2), full review dashboard IN, multi-source fusion + SLA dashboard +
> native-PDF-text IN, persisted identity OUT. **Quick wins landed 2026-06-11:** native PDF
> text (PdfPig, B2) + config/template cleanup (E2). The 2026-06-07 matrix below remains the
> prior baseline; the 06-11 docs supersede it for *current* state.

> Re-audited **2026-06-07** by tracing the DI composition roots (Web.UI / Athena
> Worker / Orion Worker) and every pipeline stage. This is **static-wiring reality**;
> integration/system/E2E suites were **not** run, so end-to-end behaviour is still
> unverified. Full evidence + file:line citations:
> `docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md`. The 2025-01-12
> PRD and the Mission docs remain the **intended targets** — the classification below
> describes the *distance to* those targets, it does not retire them.

**Done (real + wired + meets intent):**
- Export pipeline — `AdaptiveExporter` (SIRO XML / Excel / PDF) wired in Athena Worker + Web.UI.
- Database / EF Core, File storage, Field extractors (PDF/DOCX/XML/TXT — XML extractor is real CNBV/PRP1 extraction, hardened + 16 tests 2026-06-07; the prior "dummy placeholder" label was a stale DI comment, not the code).
- Web UI auth — ASP.NET Core Identity (cookie-based), real.
- Rx.NET event architecture (`EventPublisher`) — wired and accurate to the docs.
- Health checks (`/health`, `/health/live`) — real Athena/Orion services.
- Architecture enforcement tests (19/19), Domain (337/337), Application (157/157) — green (prior session).

**Done (continued):**
- **OCR** — **Tesseract** (`TesseractOcrExecutor`, real Tesseract.NET) is the engine of
  record **by deliberate decision**. After Python.NET removal and the finding that CSnakes
  interop was hard to operationalize, the team chose the direct-C# path and **intentionally
  retained** the Python/CSnakes VLM scaffolding *dormant* (GOT-OCR2 registration + `AddPrismaPythonEnvironment()`
  commented; `PrismaOcrService` unregistered) so a GitHub/VLM model can be refactored in
  *if/when* needed. **This is optionality-by-design — do NOT treat the dormant Python as a
  gap or delete it.** See ADR-001 (`docs/architecture/python/adr-001-csnakes-vs-pythonnet.md`).
  (The "DocTR production-ready" mission summary is a research experiment, not the shipped engine.)

**Partial (real but limited / unwired / degraded end-to-end):**
- **Fusion** — `FusionExpedienteService` is real, and (✅ **2026-06-07**) the orchestrator now
  threads OCR output into it: Stage 2 text → `IFieldExtractor<TxtSource>` → `Expediente` →
  `FuseAsync` (PDF source). Remaining: XML/DOCX sources are still null in the worker path
  (single-source OCR fusion).
- **Adaptive/Robust extractors** — Adaptive **DOCX** (orchestrator + all 5 strategies) and
  Adaptive **Export** are implemented and DI-wired. Adaptive **TXT** is implemented and (✅
  **2026-06-07**) its 3 previously-skipped robustness edge cases are now fixed + unskipped
  (CNBV-vs-SAT authority priority, Expediente pattern `B/CDEF-1234-567890-ABC`, SAT detection
  conflict) — `Tests.Infrastructure.Extraction.Txt` 35/35, 0 skipped. `XmlFieldExtractor` is
  also real CNBV/PRP1 XML extraction (hardened + 16 tests 2026-06-07; its former "dummy
  placeholder" tag was just a stale DI comment). So the field extractors are now in good shape.
- **Quality** — real (`PolynomialImageQualityAnalyzer` in Worker, `EmguCvImageQualityAnalyzer`
  in UI), but trained filter-selection models are still stub coefficients.
- **Classification** — real path; deeper semantic field extraction is TODO.
- **Auth abstraction** (`IIdentityProvider`/`ITokenService`/`IUserContextAccessor`) — real
  `EfCoreIdentityAdapter` with JWT exists but is **registered nowhere** (UI uses Identity directly).
- **Readiness probes** — present but stubbed (`// TODO: orchestrator.IsStarted`).

**Planned (stub / placeholder / missing):**
- **Orion headless ingestion chain** — *not* "download doesn't work." Document download against the
  SIARA simulator **works and was demoed** via the browser-automation/scraping path
  (`SiaraNavigationTarget` + `DocumentIngestionService`, wired in the UI; the simulator
  `tools/Siara.Simulator` models SIARA's links-on-a-page shape; no SIARA API exists, so web-scraping is
  the deliberate approach). The gap is **headless integration**: the Orion worker still wires
  `StubDocumentDownloader` + `StubExxerHub`, the working scraper isn't yet adapted into the
  `IDocumentDownloader` port, and `IngestionOrchestrator.StartAsync()` (the poll/watcher) is a placeholder.
  **Agreed direction:** split into **3 Ember-coordinated processes — Downloader / Extractor / Reconciliator**
  (`IndFusion.Ember` "Three Actors", ADR-009). Highest-leverage remaining integration work. Details: GAP-MATRIX §9.6.
- Worker `/dashboard` metrics — `Orion/AthenaDashboardService` return zeros (UI Dashboard uses the working `IProcessingMetricsService`).
- PersonIdentityResolver DB persistence; PDF text extraction (returns empty pending iText/PdfSharp); CSnakes Python ML runtime interop. *(2026-06-07: the "3 skipped TXT extractor edge cases" were fixed, and `XmlFieldExtractor` was confirmed real + hardened — both removed from this list.)*
- Sentinel monitoring service — **not yet traced; status unknown.**

> Roadmap toward production: `docs/planning/path-to-production-2026-06.md`.

### Current build status (2026-06-07)

✅ **`dotnet build` of the main solution succeeds** (0 errors, 0 warnings) and
`dotnet list package --vulnerable` reports **no vulnerabilities**. The NU1903 CVE
was fixed by enabling `CentralPackageTransitivePinningEnabled` and pinning
`System.Security.Cryptography.Xml 10.0.8`; OpenTelemetry was bumped to 1.15.x to
clear its Moderate CVEs. Packages were updated to latest stable (see
`Directory.Packages.props`).

#### Live verification pass (2026-06-07b) — suites actually run + UI launched

A full **dual-track** verification was run on a Docker- + Playwright-capable machine (not just
static tracing). Evidence + per-suite numbers: GAP-MATRIX §9
(`docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md`).

- **Project count corrected:** the solution is **~70 projects (33 production + 37 test)**, **not
  the "195 / 200+" cited in this file and `STAKEHOLDER_PRESENTATION_READINESS.md`.** The repo-wide
  217 `.csproj` count is inflated by `scripts/*_backups/**` (old `ExxerAI.*` clone snapshots),
  `doc_quality/.../sample_repo`, and `Samples/GotOcr2Sample`. The inflated figure predates the
  housekeeping that removed the duplicated `ExxerAI` tree. **Don't repeat 195/200.**
- **Adaptive-DOCX tests were dark — now fixed:** `Tests.Infrastructure.Extraction.Adaptive` (126
  tests, all 5 DOCX strategies) was **excluded from the `.sln` and failed `CS0234`** (its
  `ProjectReference` to `Testing.*` used 7 `..\` instead of 3). Fixed the depth + re-added to the
  solution → **126/126 green**, solution still **0/0**. So "adaptive DOCX tested" is now *actually* true.
- **Formerly-"UNVERIFIED" tiers now green:** Docker SQL (`System.Storage` 39/39,
  `Infrastructure.Database` 110/110 via Testcontainers), Playwright/UI (`Tests.UI` 21/21),
  `BrowserAutomation.E2E` 18/18, `Tests.EndToEnd` 29/29. Real OCR `Extraction.Teseract` 154/154.
  ~**1,670+ tests** executed green (the "600+ tests" claim was an undercount).
- **Non-green (expected):** `System.Ocr.Pipeline` is **flaky** (one transient live-OCR fail then
  25/25 — variance); `Extraction.GotOcr2` 16 tests **skipped** (VLM dormant by design);
  `Extraction.Python` 0 (dormant). These are not defects.
- **Web.UI launched for real:** `GET /` → HTTP 200 (MudBlazor renders), `/health` → "Healthy"; boots
  gracefully even when DB seed fails (caught). **Demo caveats:** `appsettings.json` hardcodes
  `Server=DESKTOP-FB2ES22\SQL2022` (won't start off-box without reconfig); `Counter.razor` /
  `Weather.razor` template leftovers should be removed before presenting.
- **HMI** (`Prisma.HMI.Tests` 13/13) is a notification-queue **prototype whose types live inside the
  test project** — not a wired service; track as experimental, not in the services table.

**Held back deliberately (need a dedicated, decision-gated upgrade):**
- **MudBlazor** at `8.11` — `9.x` is a major UI migration (`ChartSeries<T>`,
  `ActivatorContent`, runtime/visual changes); needs visual testing.
- **SixLabors.ImageSharp** at `3.1.12` — `4.x` requires a **paid commercial
  license** (enforced at build). A business/legal decision.
- **Emgu.CV** at `4.12.0.5764` — `4.13` changes the native `CvInvoke.CLAHE`
  signature; `Contrib`/`ubuntu-x64` have no matching upstream release.
- **Testcontainers** at `4.9.0` — `4.12` obsoletes the parameterless builder
  ctors (CS0618-as-error).
- **BouncyCastle.Cryptography** on `2.7.0-beta` — the only stable (`2.6.2`) is
  older than the pinned beta.

### Testing stack (xunit.v3 + Microsoft.Testing.Platform)

This combination is **version-coupled and fragile** — get it wrong and tests fail
at *run* time with `System.MissingMethodException` (e.g.
`IOutputDevice.DisplayAsync` not found), because xunit's MTP integration is
compiled against a specific MTP ABI. The current **working** set (verified: build
0/0, `dotnet test` runs, Tests.Domain 337/337):

- Test projects reference **`xunit.v3.mtp-v2`** `3.2.2` (NOT the default
  `xunit.v3`, which is the `mtp-v1` variant). This is the switch that selects MTP v2.
- `Microsoft.Testing.Platform` (+ `.MSBuild` + `Extensions.HangDump/TrxReport/VSTestBridge`) at **`2.1.0`**;
  `Microsoft.Testing.Extensions.CodeCoverage` `18.5.2`; `xunit.analyzers` `1.27.0`;
  `Meziantou.Extensions.Logging.Xunit.v3` `2.0.1`.
- **`global.json`** opts `dotnet test` into MTP on the .NET 10 SDK:
  `{ "test": { "runner": "Microsoft.Testing.Platform" } }` — without it,
  `dotnet test` errors (VSTest target unsupported).
- `CentralPackageTransitivePinningEnabled` is on, so an MTP `PackageVersion` pin
  also forces xunit's transitive MTP — every `Microsoft.Testing.*` version must
  align to the same MTP minor (here 2.1.0). Bump the whole set together.
- CodeCoverage 18.5.2 is compiled against MTP 2.1.0, so MTP must be ≥ 2.1.0 (this
  is why 2.0.2 — what xunit's mtp-v2 declares — wasn't enough; CS1705).

### Docker integration tests (Testcontainers) — sharing & isolation (2026-06-07)

SQL Server integration tests (`Tests.System.Storage`) use a **single shared container
for the whole assembly** via the xUnit v3 **assembly fixture**
(`[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]`). To run write-heavy
classes **in parallel without colliding**, each test class provisions its own database on
that shared container via `SqlServerContainerFixture.CreateIsolatedDatabaseAsync(name)` (a
cheap `CREATE DATABASE`, dropped on disposal). One class (`DatabaseInfrastructureSmokeTests`)
keeps the canonical `PrismaTestDb` because it deliberately exercises the shared-DB ops. The
old serial `[CollectionDefinition(DisableParallelization = true)]` gate was removed. Pattern
& rationale: `docs/qa/test-plans/docker-test-isolation-2026-06.md`. **Don't** reintroduce a
shared mutable database across parallel classes — give each writer its own DB instead. Ollama
tests share one read-only container; their first-run slowness is the model download, not sharing.

### Repo hygiene

The repo was reorganized on 2026-06-07 (branch `chore/repo-reorg`): root clutter
removed, docs/scripts/tools organized, and the superseded SignalR projects removed
(now the `IndFusion.Ember` package). See
`docs/development/archive/root-cleanup-2026-06.md` for the full record and the
list of deferred follow-ups (Python tree dedupe, stale DB scripts, `Veriqan`
clone).

# Gap Matrix — Dual Ground-Truth Reconciliation (2026-06-07)

**Author:** reconciliation pass (per `docs/development/sessions/HANDOFF-2026-06-dual-ground-truth.md`)
**Branch:** `Kt2` · build green 0/0 · 0 vulnerabilities
**Status of this document:** GT1 (current-code) established by reading the DI composition
roots and tracing each pipeline stage. GT2 (intended) anchored to the PRD/epics/ADRs.
Integration/E2E test status is **still unverified** (see §6) — this matrix reflects
*static wiring reality*, not a live end-to-end run.

> **How to read this.** Two ground truths are kept side by side and never collapsed:
> **GT2 = the documented target** (what the system should become) and **GT1 = what the
> code actually does today**. "Status" classifies the *current* state against the target:
> **Done** (real + wired + meets intent), **Partial** (real but limited/unwired/degraded),
> **Planned** (stub/placeholder/missing). A Planned/Partial row does **not** retire the
> target — it records the distance left to travel.

---

## 0. Authoritative "what's wired" sources (read these to reproduce GT1)

| Composition root | File | Role |
|---|---|---|
| Web UI | `07 UI/UI/ExxerCube.Prisma.Web.UI/Program.cs` | Blazor app + demo/interactive pipeline |
| Athena Worker | `04 Services/Athena/Prisma.Athena.Worker/Program.cs` | **The real 5-stage processing host** |
| Orion Worker | `04 Services/Orion/Prisma.Orion.Worker/Program.cs` | Ingestion host |
| Extraction DI | `02 Infrastructure/Infrastructure.Extraction/DependencyInjection/ServiceCollectionExtensions.cs` | OCR/field extractors |
| Imaging DI | `02 Infrastructure/Infrastructure.Imaging/DependencyInjection.cs` | Quality analyzer + filters |

**Key structural fact:** the 5-stage `ProcessingOrchestrator`
(`04 Services/Athena/Prisma.Athena.Processing/ProcessingOrchestrator.cs:19`) takes every
stage as an **optional/nullable** dependency and *skips with a warning* if a stage is
unregistered (e.g. `:385` "Stage 1 skipped", `:463` "Stage 2 skipped"). So "is the
pipeline real?" is answered entirely by *what each host registers*, not by the
orchestrator. **Athena Worker registers real implementations for all five stages.**

---

## 1. The 5-stage pipeline (Quality → OCR → Fusion → Classification → Export)

| Stage | GT2 — intended (source) | GT1 — actual (code) | Gap | Status | Priority |
|---|---|---|---|---|---|
| **1. Quality Analysis** | Reject low-quality scans before OCR; analytical filter selection from the filtering study | Athena Worker → `PolynomialImageQualityAnalyzer` (`Athena.Worker/Program.cs:21`); Web.UI → `EmguCvImageQualityAnalyzer` (`Infrastructure.Imaging/DependencyInjection.cs:125`). Both **real**. `StubImageQualityAnalyzer` exists but is **never registered**. Filter-selection coefficients are **TODO stub models** (`PolynomialFilterSelectionStrategy.cs:54,152,289`). | Real analyzer works; the *trained* polynomial models are placeholders pending the filtering-study dataset. Two hosts use two different analyzers. | **Partial** | Med |
| **2. OCR** | Spanish-legal OCR. Original intent (ADR-001) was **CSnakes Python interop**; a VLM solution (GOT-OCR2; DocTR in `missions/PRODUCTION_READY_SUMMARY.md`) was explored. | **Tesseract is the wired engine *by deliberate decision*** — `TesseractOcrExecutor` (real Tesseract.NET NuGet, PDF rasterize via PDFtoImage), registered in Athena Worker (`:22`) and Web.UI (via `AddExtractionServices`). After Python.NET removal and the finding that CSnakes interop was hard to operationalize, the team **chose the direct-C# path** and **intentionally retained** the Python/CSnakes scaffolding dormant for future GitHub/VLM optionality: GOT-OCR2 registration is commented (`Infrastructure.Extraction/.../ServiceCollectionExtensions.cs:40-42`), `AddPrismaPythonEnvironment()` is commented (`Web.UI/Program.cs:190`), and `PrismaOcrService` (CSnakes placeholder, `Infrastructure/Python/PrismaOcrService.cs:48`) is unregistered. See ADR-001 (`docs/architecture/python/adr-001-csnakes-vs-pythonnet.md`) and `docs/development/guides/python-todo-analysis-report.md`. | OCR **works** via the chosen C# engine. The dormant Python path is **optionality-by-design, not a defect** — refactor in *if/when* GitHub/VLM models are needed. The "DocTR production-ready" mission summary is a **research experiment**, not the shipped engine. | **Done (by design)**; Python VLM = **Planned-optional (intentional)** | Low (re-enable only if VLM needed) |
| **3. Fusion / Reconciliation** | Fuse XML+PDF+DOCX extraction metadata into one reconciled expediente; detect conflicts | `FusionExpedienteService` registered + real (`Athena.Worker/Program.cs:23`). **✅ FIXED 2026-06**: the orchestrator now extracts fields from the Stage 2 OCR text (`IFieldExtractor<TxtSource>` → `AdaptiveTxtFieldExtractor`), maps them to an `Expediente`, and passes that + real metadata as the PDF source to `FuseAsync` (both legacy + ROP paths). Worker wires the extractor (`Athena.Worker/Program.cs`); verified by `Stage3_WithTxtFieldExtractor_FeedsOcrDerivedExpedienteToFusion`. Previously fed `null,null,null` + empty metadata. | Service real + now fed real OCR-derived data end-to-end. Remaining: XML/DOCX sources still null in the worker path (single-source OCR); DRY follow-up to share the ExtractedFields→Expediente mapper with the UI. | **Done (OCR→PDF source)** / Partial (multi-source) | Med |
| **4. Classification** | Classify requirement type (CNBV/SIARA), confidence threshold → manual-review flag | `FileClassifierService` registered + real (`Athena.Worker/Program.cs:24`); orchestrator threshold logic present (`:615`). Semantic detail extraction (accounts/amounts/expediente refs) is **TODO** (`SemanticAnalyzerService.cs:143,168,193,218,243`). | Classification path real; deeper semantic field extraction deferred. | **Partial** | Med |
| **5. Export** | SIRO XML, Excel, PDF signing; adaptive templates | `AdaptiveExporter` registered + real (`Athena.Worker/Program.cs:25`); Web.UI adds full export + adaptive export + legacy ExportService. Unsupported template/transform paths guarded with `NotSupportedException` (`AdaptiveExporter.cs:325`, `TemplateFieldMapper.cs:384`). Template store has SQLite/in-memory `// TODO: Switch to ToJson() in production` (`TemplateDbContext.cs:57,67`). | Export real and the most complete stage. Template persistence is demo-grade (SQLite/in-memory) pending SQL Server JSON. | **Partial → Done** (functionally closest) | Low |

**Pipeline verdict:** The orchestrator and all five stage *services* exist and are wired
in Athena Worker. The OCR "engine mismatch" is **not a gap** — Tesseract-C# is the chosen
engine and the Python/VLM path is retained dormant on purpose. The one true wiring-level
gap that breaks end-to-end value is **(b) OCR results are not passed into Fusion**
(`null,null,null`); combined with the stubbed ingestion (§2 Orion), that is where the
highest leverage lies.

---

## 2. Services (Orion / Athena / Sentinel)

| Capability | GT2 — intended | GT1 — actual | Status | Priority |
|---|---|---|---|---|
| **Orion — document download (SIARA)** | Download requerimientos from the SIARA/CNBV portal | `IDocumentDownloader` → **`StubDocumentDownloader`** only; returns `Array.Empty<byte>()` and logs a warning (`Orion.Ingestion/StubDocumentDownloader.cs:28`). Wired in `Orion.Worker/Program.cs:17`. A `tools/Siara.Simulator` portal sim exists. | **Planned** (real downloader missing — core ingestion gap) | **High** |
| **Orion — ingestion journal** | Idempotent, resumable ingestion log | `FileIngestionJournal` real, file-backed (`Orion.Worker/Program.cs:11-16`). | **Done** | — |
| **Orion — real-time hub** | Broadcast `DocumentDownloadedEvent` to UI | `IExxerHub<...>` → **`StubExxerHub`** (logs, broadcasts nothing) (`Orion.Worker/Program.cs:20`). Real hub now lives in the `IndFusion.Ember` package. | **Planned/Partial** | Med |
| **Athena — processing host** | Run the 5-stage pipeline on ingested docs | Real: registers all stages + `AthenaWorkerService` hosted service (`Athena.Worker/Program.cs`). | **Done** (host) / see §1 for stage gaps | — |
| **Athena/Orion — health checks** | `/health`, `/health/live`, `/health/ready` | **Real** `AthenaHealthCheckService` / `OrionHealthCheckService` wired (`:52` / `:34`). Liveness real; readiness has `// TODO: Check orchestrator.IsStarted` (`*HealthCheckService.cs:55`). | **Partial** | Low |
| **Athena/Orion — `/dashboard`** | Live processing metrics | **`AthenaDashboardService` / `OrionDashboardService` are stubs returning zeros** (`*DashboardService.cs:46` "TODO: Get actual metrics"). | **Planned** | Low |
| **Sentinel — monitoring** | System monitoring service (listed in CLAUDE.md services table) | `Prisma.Sentinel.Monitor` is a **real, tested library (16/16 tests pass)** but has **no `Sentinel.Worker`/host `Program.cs`** — only Orion/Athena/Web.UI are hosted. So it is built but **not deployed as a running service.** | **Partial** (real lib, unhosted) | Med |

---

## 3. Authentication / Identity

| Capability | GT2 — intended | GT1 — actual | Status | Priority |
|---|---|---|---|---|
| **Web UI auth** | Authenticated users | **Real**: ASP.NET Core Identity, cookie-based — `AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager()` (`Web.UI/Program.cs:237-240`). | **Done** (for UI) | — |
| **`IIdentityProvider` / `ITokenService` / `IUserContextAccessor` abstraction** | Hexagonal port so auth provider is swappable; JWT issuance | **Real implementation exists** — `EfCoreIdentityAdapter<TUser>` (`04 Services/Auth/Prisma.Auth.Infrastructure/EfCoreIdentityAdapter.cs:11`) with full HS256 JWT create/validate (`:75-138`); `InMemoryIdentityProvider` is the dev stub. **NEITHER is registered in any production root** — the abstraction is unwired; the UI talks to Identity directly. Allowlisted in `Tests.Architecture` as v1.1. | **Partial** (real-but-unwired) | Med |
| **JWT-secured API** | Token-based API auth | JWT code exists in `EfCoreIdentityAdapter` but is **not wired to any request pipeline** (cookies only). | **Planned** | Med |

---

## 4. The "9 allowlisted interfaces" (from `docs/qa/reports/test-debt-2026-06.md`) — resolved

The architecture test allowlisted these as "no implementation in scanned assemblies."
Verified reality:

| Interface | Verdict | Evidence |
|---|---|---|
| `IHealthCheckService` | **REAL + wired** (Orion/Athena, not scanned) | `Athena.Worker/Program.cs:52`, `Orion.Worker/Program.cs:34` |
| `IDashboardService` | **REAL but STUB behaviour** (returns zeros) | `*DashboardService.cs:46` |
| `IDocumentDownloader` | **STUB-only** (real gap) | `StubDocumentDownloader.cs:28` |
| `IIngestionJournal` | **REAL + wired** | `FileIngestionJournal`, `Orion.Worker/Program.cs:11` |
| `IIdentityProvider` | **REAL but unwired** | `EfCoreIdentityAdapter.cs:60` |
| `ITokenService` | **REAL but unwired** | `EfCoreIdentityAdapter.cs:75` |
| `IUserContextAccessor` | **REAL but unwired** | `EfCoreIdentityAdapter.cs:41` |
| `IEventHandler<T>` | **Legacy / aspirational — correctly allowlisted** | consumed by `InMemoryEventBus` (not production); Rx.NET `EventPublisher` is the real mechanism |
| `IFieldMatchingService` | **REAL + wired** | `Web.UI/Program.cs:285` |

**Takeaway:** of the 9, only **`IDocumentDownloader`** is a genuine missing implementation;
three (`IIdentityProvider`/`ITokenService`/`IUserContextAccessor`) are *real but unwired*;
one (`IDashboardService`) is *real but stubbed*; the rest are real+wired or correctly-legacy.

**✅ FIXED 2026-06**: `Tests.Architecture` was hardened — implementation detection is now
**name-based** (immune to `Assembly.LoadFrom` type-identity mismatch) across Infrastructure +
Orion/Athena/Auth/Application (the service projects are now referenced + typeof-anchored). The
allowlist shrank from 12 → 4 entries: only `IEventHandler<T>` (legacy, genuinely unimplemented)
+ 3 domain-only marker interfaces remain. The 8 former v1.1-deferred entries are now verified
directly. Note: "has an implementation" includes stubs (e.g. `StubDocumentDownloader`) — the
stub-vs-real quality distinction stays tracked in this matrix, not the arch test. 19/19 green.

---

## 5. Technical-debt markers (production C# only)

Counts confirmed roughly in line with the handoff (≈27 TODO, ≈18 placeholder, **0 `NotImplementedException`**, 2 intentional `NotSupportedException` guards). Concentrations:

| Cluster | Files | Nature | Classification |
|---|---|---|---|
| CSnakes Python OCR placeholder | `Infrastructure/Python/PrismaOcrService.cs:41,48,58-60` | hardcoded "Placeholder OCR" result | **Intentional dormant optionality** (unwired by design; Tesseract is the chosen path) — keep for future VLM, do NOT treat as cleanup |
| Trained ML filter models | `Infrastructure.Imaging/Strategies/PolynomialFilterSelectionStrategy.cs:54,120,152,289,292`; `FeatureNormalizer.cs:11,75,83` | stub coefficients pending filtering-study data | Planned (v1.1) |
| Semantic field extraction | `Infrastructure.Classification/SemanticAnalyzerService.cs:143,168,193,218,243` | deeper field extraction deferred | Planned (v1.1) |
| Dashboard/SLA metrics | `*DashboardService.cs:46`; `SLAMetricsCollector.cs:250` (`return 0`) | metrics not wired to telemetry | Planned |
| PDF text extraction | `PdfMetadataExtractor.cs:209-229,211` (returns `string.Empty`) | needs iText/PdfSharp | Planned |
| PersonIdentityResolver DB | `PersonIdentityResolverService.cs:188` | DB persistence deferred (v1.1 per CLAUDE.md) | Planned |
| Template store persistence | `TemplateDbContext.cs:57,67` | SQLite/in-memory → SQL Server JSON | Cleanup |
| Orchestrator `IsStarted` | `*HealthCheckService.cs:55` | readiness probe stub | Cleanup |
| ~~Dummy XML field extractor~~ | `Infrastructure.Extraction/.../ServiceCollectionExtensions.cs:33` | The "temporary placeholder" was a **stale DI comment only** — the extractor is real CNBV/PRP1 XML extraction. | **✅ Resolved 2026-06-07** (comment dropped; null-guards + 16 tests added; Extraction 86/86) |

**Stubs (4, confirmed):** `StubDocumentDownloader` (wired, Orion), `StubExxerHub` (wired,
Orion), `StubEventPublisher` (Athena — **not** in production root; `EventPublisher` is),
`StubImageQualityAnalyzer` (**never** registered).

---

## 6. Test / verification status

| Suite | Status |
|---|---|
| Tests.Domain | ✅ 337/337 (prior session) |
| Tests.Application | ✅ 157/157 (prior session) |
| Tests.Architecture | ✅ 19/19 (prior session, with documented allowlist §4) |
| **Orchestration / Service / System suites (RAN 2026-06-07)** | ✅ Orchestration 6/6, Athena.Processing 34/34, Orion.Ingestion 8/8, **Athena.Worker 9/9 + Orion.Worker 9/9 (hosts boot via WebApplicationFactory)**, Sentinel 16/16, System.XmlExtraction 9/9, System.Storage 39/39, System.Export.Adaptive 15/15. Full report: `docs/qa/reports/test-status-verification-2026-06-07.md`. |
| **System OCR pipeline (RAN)** | ⚠️ **FLAKY 22–23/25** — real Tesseract OCR runs (confirms OCR works), but `PdfFieldExtraction_NullPdfSource_ReturnsFailure` is a **real NRE-not-Result bug** and `..._ExtractSingleField_Works` is flaky (asserts on live OCR output). See §8a. |
| **SQL DB integration (`Tests.System.Storage`, RAN 2026-06-07 with Docker)** | ✅ **31/31 green** on a **shared assembly-fixture container + per-class isolated DBs** (parallel writers; replaced serial `DisableParallelization`). See `docs/qa/test-plans/docker-test-isolation-2026-06.md`. |
| Playwright / full-host / Ollama-model E2E (Tests.EndToEnd, Tests.UI, BrowserAutomation.E2E, GotOcr2, Ollama smoke) | ⚠️ **Still UNVERIFIED** — Playwright not exercised; Ollama needs model download. |

---

## 8a. Findings surfaced by running the system OCR pipeline (2026-06-07)

Running `Tests.System.Ocr.Pipeline` (which executes **real Tesseract OCR**, confirming the
chosen C# OCR engine genuinely works) surfaced two issues not visible from unit suites:

| Finding | File/test | Type | Action |
|---|---|---|---|
| Null PDF source → internal NRE instead of `Result.WithFailure` | `PdfFieldExtraction_NullPdfSource_ReturnsFailure` (error: "Object reference not set…") | **Real bug** — violates "validate, never throw" | Add up-front null guard returning `Result.WithFailure`; small, verified fix |
| Field-extraction assertion depends on live OCR confidence (varies run-to-run) | `PdfFieldExtraction_ExtractSingleField_Works` ("Field 'Expediente' not found") | **Flaky test** | Assert against a fixed OCR transcript fixture, or relax to confidence-tolerant matching |

> The pass count varied (3 failures then 2 across two runs) — proof of flakiness, not a
> stable red. Tracked here so it isn't mistaken for "all green."

## 7. Documentation ↔ code gaps (docs that over-claim or lag)

| Doc | Claim | Reality | Action |
|---|---|---|---|
| `missions/PRODUCTION_READY_SUMMARY.md` | "DocTR production-ready, 100% field accuracy, deploy now" | DocTR not in product; Tesseract-C# is the chosen wired engine; Python VLM retained dormant by design. Describes a Python experiment. | Mark as **research result / future-optionality**, not shipped state |
| `CLAUDE.md` "Release Status" | Production-ready list, "unverified since dormancy" | Mixed Done/Partial/Planned per this matrix | **Rewrite** to Done/Partial/Planned (keep targets) |
| `prd.md` | v1.0 "Draft" 2025-01-12 | Predates much of what was built | Confirm scope; note divergence; don't delete intent |
| `CLAUDE.md` event-architecture (Rx.NET) | EventPublisher is the real impl | ✅ **Confirmed accurate** (`Web.UI/Program.cs:185`, Athena `:17`) | none |

---

## 8. Top gaps by leverage (feeds the roadmap)

1. ~~**Orchestrator feeds Fusion empty inputs** (`null,null,null`)~~ — **✅ FIXED 2026-06**: OCR
   text → field extraction → Expediente now feeds fusion (see §1 stage 3).
2. **`StubDocumentDownloader`** — no real SIARA ingestion; nothing real enters the pipeline. — **High**
3. **Establish integration/E2E test truth** — Docker/Playwright suites still pending (see §6). — **High**
4. Wire the auth abstraction (or document it as deliberately deferred); dashboard metrics;
   trained filter models; PDF text extraction; PersonIdentityResolver DB. — **Med/Low**
5. ~~Harden the architecture test to scan Orion/Athena/Auth~~ — **✅ FIXED 2026-06** (§4).
6. ~~PDF→image conversion not behind a port (untestable extractor)~~ — **✅ FIXED 2026-06**
   (`IPdfToImageConverter`; §8a).

> **Not a gap (recorded so it isn't "fixed" by mistake):** OCR engine choice. Tesseract-C#
> is the deliberate engine of record; the Python/CSnakes VLM path is intentionally retained
> dormant for future GitHub/VLM optionality (ADR-001 + python-todo-analysis-report). Re-enable
> only *if/when* a VLM is actually needed — do not delete it and do not treat it as debt.

> Destination unchanged: a working CNBV/SIARA compliance pipeline. This matrix marks the
> route — it does **not** declare arrival.

# Path to Production — Roadmap (2026-06-07)

Companion to `gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md`. This is the
sequenced route from current static-wiring reality to a working CNBV/SIARA compliance
product. It does **not** assert the product is finished; it orders the remaining work by
leverage. Each item links to the gap-matrix row that justifies it.

## Guiding principle
The destination (the PRD/epics target: download → OCR → fuse → classify → export
compliance artifacts for Spanish legal/banking docs) is unchanged. These steps close the
distance; they don't redefine the goal.

## P0 — Establish true status (do first; everything else is static analysis until then)
1. **Run the integration/system/E2E suites.** *(Gap §6.)*
   - ✅ **Partially done 2026-06-07** (`docs/qa/reports/test-status-verification-2026-06-07.md`):
     orchestration, both worker hosts (boot via WebApplicationFactory), Sentinel, and the
     non-Docker system suites are **green (~145 tests)**; the System OCR pipeline runs real
     Tesseract and is **flaky 22–23/25** with one real null-guard bug.
   - ⏳ **Remaining (needs Docker/Playwright env):** `Tests.EndToEnd`,
     `Tests.Infrastructure.Database` (Testcontainers SQL), `Tests.UI` + `BrowserAutomation.E2E`
     (Playwright), `GotOcr2` (Python env). Run these where Docker + Playwright are available.
1b. **Fix the OCR null-PDF NRE** (`PdfFieldExtraction_NullPdfSource_ReturnsFailure`) — add an
   up-front null guard returning `Result.WithFailure`; de-flake `..._ExtractSingleField_Works`
   by asserting against a fixed transcript. Small, verified. *(Gap §8a.)*

## P1 — Make one document flow end-to-end for real
> **OCR engine is already decided — not a P1 item.** Tesseract-C# is the engine of record
> by deliberate decision; the Python/CSnakes VLM path is intentionally retained dormant
> (ADR-001). Re-enabling it (`AddPrismaPythonEnvironment()` `Web.UI/Program.cs:190` +
> GOT-OCR2 registration) is a *future-optionality* task, to do only *if/when* a GitHub/VLM
> model is actually required — do not delete the scaffolding.

2. ~~**Fix OCR→Fusion data flow.**~~ **✅ DONE 2026-06** — the orchestrator now extracts fields
   from Stage 2 OCR text and feeds the resulting Expediente + metadata to fusion (both legacy
   and ROP paths); Worker wires `IFieldExtractor<TxtSource>`. *(Gap §1 stage 3.)*
3. **Replace `StubDocumentDownloader` with a real SIARA downloader.** Nothing real enters
   the pipeline until this exists. The `tools/Siara.Simulator` portal sim can back
   integration tests. *(Gap §2 Orion.)*

## P2 — Operability & security
5. **Auth abstraction decision.** Either wire `EfCoreIdentityAdapter` as
   `IIdentityProvider`/`ITokenService`/`IUserContextAccessor` (and add JWT to the API
   pipeline), or document the cookie-only UI auth as the deliberate v1 choice and mark the
   abstraction explicitly deferred. *(Gap §3.)*
6. **Real `/dashboard` metrics + readiness probes.** Wire `Orion/AthenaDashboardService` to
   real counters; implement orchestrator `IsStarted` for readiness. *(Gap §2.)*
7. **Trace Sentinel.** Status currently unknown — audit and classify. *(Gap §2.)*

## P3 — Depth & cleanup
8. Trained polynomial filter-selection models (replace stub coefficients with filtering-study output).
9. Semantic field extraction (accounts/amounts/expediente refs) in `SemanticAnalyzerService`.
10. PDF text extraction library (iText/PdfSharp) — currently returns empty.
11. PersonIdentityResolver DB persistence; template store SQLite/in-memory → SQL Server JSON.
12. ~~**Harden the architecture test**~~ **✅ DONE 2026-06** — name-based implementation
    detection across Orion/Athena/Auth/Application; allowlist trimmed 12→4 (only the legacy
    `IEventHandler<T>` + 3 domain-only markers remain). *(Gap §4.)*
14. ~~**Extract `IPdfToImageConverter` port**~~ **✅ DONE 2026-06** — PDF→image rasterization is
    now behind an injectable port; `PdfOcrFieldExtractor` is unit-testable (10 broken tests fixed). *(Gap §8a.)*
15. **DEFERRED — justify-or-improve the analytical-filter ≥10% OCR threshold (needs real data).**
    `AnalyticalFilterE2ETests`' hard ≥10%-improvement gate was relaxed to a no-regression gate to
    de-flake (synthetic-data OCR is non-deterministic; ~1/8 flake). DEFERRED goal — *preferably
    improve* the filter to reliably hit ≥10% (study targets Q2=78.1%, Q1=24.9%; high likelihood of
    further gains), or justify the threshold; then restore the hard gate. **Revisit with REAL
    (non-synthetic) document data** — current results came from synthetic fixtures. *(Gap §8a.)*
16. **DRY follow-up** — share the `ExtractedFields → Expediente` mapper between the orchestrator
    (`BuildPdfExpedienteFromOcrAsync`) and the UI `PdfProcessingService`. *(Gap §1 stage 3.)*

## P4 — Held upgrades (decision-gated, see CLAUDE.md)
13. MudBlazor 8→9 (UI migration + visual testing); SixLabors.ImageSharp 3→4 (paid license
    business decision); Emgu.CV / Testcontainers / BouncyCastle holds.

## Definition of "production-ready" (none asserted yet)
A document downloaded from SIARA flows through all five stages with real data, produces a
valid compliance export, with integration/E2E suites green and observability live. Today:
Tesseract OCR + real export exist, but ingestion is stubbed and OCR→Fusion is not threaded —
so end-to-end is **not** yet demonstrable.

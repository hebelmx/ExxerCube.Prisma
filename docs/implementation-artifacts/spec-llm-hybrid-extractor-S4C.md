# Intended Solution — S4-C: Un-dark the hybrid extractor into the Athena pipeline

Anti-drift reference for the (future, owner-gated) S4-C build. Parent specs:
`spec-llm-hybrid-extractor.md` (S1–S3b contracts), `spec-llm-hybrid-extractor-S4A.md` (eval harness),
`spec-llm-hybrid-extractor-S4B.md` (extractor extension). Governing ADR: `ADR-024-llm-hybrid-graduation-criteria.md`
— **S4-C is "Gate B" and this spec does NOT authorize the flip.** Written 2026-07-04 (orchestrator session,
branch `Liv`) as the design deliverable; **no production code is to be written from it without explicit owner
sign-off** (see §7).

## Status: DESIGN ONLY — BLOCKED on Gate-B preconditions
Today the hybrid path (`IHybridExtractionService`) is reachable **only** from the Web.UI `/hybrid-extraction`
demo page. The Athena worker never calls it. S4-C is the epic that would let the **pipeline** use it. It is
blocked (by design) on the ADR-024 D6 corpus + D7 honesty preconditions + owner sign-off — see §7.

## Why (confirmed from code)
- Worker composition root `04 Services/Athena/Prisma.Athena.Worker/Program.cs` builds DI **manually**. It
  registers the deterministic `IFieldExtractor<TxtSource>` → `AdaptiveTxtFieldExtractor` at `Program.cs:174`
  and never calls `AddExtractionServices` / `AddClassificationServices` / `AddPrismaInfrastructure`, never
  binds `LlmProvidersOptions`, never registers `IHybridExtractionService`.
- Live extraction call site: `AthenaWorkerService` → `ExtractionPipelineService.cs:164` →
  `ExtractionOrchestrator.ExtractAsync` → Stage 3 `BuildPdfExpedienteFromOcrAsync` →
  **`ExtractionOrchestrator.cs:392`** `_txtFieldExtractor.ExtractFieldsAsync(txtSource, fieldDefinitions)`
  → `MapExtractedFieldsToExpediente` (`:552`) → `_fusionService.FuseAsync` (`:313`).
- The hybrid service already runs the deterministic track internally + LLM-text + LLM-vision (flag-gated) and
  reconciles → `Result<ReconciliationResult>` (`Best`, `Candidates`, `ReviewFlags`, `RequiresManualReview`).

## Overriding principle (unchanged): DARK by default, honesty first
Flags default false → **flag-off behaviour must be byte-identical to today** (the deterministic path is the
source of truth; a plausible-wrong LLM value is worse than an abstention). The flip to production is a separate,
evidence-gated, signed-off act (ADR-024 flip procedure) — NOT part of landing the wiring.

## Design fork (the decision this spec settles) — RESOLVED: replace-when-enabled, gated branch

**Recommendation: a flag-gated branch in `ExtractionOrchestrator` Stage 3 that REPLACES the `:392` producer
when the hybrid is enabled, rather than an additive parallel run.** Rationale: the hybrid **already contains**
the deterministic track, so running both would double-run deterministic extraction and create two sources of
truth. Concretely:
- **Flag OFF (default):** the orchestrator does not touch the hybrid; the existing `:392` deterministic path
  runs unchanged. Zero behavioural delta, zero risk. (This is the non-negotiable equivalence requirement.)
- **Flag ON:** Stage 3 produces the PDF-source `Expediente` from the hybrid's reconciled `Best`, and threads
  `RequiresManualReview` / `ReviewFlags` into the fusion's manual-review signal (see §"ManualReview threading").

Rejected alternative — *additive parallel* (run deterministic `:392` AND hybrid, compare downstream): doubles
deterministic work, muddies which producer fused, and duplicates the reconciler that already lives inside the
hybrid. No benefit over replace-when-enabled.

### Sub-decision (OPEN, owner/architect): the OCR-duplication seam
`IHybridExtractionService.ExtractAsync(byte[] pdfBytes, string documentId, …)` takes **raw PDF bytes and
re-OCRs internally**. But the worker has **already OCR'd** the PDF by Stage 3 (`txtSource` at
`ExtractionOrchestrator.cs:378-381`). Calling the hybrid as-is would **OCR the document twice** and risk two
divergent OCR texts. Two ways to resolve, pick before coding:
- **(S4C-i, recommended) Add a text-accepting overload** to `IHybridExtractionService` (e.g.
  `ExtractAsync(TxtSource ocrText, byte[]? pdfBytesForVision, string documentId, …)`) so the pipeline passes
  the Stage-2 OCR text it already has; the hybrid skips its own OCR for the text/deterministic tracks and only
  needs `pdfBytes` when the vision track is enabled. Least wasteful, keeps one OCR of record. Additive to the
  demo path (which can keep calling the bytes overload).
- **(S4C-ii) Pass PDF bytes, accept the double-OCR** for v1 and optimise later. Simpler wiring, but two OCR
  runs per document and a latent divergence risk — discouraged for a legal pipeline.

## ManualReview threading (honesty carry-through)
The gate-coupling work (ADR-024 **D8**, commit `a633690d`) added `ReconciliationResult.RequiresManualReview`.
When the hybrid is the Stage-3 producer, S4-C must map that into the pipeline's existing manual-review surface
(`FusionExpedienteService.ManualReviewRequired` / the `Expediente` review state consumed by the review
dashboard + export gate) so an abstained/uncertain LLM-assisted record is never exported as clean. Verify the
exact fusion field/flag and wire `RequiresManualReview || ReviewFlags.Count > 0` into it.

## Blast radius (files that would change — for the eventual build, NOT now)
- `04 Services/Athena/Prisma.Athena.Worker/Program.cs` — register the LLM/hybrid stack (providers +
  `LlmProvidersOptions` bind + `IHybridExtractionService` + reconciler + `LlmTxtFieldExtractor`
  [+ `LlmVisionFieldExtractor`]). Prefer a **minimal targeted registration** (or a single new
  `AddHybridExtraction(worker)` extension) over pulling `AddPrismaInfrastructure` wholesale — keep the
  worker's manual-DI discipline and avoid dragging in unrelated services.
- `03 Orchestration/…/ExtractionOrchestrator.cs` (Stage 3, around `:368-402`) — the flag-gated producer branch;
  inject `IHybridExtractionService` + `IOptionsMonitor<LlmProvidersOptions>`. Mirror in
  `ProcessingOrchestrator.cs` (`:491`) if the in-process monolith path must match.
- `01 Core/Domain/Interfaces/IHybridExtractionService.cs` + `HybridExtractionService.cs` — only if S4C-i
  (text-accepting overload) is chosen.
- Fusion/review wiring — the `RequiresManualReview` → `ManualReviewRequired` thread.
- `appsettings.json` (worker) — the `LlmProviders` section (defaults **false**; never flip the checked-in default).

## Tests (for the eventual build)
- **Flag-off equivalence (the critical one):** an orchestrator/pipeline test proving that with the flags false,
  the Stage-3 output for a fixture is identical to the pre-S4-C deterministic path (no LLM call made — assert
  the provider is never invoked).
- Flag-on: hybrid `Best` becomes the fused `Expediente`; `RequiresManualReview` propagates to the review
  signal; a gate-abstained field yields a manual-review record, not a clean export.
- DI: the worker composition root resolves `IHybridExtractionService` and binds `LlmProvidersOptions` (a
  composition-root test in the worker test project).
- No live-Ollama dependency in CI — mock `ILlmProvider` (canned JSON), same as the S1–S4B suites.

## Gate-B graduation checklist (ADR-024) — ALL required before the flip; NONE assumed met
1. **D6 corpus:** N ≥ 30 documents, stratified by quality tier (the current PRP1-golden is 20). Owner-gated.
2. **D1 per-field accuracy bar** cleared on that corpus for the specific (track, provider, model).
3. **D2 SolicitudPartes no-regression** (the headline field) — hybrid must not lose partes vs deterministic.
4. **D3 coverage floor** (≥ 80% proposed).
5. **D7.1 source-text containment guard** — *NOT YET BUILT.* A shape-valid-but-absent value must abstain. This
   is a hard precondition and a real code task in its own right; it plugs into the D8 `_AbstainedFields` path.
6. **D7.2 / D7.3** authority-guard residuals addressed (or explicitly accepted).
7. **Explicit owner sign-off** on the wiring design (this doc) AND the evidence — ADR-024 says Gate B "does not
   pre-authorize any part of that wiring."
8. Flip via the ADR-024 flip procedure (env/appsettings override, never the checked-in default), with a
   traceable record back to the authorising baseline artifact.

## Standards
.NET 10, `Result<T>` (never throw for business logic), `CancellationToken` on every async, `ConfigureAwait(false)`
in library code (NOT in Blazor UI), warnings-as-errors, xUnit v3 + Shouldly + NSubstitute, `Method_Scenario_Expected`.
Deterministic path must remain the source of truth; do NOT modify `AdaptiveTxtFieldExtractor`.

## Open decisions for owner (blockers to starting the build)
1. **Go/no-go on Gate B** given the checklist above (esp. D6 corpus + D7.1 guard are not met/built).
2. **OCR-duplication seam:** S4C-i (text-accepting overload, recommended) vs S4C-ii (double-OCR).
3. **Scope of "un-dark":** worker Extractor path only, or also the in-process `ProcessingOrchestrator` monolith?
4. Whether to build **D7.1 source-containment** as a prerequisite sub-epic before any S4-C wiring.

## Definition of done (for THIS spec deliverable)
This document exists, is committed on `Liv`, and captures: the current wiring (file:line), the resolved design
fork (replace-when-enabled), the OCR-seam sub-decision, the ManualReview threading, the blast radius, the test
plan (esp. flag-off equivalence), and the Gate-B checklist. **No production code was written.** S4-C build
starts only after the owner clears §"Open decisions".

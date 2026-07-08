# Veriqan Epic E7 — S7.2/S7.3 Header-Image OCR Product Resolution — Design

**Status:** Approved for build. **Author:** Architect (Winston), consolidating a 3-lens design
party (architect + QA + adversarial-implementer). **Date:** 2026-07-08. **Repo:** ExxerCube.Prisma,
branch `Liv`.

---

## 1. Context & goal

The demo product on `good.pdf` is `"Tarjeta de Crédito COSTCO BANAMEX"`, printed only as a
**rendered header image** (not PDF text) — confirmed GO by the S7.0 spike (Tesseract `spa` reads a
page-1 crop cleanly). Today `PdfPigStatementFieldExtractor.ExtractProductName` never sees that
string; it instead matches the "Número de tarjeta 4111…" band and the demo catalog carries a
hand-written alias mapping that token to `TC-BSSB` so the pipeline doesn't abstain
(`UnknownProduct`). S7.2/S7.3 replace that alias hack with a real OCR read of the header image,
resolved against a catalog entry that actually names the product — while keeping the 5-fixture
demo verdict regression bar green. This document is the single buildable design both stories build
against; §5 explains why they cannot ship separately.

---

## 2. Ground-truth findings (verified against code)

| # | Finding | Evidence |
|---|---|---|
| G1 | `FieldKind.Product` already flows through the escalation decorator on every `ExtractFullAsync` call. | `EscalatingStatementFieldExtractor.cs:157` |
| G2 | A resolution stage receives raw `PdfBytes` (not just parsed text), so a stage CAN render+OCR without depending on the PdfPig `Corpus`. | `FieldResolutionContext.cs:44` (`pdfBytes` ctor param), `:60` (`PdfBytes` property) |
| G3 | `DefaultFieldStageProvider.GetHigherStages<TValue>` is the seam that hands the orchestrator real stage implementations, keyed by `FieldKind`; today only `PaymentDueDate` has a non-empty entry. | `DefaultFieldStageProvider.cs:36-53` |
| G4 | `FieldEscalationLadderRegistry.BuildDefaultLadders` is the seam that decides *when* a field escalates and to which stage; today only `PaymentDueDate` has a rung (`StatusGate` → `FuzzyLabel`). | `FieldEscalationLadderRegistry.cs:75-85` |
| G5 | `ShouldEscalate` fires `StatusGate` only when `!current.HasValue` — i.e. only when stage 1 returned `Missing`. A `Found` value at confidence 1.0 never escalates regardless of correctness. | `FieldResolutionOrchestrator.cs:172-173` |
| G6 | `ExtractedField<T>.Found` always sets confidence to 1.0; there is no "found but low-confidence positional" state. | `ExtractedField.cs:82-83` |
| G7 | On `good.pdf`, `ExtractProductName` matches the "Número de tarjeta" band by design (left column, X<200) and returns `Found` confidence 1.0 — the code comment says explicitly this is "technically wrong, BUT load-bearing BY DESIGN" for the current alias hack. | `PdfPigStatementFieldExtractor.cs:834-866`, esp. the NOTE at `:851-858` |
| G8 | The demo catalog's only product row is `TC-BSSB` with aliases `BSSB\|Tarjeta de Crédito BSSB\|Número de tarjeta 4111000000070001` — no "COSTCO BANAMEX" string anywhere. | `Prisma/Fixtures/PRP2/demo/reference-bundle/Demo_Bank_(Iqubica)/products.csv:2` |
| G9 | `ProductResolver.Resolve` is exact-match after `Normalise` (uppercase + collapse-whitespace via `string.Join(" ", value.Split(null, RemoveEmptyEntries))`) — no fuzzy step. | `ProductResolver.cs:33` (call site), `:75-77` (`Normalise`) |
| G10 | The reference bundle (and thus the real catalog) is resolved only at Stage 4 (`BindAsync`), strictly *after* Stage 2/3 extraction — the `productToken` passed into bind is whatever Stage-2/3 extraction already produced. A validator that resolves against the *real* catalog cannot run inside the extraction-stage ladder (which has no bundle reference). | `VerificationPipeline.cs:656-658` (Stage 3, token resolved from `statementModel`) and `:666-669` (Stage 4, `BindAsync` receives the already-fixed `productToken`) |
| G11 | `ToExtractedField` marks a candidate `ExtractedByInference` only for `StageId.SemanticSearch` / `StageId.LlmExtraction`; every other stage (including any new one) is stamped plain `Extracted`. | `FieldResolutionOrchestrator.cs:265-267` |
| G12 | `ExtractionProvenance.Stage` already carries the producing `StageId` on every `ExtractedField<T>`; no separate "source" type exists or is needed — a new `StageId.HeaderImageOcr` value is itself the provenance signal. | `ExtractionProvenance.cs:24-28`, `StageId.cs:14-47` |
| G13 | `CountExtractedFields` counts `Product` (and 6 other header fields + up to 22 `PeriodSummary` fields) toward the `TenantProfile.DefaultMinExtractionCoverageCount` floor (10); it is provenance-blind (counts any non-`NotExtracted` status). | `VerificationPipeline.cs:1184-1200`ff, `TenantProfile.cs:183` |
| G14 | `TesseractEngine` must be created at most once per process and is not thread-safe; the existing Prisma OCR executor fixes this with a lazy singleton + `SemaphoreSlim(1,1)`. | `TesseractOcrExecutor.cs:10-28` (design remarks), `:41-42` (`_engine`/`_engineLock`), `:107-138` (lock/lazy-init pattern) |
| G15 | `DefaultFieldStageProvider` is instantiated fresh with no injected dependencies today (a bare `new()`), and stages are looked up per field per document via `_stageProvider.GetHigherStages<TValue>(fieldKind)` inside `FieldResolutionOrchestrator.ResolveAsync`. | `DefaultFieldStageProvider.cs:19` (no ctor), `FieldResolutionOrchestrator.cs:103` |
| G16 | `Veriqan.Infrastructure.Extraction.csproj` has zero OCR package reference today (`PdfPig`, `ZXing.Net`, `PDFtoImage`, `CoenM.ImageSharp.ImageHash`, `SixLabors.ImageSharp`, `FuzzySharp` — no `Tesseract`). | `ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.csproj` (`<ItemGroup>` package list) |
| G17 | `Tesseract` NuGet is centrally pinned at 5.2.0 and ships Windows-only natives; Prisma's own `Web.UI/Dockerfile` already solves the Linux-native gap via an apt install + a soname-symlink shim script; `Veriqan.Worker/Dockerfile` has no such provisioning at all. | `Directory.Packages.props:37`; `Web.UI/Dockerfile:54,63-64,66-67` (apt install + shim invocation) and `eng/link-tesseract-natives.sh` (symlink logic); `Veriqan.Worker/Dockerfile` (full file, no tesseract lines) |
| G18 | `FuzzyLabelStage<TValue>` is the established structural precedent for a new higher stage: stateless per call, constructed with pure delegates, returns `FieldCandidate<TValue>.None(Stage)` rather than fabricating a value on any failure path. | `FuzzyLabelStage.cs:56-154`, honesty-discipline remark at `:32-40` |
| G19 | An existing xUnit v3 `[CollectionDefinition(Name, DisableParallelization = true)]` pattern is already used in this codebase to serialize tests around a process-global singleton resource. | `MetricsIsolationCollection.cs` (Veriqan.Orchestration.Tests) |
| G20 | The 150 DPI fiscal-block render + `Conversion.ToImage(pdfStream, ..., options: new RenderOptions(Dpi: ...))` pattern is the direct precedent for rendering page 1 for OCR. | `PdfPigStatementFieldExtractor.cs:3467` (`FiscalPageRenderDpi = 150`), `:3577-3581` (render call) |
| G21 | good.pdf and its 4 non-scanned siblings are 612×792pt (US Letter) per the synthetic corpus's real-layout clone constants; the synthetic generator's S6.2.2 "real-layout clone" specimen already reproduces this page geometry and its own product string at a hardcoded (x, y-bottom) position, but has no header-image/OCR concept yet. | `scripts/veriqan-corpus/synth_gen.py:379` (`PAGE_HEIGHT_PT_S622 = 792.0`), `:465` (`productName` field), `:500-513` (page1 build) |

---

## 3. Design decisions

### 3.1 Seam: new `HeaderImageOcrStage` behind the existing ladder machinery

**Decision:** Add `HeaderImageOcrStage : IFieldResolutionStage<string>` and `StageId.HeaderImageOcr`.
Register it for `FieldKind.Product` in `DefaultFieldStageProvider.GetHigherStages<TValue>` (G3),
and add a ladder rung for `FieldKind.Product` in `FieldEscalationLadderRegistry.BuildDefaultLadders`
(G4). No change to `IStatementFieldExtractor`, `PdfPigStatementFieldExtractor`, or
`VerificationPipeline`.

**Rationale:** G1+G2 prove the seam already exists and already receives everything a render+OCR
stage needs (raw PDF bytes) without touching the positional extractor or the pipeline. This is the
same pattern `FuzzyLabelStage` already established for `PaymentDueDate` (G18) — adding a field's
second rung is "a data change to `BuildDefaultLadders`, not new orchestrator code" (per that
registry's own doc comment).

**Rejected alternative:** Extending `ExtractProductName` itself to fall back to OCR inline. Rejected
because it would re-couple positional extraction to OCR/rendering concerns the ladder architecture
was built specifically to keep separate, and it would bypass the orchestrator's disagreement-gate
and provenance machinery entirely (no way to mark the result `ExtractedByInference`-equivalent, no
honesty-preserving abstention path).

### 3.2 Region: fixed-DPI render + fractional top-band crop, ≥30% of page height

**Decision:** Render page 1 via `Conversion.ToImage(pdfStream, page: 0, options: new
RenderOptions(Dpi: 150))` (mirroring G20's constant and call pattern). Compute a fractional crop
rectangle from the page's `MediaBox` — **top 30% of page height**, full width — and crop the
rendered bitmap to that band before handing it to Tesseract. Pin Tesseract PSM (single-block /
`PSM.SingleBlock` or equivalent) + OEM, language `spa`. After OCR, regex-match the product-heading
shape `Tarjeta de Crédito .+` (case/accent-tolerant) against the returned text.

**Rationale:** No per-fixture pixel coordinates (would break the moment page content shifts,
unlike the fixture-agnostic positional/fuzzy stages). The design party's adversarial pixel check
found the left-column "Tarjeta…" occurrence sits at **20.6–21.5% of page height** on the real
demo fixtures — inside a naive 25% crop but with only ~4pt of margin, i.e. one font-size or layout
tweak away from a silent miss. **30%** was chosen over 25% specifically to absorb that margin.
Fixed 150 DPI matches the codebase's one existing render precedent (G20) rather than inventing a
second DPI convention.

**Rejected alternative:** OCR the whole page. Rejected — slower, and raises false-positive risk of
matching `Tarjeta de Crédito` inside unrelated CONDUSEF glossary prose elsewhere on the page (the
same risk `FuzzyLabelStage` already had to design around, G18's remark on `:44-46` of that file, for
label matching); a header-only crop is not just an optimization but a genuine precision gain.

### 3.3 Determinism / caching: content-hash keyed, two-tier tests

**Decision:** Cache the OCR result keyed by a content-hash of the **rendered crop bytes** (reuse
the perceptual-hash precedent already present in this file via `CoenM.ImageSharp.ImageHash`,
already a package reference of `Veriqan.Infrastructure.Extraction`, G16). Cache is in-memory on the
singleton OCR-engine service (§4.C). Tests come in two tiers:
- **T1 (gated):** a committed cached-OCR snapshot (crop-hash → OCR text) drives CI; the assertion
  is on the **resolved `ProductId`** (semantic round-trip), per the E6.S6.2.6 house rule "gate on
  geometry/identity, not raw bytes/text."
- **T2 (non-gating canary):** `[Trait("Category","LiveOcr")]` invokes the real Tesseract engine on
  the same crop and diffs its output against the T1 snapshot, so silent native-wiring rot (the
  Docker gap in §4.B) is caught without blocking every CI run on a native OCR dependency.

**Rationale:** Matches the codebase's existing "gate on resolved identity not raw text" discipline
(explicitly the lesson baked into E6.S6.2.6's corpus-manifest gate) and its existing perceptual-hash
precedent for caching render-derived artifacts.

**Rejected alternative:** No caching (always re-run Tesseract per document). Rejected: OCR is
comparatively slow and non-deterministic-in-timing under the singleton-engine serialization design
(§4.C), and every one of the 4 non-scanned demo fixtures shares the same header — cache reuse
directly reduces both wall-clock and load on the single serialized engine.

---

## 4. Showstopper resolutions

### A. Trigger won't fire + catalog gap → S7.2 and S7.3 co-land

**Root cause (verified):** On `good.pdf`, `ExtractProductName` returns `Found` confidence 1.0 (G7),
so `ShouldEscalate`'s `StatusGate` check (`!current.HasValue`, G5) is `false` and the OCR stage
never runs. Even if it did run, the catalog has no "COSTCO BANAMEX" entry (G8), so
`ProductResolver` would return `UnknownProduct` regardless of a perfect OCR read.

**Resolution — StatusGate-via-neuter (not a bundle-aware `ValidatorFailure`):**
1. **Neuter the positional card-number fallback.** Modify `ExtractProductName` so that when the
   only "Tarjeta"-anchored match at the left column is actually the "Número de tarjeta …" digit
   band (i.e. the band's text is a card-number pattern, not a product-name phrase), it returns
   `ExtractedField<string>.Missing(...)` instead of `Found`. This is a **change to the existing
   fallback comment block at `PdfPigStatementFieldExtractor.cs:851-858`** — the alias hack the
   comment describes is exactly what is being retired.
2. Because stage 1 now returns `Missing`, `ShouldEscalate`'s `StatusGate` trigger fires (G5) and
   `HeaderImageOcrStage` runs.
3. **Add the real catalog row.** Extend `products.csv` (G8) with a genuine
   `TC-COSTCO-BANAMEX,Tarjeta de Crédito COSTCO BANAMEX,COSTCO BANAMEX|Tarjeta de Crédito COSTCO
   BANAMEX,...` entry (columns per the existing `productId,productName,aliases,...` header).

**Why not a bundle-aware `ValidatorFailure` trigger:** `EscalationTrigger.ValidatorFailure` is
evaluated purely against `ladder.Validator.IsValid(current.Value)` inside
`FieldResolutionOrchestrator` (verified at `FieldResolutionOrchestrator.cs:178-184`), which has no
route to the real `VecReferenceBundle` — the bundle is resolved only at Stage 4 `BindAsync`, strictly
after extraction (G10). A validator that "knows" the catalog would need the bundle threaded through
the extraction-stage ladder, which does not exist and is out of scope. StatusGate-via-neuter is the
only trigger mechanism wireable with the seams that exist today.

**Consequence — why S7.2+S7.3 must co-land:** deploying the neutered extractor without the new
`HeaderImageOcrStage` registered would turn `Product` into a hard `Missing` on all 4 non-scanned
demo fixtures (positional now abstains, nothing recovers it) → `UnknownProduct` → verdict regression.
Deploying the OCR stage without the catalog row would resolve `Missing` → real OCR text → still
`UnknownProduct` (no matching alias). Deploying the catalog row alone changes nothing (nothing
triggers escalation). All three sub-changes are one atomic slice.

### B. Docker native provisioning

**Resolution:** Add the identical apt-install + shim-script block Prisma's `Web.UI/Dockerfile`
already uses (G17) to `Veriqan.Worker/Dockerfile`'s runtime stage:
```dockerfile
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-spa tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*
COPY "Prisma/Code/Src/CSharp/eng/link-tesseract-natives.sh" /tmp/link-tesseract-natives.sh
RUN bash /tmp/link-tesseract-natives.sh /app && rm /tmp/link-tesseract-natives.sh
```
placed after the `COPY --from=build /app/publish .` line so `/app/x64/leptonica-1.82.0.dll` exists
for the shim's guard check (`link-tesseract-natives.sh` exits 0 harmlessly if that file is absent,
so ordering after the publish copy is required, not optional).

**Failure mode called out explicitly:** this is silent on a dev box (Tesseract's Windows-native
NuGet payload works fine there) and only bites inside the Linux container — exactly the class of
gap the `Web.UI` precedent was already built to close once, elsewhere.

### C. OCR engine DI lifecycle

**Root cause (verified):** `DefaultFieldStageProvider` is built fresh per field per document with
no injected dependencies (G15), and `TesseractEngine` must be created at most once per process,
non-thread-safe (G14). A stage that owns its own engine would create a new engine per document —
the documented deadlock.

**Resolution:**
1. Add a **new, Veriqan-owned** singleton service — do **not** depend on Prisma's
   `Infrastructure.Extraction.Ocr.Teseract.TesseractOcrExecutor` (different bounded context;
   `Veriqan.Infrastructure.Extraction` has no reference to it and none should be added). Add
   `PackageReference Include="Tesseract"` to `Veriqan.Infrastructure.Extraction.csproj` (G16) and
   implement a thin `IHeaderProductOcrEngine` with the same lazy-singleton +
   `SemaphoreSlim(1,1)`-serialized pattern as G14 (create-once, lock around `engine.Process`).
2. Register it `AddSingleton<IHeaderProductOcrEngine, TesseractHeaderProductOcrEngine>()` in the
   Veriqan Worker's composition root.
3. Give `DefaultFieldStageProvider` a constructor taking `IHeaderProductOcrEngine` (currently has
   none, G15) and pass it into `HeaderImageOcrStage`'s constructor when building the
   `FieldKind.Product` stage list. `FieldResolutionOrchestrator` itself needs no change — it already
   just calls `_stageProvider.GetHigherStages<TValue>(fieldKind)` (G15) and does not care how the
   provider is constructed.
4. `DefaultFieldStageProvider` must itself become a DI-registered singleton (or at minimum resolve
   the engine once) rather than the implicit bare `new()` some callers may use today — the design
   task list in §5 calls this out as an explicit DI-wiring step.

### D. Provenance

**Resolution:** No new type needed. `ExtractionProvenance.Stage` (G12) already distinguishes
`StageId.HeaderImageOcr`-sourced values from positional ones — a test can assert
`product.Provenance.Stage == StageId.HeaderImageOcr` directly. The one required code change is
**extending the inference-marking predicate** in `FieldResolutionOrchestrator.ToExtractedField`
(G11, `:265-267`) to also treat `StageId.HeaderImageOcr` as `ExtractedByInference`:
```csharp
var status = candidate.Stage == StageId.SemanticSearch
    || candidate.Stage == StageId.LlmExtraction
    || candidate.Stage == StageId.HeaderImageOcr
    ? ExtractionStatus.ExtractedByInference
    : ExtractionStatus.Extracted;
```
Without this, an OCR-recovered product presents identically to a full-confidence positional read
downstream (G11's exact failure mode) — a real risk given the whole point of this epic is to prove
the value came from a genuine second-source read, not a reintroduced shortcut.

### E. Coverage-floor (latent, document only)

`CountExtractedFields` (G13) is provenance-blind — it will count an OCR-recovered `Product` toward
the same floor (10, `TenantProfile.DefaultMinExtractionCoverageCount`) as any positionally-extracted
field. This is a **pre-existing** latent gap (not introduced by this epic): `scanned.pdf` already
sits comfortably below the floor without any OCR field and stays `ExtractionGap` today. Adding one
more possible-Extracted field (`Product`, now sometimes `HeaderImageOcr`-sourced) marginally raises
the ceiling of what an otherwise-degraded document could count toward the floor. **No fix required
now** — flagged for a future coverage-floor hardening pass if/when more OCR-derived fields are
added.

### F. S7.1 shared contract (independent sibling)

The synthetic generator renders text today (`put(...)` calls at fixed PdfPig-space coordinates,
G21); it has no rasterized-image concept. For S7.1 to exercise this OCR stage, it must **rasterize**
a product banner (e.g. via PyMuPDF `page.insert_image(rect, pixmap=...)` or render-to-image-then-
reinsert) into the **same fractional top-30%-of-page-height band** this stage crops and reads — a
header-crop-only raster, structurally distinct from the existing whole-page-flatten
`build_scanned_doc` path used for the `scanned.pdf` fixture.

**Shared contract (S7.1 builds against this independently of S7.2/S7.3 landing):**
- Fractional rect: `(x0=0.0, y0=0.0, x1=1.0, y1=0.30)` of `MediaBox`, expressed as a page-space
  rectangle at generation time (mirrors the `page_height - bottom` conversion already used at
  `synth_gen.py:115-121`).
- The manifest's `Product` field carries the expected **resolved identity** (`productId` +
  `productName` string), not raw OCR text — the manifest assertion route is **resolved-ProductId /
  LiveOcr**, not the deterministic word-geometry gate the rest of the corpus uses (per the
  E6.S6.2.6 house rule already established in this codebase: gate on geometry for text-layer
  fields, gate on resolved identity for anything OCR-derived).
- S7.1 does not depend on the `HeaderImageOcrStage` implementation existing yet — it only needs to
  produce a fixture whose rasterized header band is byte-deterministic and whose manifest declares
  the expected product identity, so S7.2/S7.3's test suite can consume it once landed.

---

## 5. Story re-sequencing & implementation task list

**S7.2 and S7.3 co-land as one atomic slice.** Per §4.A, the extractor neuter (would-be S7.2 alone)
breaks the demo the instant it ships without the OCR stage + catalog row (would-be S7.3) landing in
the same change — there is no safe intermediate commit that keeps all 5 demo verdicts green. Treat
the epic tracker's S7.2/S7.3 split as a *documentation* split (what changed vs. why), not a
*deployment* split.

**S7.1 is the independent sibling.** It has no code dependency on S7.2/S7.3 (§4.F) and can be built,
reviewed, and merged in parallel — it only needs the shared contract above so its output fixture is
consumable once the OCR stage lands.

**Ordered implementation task list (S7.2/S7.3 atomic slice):**

1. **Veriqan.Infrastructure.Extraction**: add `PackageReference Tesseract`; implement
   `IHeaderProductOcrEngine` / `TesseractHeaderProductOcrEngine` (lazy singleton, semaphore-
   serialized, per §4.C.1). Unit-test the crop-hash cache in isolation (no real Tesseract call).
2. **Domain**: add `StageId.HeaderImageOcr` (`StageId.cs`).
3. **Veriqan.Infrastructure.Extraction/Resolution**: implement `HeaderImageOcrStage :
   IFieldResolutionStage<string>` (render page 1 at 150 DPI → crop top 30% → OCR via the engine
   from step 1 → regex-match `Tarjeta de Crédito .+` → `FieldCandidate<string>.Found`/`None`,
   following the `FuzzyLabelStage` abstention discipline, G18).
4. **`DefaultFieldStageProvider`**: add ctor param `IHeaderProductOcrEngine`; register the new stage
   for `FieldKind.Product` in `GetHigherStages<TValue>` (§4.C.3). Update DI registration wherever
   `DefaultFieldStageProvider` is currently constructed to supply the engine and register the
   provider itself as a singleton (§4.C.4).
5. **`FieldEscalationLadderRegistry`**: add the `FieldKind.Product` rung —
   `StageId.HeaderImageOcr` on `EscalationTrigger.StatusGate` — in `BuildDefaultLadders` (mirrors
   the existing `PaymentDueDate` entry pattern, G4).
6. **`FieldResolutionOrchestrator.ToExtractedField`**: extend the inference-marking predicate to
   include `StageId.HeaderImageOcr` (§4.D).
7. **`PdfPigStatementFieldExtractor.ExtractProductName`**: neuter the card-number-band fallback so
   it returns `Missing` instead of `Found` when the matched band is a digit/card-number pattern,
   not a product-name phrase (§4.A.1). This is the one change to the file the settled decisions
   said NOT to touch structurally — it is a narrowing of an existing method's output, not a new
   dependency or a change to the `IStatementFieldExtractor` contract.
8. **Demo catalog**: add the `TC-COSTCO-BANAMEX` row to `products.csv` (§4.A.3).
9. **`Veriqan.Worker/Dockerfile`**: add the tesseract apt-install + native-shim block (§4.B).
10. **Test fixtures**: commit the T1 cached-OCR snapshot (crop bytes → OCR text) for the 4
    non-scanned demo fixtures' shared header crop.
11. **Green-gate order**: `Veriqan.Infrastructure.Extraction.Tests` (new stage unit tests + T1
    snapshot tests) green → `Veriqan.Orchestration.Tests` (`VecChecklistDemoE2ETests`, full 5-
    verdict regression bar, §6) green → optionally the `LiveOcr`-traited canary run manually /
    in a native-capable CI lane.

---

## 6. Test / verification plan

- **T1 — gated snapshot test** (`Veriqan.Infrastructure.Extraction.Tests`): committed crop-bytes →
  OCR-text snapshot for the shared demo header; assertion is on the **resolved `ProductId`**
  end-to-end (extractor → resolver), not raw OCR string equality, per the E6.S6.2.6 house rule.
  Must run in every CI lane (no native Tesseract dependency at test time — it reads the snapshot).
- **T2 — LiveOcr canary** (`[Trait("Category","LiveOcr")]`, same test project): invokes the real
  `IHeaderProductOcrEngine` against the same crop and asserts its output matches the T1 snapshot
  (within a tolerance / after the same regex normalization). Non-gating in default CI runs; wired
  into whatever lane already has native Tesseract available (mirrors how `Extraction.Teseract`
  already runs 154/154 against real Tesseract per `CLAUDE.md`'s live-verification note).
- **CI trait convention + exclusion (closed F1/M1, 2026-07-08)**: every test class that
  constructs the real `TesseractHeaderProductOcrEngine` (directly, or by joining
  `[Collection(VeriqanHeaderOcrCollection.Name)]`) carries `[Trait("Category", "LiveOcr")]`
  alongside the `[Collection(...)]` attribute — currently `HeaderImageOcrStageLiveOcrCanaryTests`,
  `SyntheticHeaderOcrProductTests`, `PaymentDueDateFuzzyRecoveryCanaryTests`,
  `EscalationSeamBehaviorNeutralTests`, `EscalatingExtractorRebuildPathTests` (Extraction), plus
  `VecChecklistDemoE2ETests` and `SyntheticDefectVerdictE2ETests` (Orchestration). Two classes were
  audited and deliberately left **untagged** because they register the real engine via
  `AddVeriqanExtraction()` but never actually invoke it — `HeaderImageOcrStage.TryResolveAsync`
  only fires when stage-1 positional extraction abstains (`FieldResolutionOrchestrator.ShouldEscalate`
  gated on `StatusGate`), and every specimen these two tests touch resolves the product from PDF
  text at stage 1: `VerificationPipelineEndToEndTests` and `CalibrationDriverTests`/`CalibrationHarness`
  (corpus-manifest.json specimens). `HeaderImageOcrStageSnapshotTests` (T1, above) stays trait-free
  by design — it is the one Tesseract-free gate. CI (`quality-gates.yml`) excludes the trait from
  both unfiltered `dotnet test` runs via `dotnet test ... -- --filter-not-trait "Category=LiveOcr"`
  — this repo runs xUnit v3 on **Microsoft.Testing.Platform**, not VSTest, so the exclusion must use
  MTP's own simple-filter switch (passed after `--`) rather than the legacy VSTest
  `--filter "Category!=LiveOcr"` form, which MTP does not honor.
- **Provenance assertion**: a dedicated test resolves `good.pdf`'s `Product` field and asserts
  `Provenance.Stage == StageId.HeaderImageOcr` and `Status == ExtractionStatus.ExtractedByInference`
  — proves the value came from the new stage, not a reintroduced positional shortcut (§4.D).
- **Fallback-starved regression fixture**: a fixture (or the existing 4 non-scanned demo PDFs,
  which already share the property) where the neutered `ExtractProductName` returns `Missing` —
  asserts `StatusGate` fires and `HeaderImageOcrStage` is actually invoked (not just that the final
  answer happens to be right).
- **Alias-starved regression fixture**: a fixture whose OCR'd text does NOT match any catalog
  alias — asserts the pipeline abstains honestly (`UnknownProduct` / `Missing`, never a fabricated
  match), proving `HeaderImageOcrStage` inherits the `FuzzyLabelStage` abstention discipline (G18)
  rather than picking a nearest-guess.
- **The 5-verdict regression bar** (`VecChecklistDemoE2ETests`, `Veriqan.Orchestration.Tests`) must
  stay exactly as documented at `VecChecklistDemoE2ETests.cs:211-241`:
  `good.pdf`→Red(LAW-SEC-PRESENCE), `bad-math-cl21.pdf`→Red(CL-21+CL-22),
  `bad-font-cl35.pdf`→Red(CL-35), `scanned.pdf`→ExtractionGap,
  `compliant-master.pdf`→Red(LAW-SEC-PRESENCE); Bank=Yellow / Condusef=Red on all non-scanned
  fixtures. This is the primary "did we break the demo" gate — run it after every task in §5's
  list from step 7 onward, not just once at the end.
- **xUnit non-parallel collection**: define a `[CollectionDefinition("VeriqanHeaderOcr",
  DisableParallelization = true)]` (mirrors the existing `MetricsIsolationCollection` pattern, G19)
  and place every test class that constructs a real `IHeaderProductOcrEngine` (T2 canary + any
  integration test exercising the real singleton) into it — prevents concurrent tests from
  double-initializing the process-global `TesseractEngine` (G14's deadlock).
- **Exact-match-first policy for `ProductResolver`**: per G9, ship without a fuzzy pre-step on the
  OCR output — the S7.0 spike already confirmed a clean read on the real fixtures, and the crop-
  band + regex-match design in §3.2 is intended to produce a clean string. Document
  `FuzzyLabelStage`-style fuzzy matching as the fallback lever (not built now) if live-fixture drift
  ever produces a near-miss OCR string that fails exact match.

---

## 7. Open risks / owner decisions still needed

1. **Exact-match vs. fuzzy on OCR output (§6, last bullet).** The design leans exact-match-first
   per the spike's clean read. If the owner wants insurance against OCR noise (extra whitespace,
   accent drops, a stray character) from day one, say so now — adding the `FuzzySharp` pre-step
   (seam already exists, G18) is a small addition but changes the "abstain honestly" boundary
   (fuzzy match introduces a score-threshold judgment call that exact match does not have).
2. **Crop-band percentage (30%) is derived from the 4 known demo fixtures only.** If any future
   fixture (real or synthetic) has a materially different header layout, 30% may need
   re-validation — this is a fixture-corpus-dependent constant, not a computed one.
3. **`TC-COSTCO-BANAMEX` catalog row content** (aliases, `hasRewardsProgram`, `annualCommission`,
   `currency` columns) — this design specifies the resolution *mechanism* (§4.A.3) but the owner
   should confirm the actual commercial values for those columns before they ship in a client-
   facing demo catalog.
4. **Coverage-floor hardening (§4.E)** is explicitly deferred, not resolved — flag if the owner
   wants it pulled into this slice rather than left as documented latent risk.

---

## 8. Owner rulings (2026-07-08) — SETTLED, build authorized

1. **Build now** — dev+qa pipeline authorized on the atomic S7.2/S7.3 slice; orchestrator verifies
   every step from ground truth (build 0/0 + 5-verdict regression bar).
2. **Fuzzy from day one (OVERRIDE of §6/§7.1 exact-match-first).** Add a `FuzzySharp` fuzzy-match
   fallback in `ProductResolver` (01 Core/Veriqan.Application/Services/ProductResolver.cs): after the
   existing exact-match miss (uppercase+whitespace-collapse), attempt fuzzy match of the OCR'd string
   against catalog `ProductName` + aliases with a score threshold; below threshold → abstain
   (`UnknownProduct`, never nearest-guess). The threshold is a new calibrated constant — document it
   and keep the "abstain honestly" boundary explicit. NOTE: fuzzy must live at resolution time
   (Application layer), NOT inside `HeaderImageOcrStage` — the stage has no bundle (catalog is only
   resolved at Stage-4 `BindAsync`, `VerificationPipeline.cs:656-669`).
3. **Carry demo values, fix name/alias** — the new `TC-COSTCO-BANAMEX` row reuses the existing
   `TC-BSSB` demo row's commercial column values; only `productId`, `productName`
   (`Tarjeta de Crédito COSTCO BANAMEX`), and aliases change. Demo-safe (verdicts key on
   LAW-SEC/CL-21/CL-35, not these columns); reversible.
4. **Pull coverage-floor hardening into this slice (OVERRIDE of §4.E defer).** Make
   `CountExtractedFields` (`VerificationPipeline.cs:1184-1200`) provenance-aware: exclude
   non-positional (OCR / `ExtractedByInference`) provenance from the extraction-floor count, so the
   floor stays a *text-coverage* measure. Assert `scanned.pdf` still yields `ExtractionGap`
   (its OCR-recovered Product must NOT count toward the floor). New regression surface — add a
   targeted test.

**Added implementation tasks (append to §5 list):**
12. **`ProductResolver`**: FuzzySharp fuzzy-match fallback after exact-match miss, with a documented
    score threshold + honest-abstention below it (owner ruling 2).
13. **`VerificationPipeline.CountExtractedFields`**: exclude OCR/inference provenance from the floor
    count; add a test asserting `scanned.pdf` stays `ExtractionGap` with an OCR-sourced Product
    present (owner ruling 4).

# Epic 2 — Confidence Pipeline Hardening: Test Strategy

**Authored:** 2026-06-25 · **Branch:** `Liv` · **Status:** PLANNING — do not execute until source stories are merged  
**Source of truth:** `docs/planning-artifacts/remediation/CONFIDENCE-PIPELINE-DEEP-INSPECTION-2026-06-25.md` (all file:line citations below resolve to that inspection unless prefixed with another path)  
**Test stack:** xUnit v3 (`xunit.v3.mtp-v2` 3.2.2 + MTP 2.1.0), Shouldly, NSubstitute — see CLAUDE.md §Testing stack  
**Mutation-testing companion:** `docs/qa/test-plans/mutation-testing-roadmap.md` (hardened surface that backstops Tier-3 refactor)

---

## 0. Story map — the nine canonical stories

The inspection identifies three independently shippable tiers across nine stories.
Each story ID below is used as the index throughout this document.

| ID   | Tier | Story (from inspection §5)                                        | Source |
|------|------|-------------------------------------------------------------------|--------|
| 2.1  | 1    | Wire OCR body text into Stage 4 classifier input                  | T1.1   |
| 2.2  | 1    | Return `Unknown` (not `(Aseguramiento, 10)`) on no-signal input   | T1.2   |
| 2.3  | 2    | Consult `TieneAseguramiento` boolean (and other structured flags) | T2.1   |
| 2.4  | 2    | Broaden keyword sets to match generator prose                     | T2.2   |
| 2.5  | 2    | Make classifier confidence a real score with documented semantics | T2.3   |
| 2.6  | 3    | Introduce one `Confidence` value object on a fixed 0–1 scale      | T3.1   |
| 2.7  | 3    | Define explicit aggregation contract for overall document score   | T3.2   |
| 2.8  | 3    | Single source of truth for thresholds (bind `ExportGatePolicy`)   | T3.3   |
| 2.9  | 3    | Fix dual-scale `ClassificationCompletedEvent`                     | T3.4   |

Tiers are independently shippable. **Tier 1 alone turns the §2 max-fidelity gate green.**

---

## 1. Regression guardrail inventory — change-impact analysis

These are the four existing test sites that pin the exact numbers the epic will touch.
They constitute the primary mutation safety net; they must be explicitly managed per story.

### 1.1 ReconciliationOrchestratorExportGateTests.cs

**Location:** `08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ReconciliationOrchestratorExportGateTests.cs`

Six test methods (lines 148–430) exercise the three-gate export policy using the confidence values **50 / 95 / 30 / 40 / 90** around the hardcoded threshold **70**:

| Line | Test method                                                        | Confidence used | Gate(s) exercised |
|------|--------------------------------------------------------------------|-----------------|-------------------|
| 149  | `ReconcileAsync_LowConfidenceClassification_BlocksExport…`        | 50 (below 70)   | Gate 1 (BlockOnLowConfidence) |
| 201  | `ReconcileAsync_FusionManualReviewRequired_BlocksExport…`          | 95 (above 70)   | Gate 2 (BlockOnFusionManualReviewRequired) |
| 251  | `ReconcileAsync_UnresolvedFusionConflicts_BlocksExport…`           | 95 (above 70)   | Gate 3 (BlockOnUnresolvedConflicts) |
| 314  | `ReconcileAsync_HighConfidenceAutoProcessNoConflicts_Exports…`     | 90 (above 70)   | All gates pass |
| 361  | `ReconcileAsync_GateDisabledByPolicy_LowConfidenceDoesNotBlock…`  | 40 (below 70)   | Gate disabled via policy |
| 403  | `ReconcileAsync_AllGateConditionsActive_BlockReasonsContainsAll…` | 30 (below 70)   | All three fire together |

All six tests use a **mocked `IFileClassifier`** (via `CreateSut(classifierConfidence:)`) so they are isolated from the real classifier implementation. They do not care how the classifier derives its score — only the gate's downstream behaviour. This means:

- **Stories 2.1 and 2.2** (wiring fix + Unknown label): these tests must **stay green without modification**. The mock returns a fixed confidence regardless of input wiring.
- **Story 2.8** (bind threshold from config): these tests must be **updated** to pass the threshold explicitly via the policy constructor or a bound options object. The confidence/threshold relationship (50 vs 70, 30 vs 70) must be preserved. The safe approach is to set a known `ExportGatePolicy` with `ClassificationConfidenceThreshold = 70` explicitly in each test rather than relying on the current hardcoded field. The test at line 361 (permissive policy) also needs updating if `BlockOnLowConfidence` is now config-controlled rather than a constructor parameter.
- **Stories 2.3–2.7 and 2.9** (classifier hardening and Confidence type migration): these tests must **stay green**. The gate logic is above the classifier; the test's NSubstitute mock abstracts it.

### 1.2 ProcessingOrchestratorTests.cs:541

**Location:** `08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ProcessingOrchestratorTests.cs:541`

```csharp
// Low confidence classification — fires DocumentFlaggedForReviewEvent
Confidence = 45  // below threshold 70 (ProcessingOrchestrator.cs:56, hardcoded)
```

This test pins the **45 vs 70** flag trigger. The `ClassificationResult.Confidence` is an `int` (0–100 scale, `ClassificationResult.cs:28`).

- **Stories 2.1 and 2.2**: must **stay green**. The test uses a mocked classifier returning confidence 45; the wiring fix does not change the flag-trigger logic.
- **Story 2.8**: if the hardcoded `70` in `ProcessingOrchestrator.cs:56` is moved to a bound config, this test must **verify the bound value** rather than relying on the internal constant. Add a `ProcessingOrchestratorOptions` fixture with `LowConfidenceThreshold = 70` to the test arrange.
- **Story 2.6** (Confidence value object — scale migration from `int 0–100` to `double 0–1`): if `ClassificationResult.Confidence` changes type, this test must be **updated** to use the new type. The assertion `e.Confidence.ShouldBe(...)` (or equivalent) must reference the new scale. Update the arrange value from `45` to `0.45` (or whatever the new canonical form is).

### 1.3 FusionExpedienteServiceMutationTests.cs:236-281

**Location:** `08 Tests/02 Infrastructure/Tests.Infrastructure.Classification/FusionExpedienteServiceMutationTests.cs:236-281`

These mutation-hardening tests pin the fusion source-reliability thresholds:

| Line | Test                                               | Threshold pinned |
|------|----------------------------------------------------|------------------|
| 237  | `Reliability_FlatMetadata_EqualsBaseValues`         | `PDF_BaseReliability = 0.85`, `DOCX_BaseReliability = 0.70` |
| 241  | `Reliability_PdfOcrConfidence_AdjustsAroundPoint75` | `MeanConfidence = 0.85` → `+0.05` boost |
| 251  | `Reliability_PdfImageQuality_AdjustsAroundPoint75`  | `QualityIndex = 0.85` → `+0.03` boost |
| 263  | `Reliability_ExtractionSuccessRate_AddsWeightedBoost_AndClampsTo1` | `successRate = 1.0` → `0.85 + 0.20 → clamp 1.0` |

These numbers derive from `FusionCoefficients.cs:129,136` (`ManualReviewThreshold = 0.70`, `AutoProcessThreshold = 0.85`) which the inspection identifies as semantically equivalent to the gate's `ClassificationConfidenceThreshold = 70` but on a different axis (inspection §3.4.7).

- **Stories 2.1–2.5**: must **stay green**. These tests are pure fusion-reliability arithmetic; they do not touch the classification gate.
- **Story 2.7** (aggregation contract — decision about whether fusion confidence enters the export gate): if the `0.83` fusion overall-confidence is *added* to the gate comparison path, `FusionExpedienteService.CalculateOverallConfidence` is unchanged, but the gate test in §1.1 would need a new test case exercising the fusion-confidence gate leg. The existing mutation tests here must **stay green**.
- **Story 2.8** (single source of truth): if `FusionCoefficients.cs:136` (`ManualReviewThreshold = 0.70`) is absorbed into the bound `ExportGatePolicy`, these tests must **verify the new config-driven threshold** rather than the constant. Do not silently rely on the field default — set it explicitly in each test arrange.

### 1.4 ReconciliationPipelineServiceTests.cs:199,204

**Location:** `08 Tests/04 Services/Athena/Prisma.Athena.Processing.Tests/ReconciliationPipelineServiceTests.cs:199,204`

```csharp
// line 199
.ShouldContain(r => r.Contains("BlockOnFusionManualReviewRequired"), "gate 2 reason string");
// line 204
.ShouldContain(r => r.Contains("BlockOnUnresolvedConflicts"), "gate 3 reason string");
```

These pin the exact block-reason string tokens that appear in `ExportHeldForReviewEvent.BlockReasons`.

- **All stories 2.1–2.9**: must **stay green**. None of the epic stories rename the policy condition identifiers. The strings `"BlockOnFusionManualReviewRequired"` and `"BlockOnUnresolvedConflicts"` are serialised enum names / policy field names; changing them would be a breaking contract change outside the scope of this epic.

---

## 2. New acceptance tests per story

### Story 2.1 — Wire body text into Stage 4 (gate unblock)

**Production change:** `ProcessingOrchestrator.cs:530` and `ReconciliationOrchestrator.cs:353–356` — populate `ExtractedMetadata.LegalReferences` (or a new body-text field) from the Stage-2 OCR result text before calling `_classifier.ClassifyAsync(metadata, ...)`.

**New tests to add** (in `Tests.Infrastructure.Classification` or `Tests.Athena.Processing`):

```
FileClassifierService_WithBodyText_ImssCase_ScoresAseguramientoAt90
```
- Arrange: an `ExtractedMetadata` where `LegalReferences` contains the string `"ASEGURAMIENTO PRECAUTORIO de las cuentas bancarias"`
- Act: `ClassifyAsync(metadata, ct)`
- Assert: `result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento)` and `result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70)`
- Basis: inspection §2.3 — the generator emits this exact phrase for `aseguramiento` cases; with body text present, score = 90, difference = 80 ≥ 60, confidence = 90 (inspection §2.5).

```
ProcessingOrchestrator_Stage4_BodyTextPopulatedFromOcrResult
```
- Arrange: mock `IOcrExecutor` returns an `OcrResult` with `ExtractedText = "ASEGURAMIENTO PRECAUTORIO..."` and `ConfidenceAvg = 95`; real `FileClassifierService` wired (not substituted)
- Act: drive the orchestrator through stages 1–4 with a substituted quality analyzer and no-op fusion
- Assert: the `ClassificationCompletedEvent` that is published carries `Confidence >= 70` (not `10`)
- Basis: inspection §2.4 — if body text is absent, all six scores = 10 and confidence = 10 deterministically.

**Regression guardrails:**
- §1.1 tests: stay green (mocked classifier, unaffected by wiring).
- §1.2 test (`ProcessingOrchestratorTests:541`): stay green (that test exercises the *low-confidence flag*, not the input wiring).

---

### Story 2.2 — Return Unknown on no-signal input

**Production change:** `FileClassifierService.DetermineLevel1Category` (`:238–251`) and `CalculateConfidence` (`:205–236`) — when `maxScore == 10` (all categories at floor), return `ClassificationLevel1.Unknown` at confidence 0 (or an explicit sentinel) instead of the first-inserted-key `Aseguramiento`.

**New tests to add** (in `Tests.Infrastructure.Classification`):

```
FileClassifierService_EmptyMetadata_ReturnsUnknownWithZeroConfidence
```
- Arrange: `ExtractedMetadata` with all fields null or empty (the condition described in inspection §2.4)
- Assert: `result.Value.Level1.ShouldBe(ClassificationLevel1.Unknown)` and `result.Value.Confidence.ShouldBe(0)` (or `ShouldBeLessThan(10)`)
- Basis: inspection §2.4 — confidence 10 means "zero keywords matched", not "weak Aseguramiento evidence." `(Aseguramiento, 10)` is a correctness hazard.

```
FileClassifierService_NonMatchingText_ReturnsUnknown_NotAseguramiento
```
- Arrange: `LegalReferences` contains only a date and a case number (`"14 de junio de 2026 · EXP-0001-2026"`) — none of the Level-1 keyword sets match
- Assert: `result.Value.Level1.ShouldNotBe(ClassificationLevel1.Aseguramiento)`; `result.Value.Level1.ShouldBe(ClassificationLevel1.Unknown)`
- Basis: inspection §2.4 — the `DetermineLevel1Category` tie-break is dictionary-insertion-order, so any all-tie returns `Aseguramiento` deterministically today.

**Mutation safety:** `DetermineLevel1Category` and `CalculateConfidence` are already included in the `Infrastructure.Classification` Stryker scope (`mutation-testing-roadmap.md` §2 — `FileClassifier 89%`, 0 killable survivors). The existing 89% kill-rate means the new Unknown-path branch will surface as NoCoverage survivors until these tests are added; the two new tests above will kill them.

**Regression guardrails:**
- §1.1 tests: the gate tests use a mock classifier that returns confidence 50 / 90 / 95 / 30 / 40. None return 10 or Unknown. All stay green.
- §1.2 test: uses mock returning 45. Stays green.

---

### Story 2.3 — Consult structured signals (TieneAseguramiento)

**Production change:** Route Stage 4 through `ExpedienteClasifierService` (`:365–409`, which already handles `TieneAseguramiento == true` → returns 0.90 — inspection §3.2, T2.1), or fold the boolean check into `FileClassifierService` early in its scoring loop.

**New tests to add** (in `Tests.Infrastructure.Classification`):

```
ClassifierService_TieneAseguramientoTrue_ReturnsAseguramientoAtHighConfidence
```
- Arrange: `Expediente.TieneAseguramiento = true`; no keyword text; `ExpedienteClasifierService` wired (or the folded path in `FileClassifierService`)
- Assert: `result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento)` and `result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70)` (expect 90 if `ExpedienteClasifierService.cs:379` drives it)
- Basis: inspection §3.2 — `ExpedienteClasifierService` already returns 0.90 for `TieneAseguramiento`; it is simply not wired at Stage 4.

```
ClassifierService_TieneAseguramientoFalse_WithKeywordBody_ScoresNormally
```
- Arrange: `TieneAseguramiento = false`; body text contains `"ASEGURAMIENTO PRECAUTORIO"`
- Assert: `result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento)` — keyword path still works when boolean is false.
- Purpose: non-regression for the keyword path when boolean signals are absent.

**Regression guardrails:** stories 2.1 and 2.2 tests: stay green. Gate tests in §1.1: stay green (mocked classifier).

---

### Story 2.4 — Broaden keyword sets to match generator prose

**Production change:** `FileClassifierService.cs:159–172` (PLD keyword set) and equivalent blocks for other types — add the phrases the generator actually emits (inspection §2.2, T2.2). Example: PLD branch must match `"RECURSOS DE PROCEDENCIA ILICITA"` and `"OPERACIONES INUSUALES"`, not only `"LAVADO"`.

**New tests to add** (in `Tests.Infrastructure.Classification`):

```
FileClassifierService_PldGeneratorPhrases_ScoresPldAboveFloor
```
- Arrange: body text = `"recursos de procedencia ilícita derivados de operaciones inusuales identificados"`
- Assert: `result.Value.Level1.ShouldBe(ClassificationLevel1.Pld)` and `result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70)`
- Basis: inspection T2.2 — the generator emits this exact phrase family; the current `FileClassifierService.cs:159–172` keyword set misses it entirely.

One similar cross-check test per category whose generator phrases diverge from the keyword table. Derive the phrase list from `core/legal_catalog.py:190–226` (inspection §2.3). A parameterised `[Theory]` is appropriate:

```csharp
[Theory]
[InlineData("ASEGURAMIENTO PRECAUTORIO de las cuentas bancarias", ClassificationLevel1.Aseguramiento)]
[InlineData("recursos de procedencia ilícita", ClassificationLevel1.Pld)]
// ... one representative phrase per category
public async Task FileClassifierService_GeneratorCanonicalPhrases_ClassifiesCorrectly(
    string bodyText, ClassificationLevel1 expected)
```

**Regression guardrails:** all §1.1–§1.4 tests stay green. The keyword additions are additive; no existing true-hit path changes.

---

### Story 2.5 — Make classifier confidence a real score

**Production change:** `FileClassifierService.CalculateConfidence` (`:205–236`) — replace the bucket heuristic (`scoreDifference ≥ 60 → Math.Min(70, ...)`) with documented band semantics that track actual match strength. At minimum, document the existing bands' semantics.

**New tests to add** (in `Tests.Infrastructure.Classification`):

```
FileClassifierService_SingleStrongHit_ConfidenceIsAbove70
FileClassifierService_WeakHit_ConfidenceIsAboveFloorButBelow70
FileClassifierService_NoHit_ConfidenceIsZeroOrExplicitlyUnknown
```

Each test pins the entry condition (strong keyword match score = 90; weak match score = 70; no match score = 10) and asserts the confidence band boundary is consistent with documented semantics. If the band semantics are documented (T2.3 says "at minimum document"), write tests that assert the documented values exactly — these become the mutation targets.

**Note on overlap with 2.2:** the "no-hit → 0/Unknown" case is already covered by story 2.2. The 2.5 tests focus on the weak-hit band (score difference 0–60, which maps to confidence 10–70 in the current logic).

---

### Story 2.6 — Introduce one Confidence value object

**Production change:** introduce a `Confidence` record/struct in the Domain layer (`01 Core/Domain/ValueObjects/`) with a fixed 0–1 scale, explicit stage provenance, and `Combine`/`Min`/`WeightedAverage` operators. Migrate `OCRResult.ConfidenceAvg` (float 0–100), `FusionResult.OverallConfidence` (double 0–1), and `ClassificationResult.Confidence` (int 0–100) to reference this type.

**New tests to add** (in the Domain test project, or a new `Tests.Domain.ValueObjects`):

```
Confidence_FromOcrPercent_ConvertsToZeroToOne
Confidence_FromFusionDouble_IsPreserved
Confidence_FromClassificationInt_ConvertsToZeroToOne
Confidence_WeightedAverage_RespectsWeights
Confidence_Min_ReturnsLowerBound
Confidence_Combine_AggregatesAcrossStages
```

Exact-value arithmetic tests (the Shouldly pattern used across this codebase):

```csharp
var ocrConf = Confidence.FromOcrPercent(95.71f);    // → 0.9571
ocrConf.Value.ShouldBe(0.9571, tolerance: 0.0001);
ocrConf.Stage.ShouldBe(PipelineStage.Ocr);

var combined = Confidence.WeightedAverage(new[] {
    (ocrConf, weight: 0.4),
    (Confidence.FromFusion(0.83), weight: 0.6)
});
combined.Value.ShouldBe(0.9571 * 0.4 + 0.83 * 0.6, tolerance: 0.0001);
```

**Scale-migration regression tests:** for each migrated result type, add a round-trip test that constructs the result type via the new `Confidence` path and asserts the downstream gate comparison still fires at the semantically equivalent level:

```
ReconciliationOrchestrator_MigratedConfidence_Below0Point70_BlocksExport
ReconciliationOrchestrator_MigratedConfidence_Above0Point70_AllowsExport
```

These replace the current int-based gate assertions at §1.1 lines 149 and 314 once the migration is complete.

**Mutation safety:** `Confidence.WeightedAverage` and `Confidence.Min` are pure arithmetic; they are ideal Stryker targets. Add the new value object to the existing `Tests.Domain` `stryker-config.json` after the story is merged. Expected kill rate ≥ 90%; equivalents are the `Stage` provenance enum (order-independent).

**Regression guardrails:**

| §1 site | Disposition after 2.6 |
|---------|----------------------|
| `ReconciliationOrchestratorExportGateTests.cs` | Update the `classifierConfidence:` parameter from `int` to `double 0–1` (e.g., `50` → `0.50`). Assertions on `ExportHeldForReviewEvent.ClassificationConfidence` must match the new type. |
| `ProcessingOrchestratorTests.cs:541` | Update arrange: `Confidence = 45` (int) → `Confidence = Confidence.FromClassificationInt(45)` or `0.45` (double). |
| `FusionExpedienteServiceMutationTests.cs:236–281` | The fusion reliability values (0.85, 0.70) are already on the 0–1 scale; assert they are `Confidence` values post-migration. No semantic change. |
| `ReconciliationPipelineServiceTests.cs:199,204` | Reason strings unchanged; stay green. |

---

### Story 2.7 — Define explicit aggregation contract

**Production change:** `ReconciliationOrchestrator.EvaluateExportGate` (`:214–258`) — document or implement the decision on whether `OverallConfidence (0.83)` from fusion should be compared to a threshold, and whether `QualityIndex` flows from `ExtractionOrchestrator.cs:403–408`.

**New tests to add:**

If the decision is **"fusion confidence enters a new Gate 0":**

```
ReconciliationOrchestrator_FusionConfidenceBelowAggregationThreshold_BlocksExport
ReconciliationOrchestrator_FusionConfidenceAtOrAboveThreshold_AllowsGate0
```

These follow the exact pattern of the existing gate tests in §1.1 but set `FusionResult.OverallConfidence` to a value below/above the new threshold.

If the decision is **"fusion confidence is advisory only":**

```
// No new gate test; instead document the decision:
ExportGatePolicy_FusionConfidenceIsAdvisory_NotGated
```
A test that verifies `ExportGatePolicy` has no `FusionConfidenceThreshold` property (or that it is always zero/null), confirming the advisory-only decision is structural.

**QualityIndex flow test:**

```
ExtractionOrchestrator_PopulatesQualityIndexFromAnalyzer_BeforeFusion
```
- Arrange: `IImageQualityAnalyzer` returns a quality assessment with a known `QualityIndex` value; capture the `ExtractionMetadata` passed to `IFusionExpediente.FuseAsync`
- Assert: `metadata.QualityIndex.ShouldBe(expected)` (today this field is never set — inspection §3.2, `ExtractionOrchestrator.cs:403`)
- This test confirms the wiring change required by T3.2, regardless of whether the gate uses it.

---

### Story 2.8 — Single source of truth for thresholds (bind ExportGatePolicy)

**Production change:** `ExportGatePolicy.cs:28` (`SectionName = "ExportGatePolicy"`) — add `Configure<ExportGatePolicy>(config.GetSection(ExportGatePolicy.SectionName))` to both composition roots (`Reconciliator Program.cs:157`, `ProcessingOrchestrator.cs:116`); promote the hardcoded `70` at `ReconciliationOrchestrator.cs:42` and the duplicate at `ProcessingOrchestrator.cs:56` into `ExportGatePolicy.ClassificationConfidenceThreshold`; reconcile `ManualReviewerService.cs:646` (`< 80`) and `FusionCoefficients.cs:136` (`0.70`) per the ADR the story must author.

**New tests to add:**

```
ExportGatePolicy_BoundFromConfig_OverridesDefault
```
- Arrange: `IOptions<ExportGatePolicy>` backed by a config section with `ClassificationConfidenceThreshold = 55`
- Act: inject into `ReconciliationOrchestrator`; call `ReconcileAsync` with `classifierConfidence = 60` (above the custom threshold, below the default 70)
- Assert: `ExportCompletedEvent` is published (export was NOT blocked at 60 when threshold is 55)
- Basis: inspection §4.2 — today this cannot be tested because `ExportGatePolicy` is never bound from config; the threshold is always 70.

```
ExportGatePolicy_DefaultThreshold_Is70
```
- Assert: `new ExportGatePolicy().ClassificationConfidenceThreshold.ShouldBe(70)` — the default must remain 70 so existing suites that rely on the default are not silently broken.

```
ProcessingOrchestrator_LowConfidenceThreshold_UsesConfigValue
```
- Mirrors the `ReconcileAsync` test above but targets `ProcessingOrchestrator.cs:56` (the duplicate hardcoded `70`).

**Mandatory updates to §1 guardrail tests:**

The six `ReconciliationOrchestratorExportGateTests` and `ProcessingOrchestratorTests:541` use confidence values that are meaningful only relative to a specific threshold. After 2.8 these tests must explicitly supply the threshold in each test's arrange (via `ExportGatePolicy` constructor or an injected options object) to remain deterministic. The specific values (50 / 95 / 30 / 90 / 40 / 45 vs 70) are unchanged, but they must be explicitly set, not rely on a hardcoded field default. This is a **required update** — tests that pass only because a constant defaults to 70 are latent quality risks if the default ever changes.

---

### Story 2.9 — Fix dual-scale ClassificationCompletedEvent

**Production change:** `Domain/Events/ClassificationCompletedEvent.cs:24–26,51–53` — remove the duplicate `Confidence` (`int 0–100`) + `ConfidenceScore` (`double 0–1`) dual-scale fields; replace with a single `Confidence` value object (from story 2.6) or a single `double 0–1` field. Requires coordination with any event subscriber (audit, SignalR broadcaster, test assertions).

**New tests to add:**

```
ClassificationCompletedEvent_HasSingleConfidenceField
```
- Verify (via reflection or a compilation test) that `ClassificationCompletedEvent` exposes exactly one confidence property of the agreed type.
- This is primarily a compile-time guarantee but an explicit assertion makes the intent visible.

```
ClassificationCompletedEvent_ConfidenceRoundTrips_ThroughSignalRJsonHubProtocol
```
- Mirror the existing `JsonHubProtocolCaseFilesTests` pattern (`3fd8a41c`).
- Arrange: construct a `ClassificationCompletedEvent` with a known confidence value; serialise via `JsonHubProtocol` (as SignalR would); deserialise; assert the confidence value is preserved.
- Basis: the dual-scale event was a correctness hazard; the round-trip test ensures the single-field variant survives the SignalR wire.

**Regression guardrails:**

Any test that currently reads `ClassificationCompletedEvent.Confidence` (int) or `.ConfidenceScore` (double) must be updated at migration time. Run `git grep "ClassificationCompletedEvent" -- "*.cs"` before merge to enumerate all consumers. Likely candidates: event-persistence tests, audit integration tests, and any assertion in the E2E gate on the published event.

---

## 3. The end-to-end gate — Tier-1 exit criterion

`MaxFidelityGateFullPipelineE2ETests.RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit` is the binding exit criterion for Tier 1 (stories 2.1 + 2.2). The gate does not pass until the following assertions are all green within the test's 25-minute hard cap:

| Assertion | Current state (gate log) | Required post-2.1 |
|-----------|--------------------------|-------------------|
| Stage 4 classification confidence | `10` (Aseguramiento, floor) | `≥ 70` (90 expected with body text: inspection §2.5) |
| `ExportCompletedEvent(SiroXml)` received | Never fires (gate blocks at Stage 5) | Received within 10 min |
| `ExportCompletedEvent(DatosCargaOficioXlsx)` received | Never fires | Received within 11 min |
| SIRO XML written to shared storage | File absent | File present; `<SiroResponse>` root; `<NumeroExpediente>` non-empty |
| Audit rows in SQL | Partial | Rows for all 3 processes (Orion + Athena + Reconciliator) with non-null `ProcessId` |

The gate test already carries all five of these assertions (lines 163–220 of `MaxFidelityGateFullPipelineE2ETests.cs`). No new assertion code is needed after 2.1 + 2.2 land; the gate itself is the integration test. Story 2.2 (Unknown for no-signal) will not affect this gate because the `IMSS-2023-171230` case carries the trigger phrase and will score 90, not 0.

### 3.1 Environment prerequisites

These are required for the gate to reach a green verdict. They are documented here as part of the test strategy because a missing prerequisite will produce a gate failure that looks like a code bug.

```bash
# 1. TESSDATA_PREFIX (Tesseract 5 on Ubuntu 26.04 / Linux)
export TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata

# 2. SIARA Simulator running on :5002 (HTTP, no TLS) with consistent corpus
dotnet Siara.Simulator.dll --urls http://localhost:5002

# 3. Testcontainers SQL (Docker daemon must be live)
#    OR local SQL via env var:
export PRISMA_GATE_LOCAL_SQL="Server=HOSTNAME\INSTANCE;Integrated Security=true"

# 4. Consistent 3-companion corpus case (PDF + DOCX + XML) loaded into the sim
#    The companions must agree on AreaDescripcion and NumeroExpediente to avoid
#    FusionResult.ConflictingFields (which fires Gate 3 before Stage 5 is reached).
#    Use the fast-harness known-good case or generate one via the document generator.

# 5. Playwright / Chromium (Ubuntu 26.04 — requires AppArmor unblock for userns sandbox)
sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0   # reversible

# 6. Gate run command
TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata \
  dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj" \
  --filter-query "/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"
```

The OCR-segfault (Tesseract/Leptonica coexistence with SkiaSharp/Emgu in one process — `EXECUTION-TRACKER.md` BLOCKER #1) is an orthogonal blocker tracked in `docs/planning-artifacts/remediation/TASK-OCR-SEGFAULT-LINUX.md`. Stories 2.1 and 2.2 are correct even if the gate stalls at Stage 2; the gate-green criterion above is valid only once the segfault blocker is independently resolved.

---

## 4. Risk and coverage matrix

### 4.1 Per-tier risk assessment

| Tier | Stories | Risk level | Primary risk | Mitigation |
|------|---------|------------|--------------|------------|
| 1 | 2.1, 2.2 | **Low** | The wiring change alters `ExtractedMetadata` construction at two call sites. A typing error could silently leave the field null (same as today). | The new test `ProcessingOrchestrator_Stage4_BodyTextPopulatedFromOcrResult` (§2, story 2.1) drives the full path; a null body text would fail it. |
| 1 | 2.2 | **Low** | The Unknown-return change could break a consumer that compares `ClassificationLevel1` by ordinal and did not expect `Unknown` in a previously-confident result. | `git grep "ClassificationLevel1" -- "*.cs"` before merge; update all switch/if consumers. `ReconciliationOrchestrator.cs:369` flags for review on `Confidence < 70`, not on level type — unaffected. |
| 2 | 2.3 | **Medium** | Routing Stage 4 through `ExpedienteClasifierService` or folding its boolean signals into `FileClassifierService` changes which code path is exercised. If `ExpedienteClasifierService` is routed through `SemanticAnalyzerService`, that path includes Ollama (`Infrastructure.Classification.SemanticAnalyzerService.cs`) — **do not wire the Ollama path**; the boolean check is deterministic and must remain so. | Confirm via DI wiring that `ExpedienteClasifierService.ClassifyAsync` uses the deterministic boolean gate, not `ISemanticAnalyzer.AnalyzeAsync`. The existing mutation tests for `ExpedienteClasifierService` (mutation-testing-roadmap.md Units 45–48, "mock-driven, NOT Ollama") must stay green. |
| 2 | 2.4 | **Low** | Adding keywords is additive; the only regression risk is a newly added phrase that is a substring of an unintended category (e.g., `"EMBARGO"` inside `"DESEMBARGO"`). | Run the full `FileClassifierService` mutation suite after each keyword addition. The substring-contamination lesson from mutation-testing.md §5 ("Spanish keywords embed substrings — `'ordena'` contains `'ORDEN'`") applies here. Craft inputs that isolate one term per new keyword. |
| 2 | 2.5 | **Low** | Changing the confidence calculation formula could move a document from "blocked" to "passes gate" or vice versa for edge-case inputs. | New exact-value tests in §2 story 2.5 must pin the band semantics before the change is made. Run the existing gate tests (§1.1) before and after. |
| 3 | 2.6 | **High** | The Confidence value object migration touches four result types used across all 5 stages. Scale errors (e.g., 0.70 treated as 70 after migration) would silently flip gate decisions. | **Update the §1.1 guardrail tests first, before changing production types.** The test `ReconciliationOrchestrator_MigratedConfidence_Below0Point70_BlocksExport` (story 2.6 §) must be written and green using the current `int` type, then converted to the new type — a red→green sequence confirms the migration. |
| 3 | 2.6 | **High** | The existing mutation test surface for `FileClassifierService` (89% kill rate) and `FusionExpedienteService` (Units 45–48, 0 killable survivors) must be re-run after the type migration to confirm equivalents have not grown (a scale-confusion mutant that was previously killed by an exact-value assertion might become an equivalent if the value is now on a 0–1 scale but the test compares a rounded value). | Re-run Stryker on `Infrastructure.Classification` and `Infrastructure.Extraction.Txt` after story 2.6 merges. No new killable survivors should appear. |
| 3 | 2.7 | **Medium** | If the aggregation contract adds a fourth gate (fusion confidence gate), the `FusionAutoProcess()` fixture in `ReconciliationOrchestratorExportGateTests.cs` (which sets `OverallConfidence = 0.97`) would pass the new gate, but test cases using `FusionRequiringManualReview(conflictCount: 0)` might need an explicit `OverallConfidence` value depending on the threshold chosen. | Any new gate leg must have its own test class (or at minimum new `[Fact]` methods) in `ReconciliationOrchestratorExportGateTests.cs` before the gate is added to production code. The existing six tests (§1.1) must stay green without modification. |
| 3 | 2.8 | **High** | Config binding that is silently wrong (wrong section name, wrong property mapping) results in the production threshold defaulting to 0 or 100 with no compile-time signal, which would block or pass every document. | The test `ExportGatePolicy_BoundFromConfig_OverridesDefault` (§2 story 2.8) is designed to catch this — it verifies that a config section with a custom threshold actually changes behaviour. Run this test in CI before merge. |
| 3 | 2.9 | **Low** | Removing a public event field breaks any reflection-based subscriber or SignalR client that refers to the field by name. The round-trip test (§2 story 2.9) catches this at the JSON serialisation layer. | `git grep "\.Confidence\b\|\.ConfidenceScore\b" -- "*.cs"` before merge to enumerate all consumers. |

### 4.2 Mutation-test safety net for Tier 3

The Tier-3 refactor (2.6–2.9) is the highest-risk surface in the epic. The existing mutation-hardened tests provide the backstop:

| Mutation target | Stryker coverage | Status (roadmap) | Role in Tier 3 |
|----------------|-----------------|-----------------|----------------|
| `FileClassifierService.cs` | 89%, 0 killable survivors | COMPLETE (Units 1–5) | Catches confidence-formula regressions in 2.5, 2.6 |
| `FusionExpedienteService.cs` | 0 killable survivors | COMPLETE (Unit 48) | Catches reliability threshold regressions in 2.8 |
| `ReconciliationOrchestrator.EvaluateExportGate` | Covered by `ReconciliationOrchestratorExportGateTests` (§1.1) | Integration coverage, not Stryker | Add to `stryker-config.json` for `Tests.Athena.Processing` after 2.8 merges |
| New `Confidence` value object | Not yet in scope | Add after 2.6 merges | Pure arithmetic; ideal Stryker target ≥ 90% kill rate expected |

### 4.3 What could regress — per tier

**Tier 1 (2.1–2.2):** The gate could re-block at Stage 5 if:
- The body text field is populated but under a key that `ClassifyLevel1` does not read (`combinedText` construction at `FileClassifierService.cs:78` joins `areaDescripcion + numeroExpediente + allText`; if the new field goes into a fourth variable that is not concatenated into `combinedText`, it is silently lost).
- The corpus case used in the gate has a 3-companion mismatch, causing `FusionResult.ConflictingFields` to be non-empty and Gate 3 to block before Stage 5 even tests Gate 1.

**Tier 2 (2.3–2.5):** Classifier confidence could silently regress for a document type that was *previously* correctly classified (e.g., a Desembargo case that used keyword "DESEMBARGO" in the body text could re-score incorrectly if the keyword broadening adds a pattern that matches an Aseguramiento term). The parameterised `[Theory]` in story 2.4 must cover all six Level-1 types with both the old and new phrase sets.

**Tier 3 (2.6–2.9):** The four-scale migration is the highest regression risk in the epic. The critical non-regression is that a document which *currently* passes the gate (confidence 90 > threshold 70) continues to pass after the type migration (confidence 0.90 > threshold 0.70), and a document that currently blocks (confidence 10 < threshold 70) continues to block (0.10 < 0.70). This is explicitly validated by the mandatory updates to §1.1 tests described under story 2.6.

---

## 5. Definition of done (per tier)

### Tier 1 done (stories 2.1 + 2.2)

- The §2 gate (`MaxFidelityGateFullPipelineE2ETests`) reaches a green verdict: Stage 4 classifies the tier-1 case at ≥ 70%; `ExportCompletedEvent(SiroXml)` + `ExportCompletedEvent(DatosCargaOficioXlsx)` both received; SIRO XML written to shared storage; audit rows for all 3 processes.
- `FileClassifierService_EmptyMetadata_ReturnsUnknownWithZeroConfidence` green.
- `FileClassifierService_WithBodyText_ImssCase_ScoresAseguramientoAt90` green.
- All §1.1–§1.4 guardrail tests green with no modifications.
- Solution build 0/0, 0 warnings.

### Tier 2 done (stories 2.3–2.5)

- Classifier consults `TieneAseguramiento` (and equivalent structured flags for other types); `ClassifierService_TieneAseguramientoTrue_ReturnsAseguramientoAtHighConfidence` green.
- PLD and other types verified against at least one generator canonical phrase; parameterised `[Theory]` test green.
- Confidence band semantics documented in code or an ADR; exact-value band tests pin the boundaries.
- All §1.1–§1.4 guardrail tests green without modification.

### Tier 3 done (stories 2.6–2.9)

- A single `Confidence` value object on 0–1 scale replaces the four legacy types; `Confidence_*` value-object tests green.
- `ExportGatePolicy` is bound from config at both composition roots; `ExportGatePolicy_BoundFromConfig_OverridesDefault` green.
- `ClassificationCompletedEvent` carries a single `Confidence` field; round-trip test green.
- All §1.1–§1.4 guardrail tests updated (as per §1 disposition columns) and green.
- An ADR documents the aggregation contract decision (`docs/architecture/adr/ADR-023-Unified-Confidence-Model.md`).
- Stryker re-run on `Infrastructure.Classification` shows no new killable survivors.
- Solution build 0/0, 0 warnings.

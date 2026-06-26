# ADR-023: Unified Confidence Model for the Processing Pipeline

**Date**: 2026-06-25
**Status**: Accepted
**Deciders**: Owner + Development Team
**Tags**: confidence, value-object, export-gate, classification, fusion, ocr, quality, thresholds, domain-model, tier-3
**Related**: CONFIDENCE-PIPELINE-DEEP-INSPECTION-2026-06-25.md (§3–§5, primary evidence), ADR-001 (OCR engine), ADR-012 (per-process security spine), ADR-022 (audit ledger), Epic-2 stories 2.6 / 2.7 / 2.8 / 2.9

---

## Context

### Trigger

The §2 max-fidelity gate run on 2026-06-25 (branch `Liv`) produced the failure:

> `Stage 5 BLOCKED by export gate: Classification confidence 10% is below the required threshold of 70% (BlockOnLowConfidence)`
> (`ReconciliationOrchestrator.cs:230`)

A four-track parallel code inspection (cited throughout as "the Inspection") traced this to two independent findings:

1. **Tier-1 wiring defect** — Stage 4 classification receives only two short fused fields
   (`AreaDescripcion` + `NumeroExpediente`); the 1284-char OCR body text is never passed
   to the classifier, so all keyword scores tie at the no-match floor of 10 and the
   first dictionary key (`Aseguramiento`) wins by insertion order.
   `ReconciliationOrchestrator.cs:353-356`, `FileClassifierService.cs:40-44`.

2. **Tier-3 systemic gap** — there is no coherent confidence model in the pipeline.
   Four stages emit four independent "confidences" on four different scales; no shared
   abstraction exists; and the single most load-bearing decision in the system (auto-export
   vs. hold for manual review) is made entirely from the weakest, most brittle of those
   four signals while the three richer signals are silently discarded.

This ADR records the **Tier-3 architectural decision**: the introduction of a unified
`Confidence` value object and an explicit aggregation contract. The Tier-1 wiring fix
(thread OCR body text into Stage 4) and Tier-2 classifier hardening are independently
shippable prerequisites that do not require this ADR; this ADR governs the deliberate,
test-guarded refactor that follows them.

---

### Current State: Four Scales, Zero Composition

The Inspection found the following confidence inventory across the five pipeline stages
(Inspection §3.1, all file:line citations verified 2026-06-25):

| Stage | Producer | C# type | Scale | Enters export gate? |
|-------|----------|---------|-------|---------------------|
| 1 Quality | `PolynomialImageQualityAnalyzer.cs:174-184`; `Confidence = 0.9f` hardcoded at `:129` | `ImageQualityLevel` SmartEnum + separate `float` | enum 1–5; conf constant 0–1 | **No** |
| 2 OCR | `TesseractOcrExecutor.cs:207,232-250` | `float` | **0–100** | **No** |
| 3 Fusion | `FusionExpedienteService.CalculateOverallConfidence:2477-2504` | `double` | **0–1** | **No** — `NextAction` enum enters gate 2; the 0.83 number itself does not |
| 4a Classification (gate) | `FileClassifierService.CalculateConfidence:205-236` | `int` | **0–100** | **Yes — sole gate-1 signal** |
| 4b Classification (expediente) | `ExpedienteClasifierService.cs:365-409` | `double` | **0–1** (hardcoded 0.80–0.95) | **No** — never wired into the export gate |

#### Signals discarded by the current gate (Inspection §3.2)

- **Quality never numerically combined.** `ExtractionOrchestrator.cs:403-408` sets only
  `MeanConfidence` on the extraction metadata; the `Q3_Low` enum result is never converted
  to a number and never flows downstream.
- **OCR confidence enters Fusion via a lone ad-hoc bridge only** (`confidence = ocrResult.ConfidenceAvg / 100.0`
  at `ExtractionOrchestrator.cs:377`). Meanwhile static extractors hardcode the same field
  on the 0–1 convention (`PdfMetadataExtractor.cs:623 = 0.85`, `DocxMetadataExtractor.cs:386 = 0.70`) —
  two conventions in the same field.
- **Classification ignores OCR and fusion confidence entirely** (keyword-only).
- **`FusionResult.OverallConfidence = 0.83` is source reliability,** not calibrated probability
  (`FusionExpedienteService.cs:214,245,2501`): it is `SourceReliability / maxReliability`,
  a 70/30 weighted average of per-field source-reliability fractions. Worth knowing before
  combining it with a probability-style score.
- **No "document confidence" concept exists.** The export gate evaluates three independent
  sub-gates (`ReconciliationOrchestrator.EvaluateExportGate:214-258`); the three stage
  signals that do not enter gate 1 are never aggregated.

#### Scale/semantic inconsistencies (Inspection §3.4)

1. Quality `Confidence` hardcoded `0.9f` under a "trained model" comment over stub
   coefficients (`PolynomialImageQualityAnalyzer.cs:129`).
2. OCR `0–100` (`OCRResult.cs:16`) vs fusion `0–1` (`FusionResult.cs:28`) — bridged only
   at one call site.
3. Fusion `0–1` vs classification `0–100` compared to `70` (`ReconciliationOrchestrator.cs:227,42`) —
   never on the same axis.
4. Two classification confidences on two scales: `ClassificationResult.cs:28` (int) vs
   `ExpedienteClassificationResult.cs:33` (double); gate uses the int one.
5. One domain event carries **both scales simultaneously**: `ClassificationCompletedEvent.Confidence`
   (int 0-100, init property) **and** `ClassificationCompletedEvent.ConfidenceScore` (double 0-1,
   second constructor field) — `Domain/Events/ClassificationCompletedEvent.cs:26,53`.
6. **0.70-vs-70 collision**: `FusionCoefficients.cs:136` `ManualReviewThreshold = 0.70` and
   `ReconciliationOrchestrator.cs:42` `ClassificationConfidenceThreshold = 70` express the
   same intent in two units, impossible to synchronize automatically.

#### Threshold inventory (Inspection §4)

| Location | Value | Unit | Config-bound? |
|----------|-------|------|---------------|
| `ReconciliationOrchestrator.cs:42` | 70 | int 0-100 | **No — hardcoded constant** |
| `ProcessingOrchestrator.cs:56` (duplicate) | 70 | int 0-100 | **No — duplicate constant** |
| `FusionCoefficients.cs:136` | 0.70 | double 0-1 | Bound in FusionCoefficients section |
| `FusionCoefficients.cs:129` | 0.85 | double 0-1 | Bound in FusionCoefficients section |
| `ManualReviewerService.cs:646-660` | 80 | int 0-100 | **No — stray third threshold** |
| `BulkProcessingService` | 75.0f | float 0-100 | No |

`ExportGatePolicy.cs` (`Section = "ExportGatePolicy"`) has settable toggle bools but is
**never bound** — no `Configure<ExportGatePolicy>` / `GetSection` / `.Bind` exists in
any composition root. Both production sites construct the all-ON default
(`ReconciliationOrchestrator.cs:88`, `Reconciliator Program.cs:157`, `ProcessingOrchestrator.cs:116`).
The toggles can disable gates but cannot change the `70`. (Inspection §4.2)

#### Tests that pin these values (Inspection §4.4)

The following tests must remain green through the migration and will be updated in lockstep:

- `ReconciliationOrchestratorExportGateTests.cs:149,201,251,314,361,403` — uses 50/95/30/90
  as boundary values against the 70 threshold.
- `ProcessingOrchestratorTests.cs:541` — asserts 45 triggers the manual-review flag vs 70.
- `FusionExpedienteServiceMutationTests.cs:236-281` — pins 0.85/0.70 fusion coefficients.
- `ReconciliationPipelineServiceTests.cs:199,204` — pins reason strings.

---

### Confidence Flow — Before and After

**Before (current state):**

```
Stage 1 Quality         ImageQualityLevel (enum 1–5)      ──────────────────────── DISCARDED
                        Confidence = 0.9f (hardcoded)     ────────────────────────────────────

Stage 2 OCR             float 0–100 (ConfidenceAvg)       ──── /100 bridge ──────── DISCARDED
                                                                (ExtractionOrchestrator.cs:377)

Stage 3 Fusion          double 0–1 (source reliability)   ──── NextAction enum ──── Gate 2 only
                        0.83 numeric value                 ───────────────────────── DISCARDED

Stage 4 Classification  int 0–100                         ──────────────────────────────────────
                                                                        ↓
                                                           Gate 1: int < 70 → BLOCK
                                                           (ReconciliationOrchestrator.cs:225–231)
                                                           threshold hardcoded at :42
```

**After (this decision):**

```
Stage 1 Quality         Confidence(value=float→0-1, stage=Quality, source="PolynomialModel")
                              ↓ QualityIndex flows (ExtractionOrchestrator.cs:403 wired)
Stage 2 OCR             Confidence(value=float/100, stage=Ocr, source="Tesseract")
                              ↓
Stage 3 Fusion          Confidence(value=double, stage=Fusion, source="SourceReliability")
                              ↓
Stage 4 Classification  Confidence(value=int/100→double, stage=Classification, source="Keyword")
                              ↓
                    DocumentConfidence.Aggregate(
                        classification: weight=0.50,   ← primary gate signal
                        ocr:            weight=0.30,   ← secondary
                        fusion:         weight=0.20,   ← tertiary (reliability proxy)
                        quality:        advisory=true  ← does not block; logged only
                    )
                              ↓
                    ExportGatePolicy (bound from config)
                    ClassificationConfidenceThreshold: 0.70   ← single source of truth
                    AggregateConfidenceThreshold:      0.65   ← new; skippable in MVP
```

---

## Decision

### D1 — Introduce a `Confidence` Value Object

Introduce `Confidence` as a **Domain value object** in `01 Core/Domain/ValueObjects/`.

```csharp
// Canonical definition — 0–1 scale, carries provenance
public readonly record struct Confidence
{
    public double Value { get; }          // always [0, 1]
    public PipelineStage Stage { get; }   // Quality | Ocr | Fusion | Classification | Aggregate
    public string Source { get; }         // producing type name for diagnostics

    public static Confidence FromPercent(double zeroToHundred, PipelineStage stage, string source)
        => new(zeroToHundred / 100.0, stage, source);

    public static Confidence WeightedAverage(IEnumerable<(Confidence c, double weight)> inputs)
        ...

    public static Confidence Min(Confidence a, Confidence b) ...

    // Explicit non-advisory combination for the gate
    public static Confidence Combine(
        Confidence classification,
        Confidence ocr,
        Confidence fusion,
        AggregationWeights weights) ...
}

public enum PipelineStage { Quality, Ocr, Fusion, Classification, Aggregate }
```

**Rationale:** The four bare scalars (`float`, `double`, `int`, `enum`) have no shared
type, no invariant (value ranges not enforced), and no provenance. A single value object
fixes all three and eliminates the 0.70-vs-70 collision by making the scale canonical at
construction. The `PipelineStage` provenance field allows diagnostics to show which stage
a given document confidence came from.

**Migration:** each stage result type migrates its confidence field to `Confidence` behind
a compatibility shim for the duration of the transition:

| Result type | Current field | Migration path |
|-------------|--------------|----------------|
| `OCRResult.cs:16` | `float ConfidenceAvg` (0–100) | Add `Confidence OcrConfidence`; shim `ConfidenceAvg` as `OcrConfidence.Value * 100` |
| `FusionResult.cs:28` | `double OverallConfidence` (0–1) | Add `Confidence FusionConfidence`; shim `OverallConfidence` as `FusionConfidence.Value` |
| `ClassificationResult.cs:28` | `int Confidence` (0–100) | Add `Confidence ClassificationConfidence`; shim int field as `(int)(ClassificationConfidence.Value * 100)` |
| `ImageQualityAssessment` | `float Confidence` + `ImageQualityLevel` | Add `Confidence QualityConfidence`; map enum level to numeric floor |

Shims are marked `[Obsolete]` at introduction and removed after the §4.4 tests are
updated and all callers migrated. The shim approach avoids a simultaneous breaking change
across all consumers.

**Addresses:** Inspection §3.1 (four-scale problem), §3.4 (scale/semantic inconsistencies 1–4).

---

### D2 — Explicit Aggregation Contract and Quality's Role

**What "document confidence" means:**

Document confidence is the aggregated confidence that the pipeline's output is correct and
complete enough to auto-export without manual review. It is a weighted combination of stage
signals, not their minimum, because each stage provides independent evidence.

**Aggregation weights (initial, tunable via `AggregationWeights` config):**

| Stage signal | Initial weight | Rationale |
|---|---|---|
| Classification | 0.50 | Primary functional signal; determines routing and compliance category |
| OCR | 0.30 | Document readability; a low OCR score predicts all downstream errors |
| Fusion | 0.20 | Source-reliability proxy; not a calibrated probability (Inspection §3.5) |
| Quality | advisory | Never enters the numeric gate; logged for corpus calibration (see below) |

**Quality's role — "advisory only" for MVP, path to "gating" after corpus calibration:**

Quality confidence is currently unreliable (`PolynomialImageQualityAnalyzer.cs:129`
hardcodes `0.9f`) and the training data is stub (`TrainedDate=null`, `TrainingDataSize=0`
— PRISMA-GATED-S2 is corpus-gated). Treating it as a gate blocker before calibration
would produce false positives. Decision: quality flows as an advisory field — logged in
the `DocumentConfidence` aggregate, emitted in telemetry, but the `ExportGatePolicy` only
compares the weighted aggregate of classification + OCR + fusion.

`ExtractionOrchestrator.cs:403-408` must be updated to populate `QualityIndex` on the
extraction metadata so the value is available downstream. This is the minimal wiring fix
(the field exists; it is just never set — Inspection §3.2).

After PRISMA-GATED-S2 (corpus calibration), `AggregationWeights.QualityWeight` can be
promoted from `0.0` to a non-zero value in config without a code change.

**Fusion confidence as reliability, not probability:**

`FusionResult.OverallConfidence` (`FusionExpedienteService.cs:2501`) is a 70/30
weighted average of per-field source-reliability fractions, not a calibrated probability.
It is included in the aggregate at 0.20 weight as a source-reliability signal. The field
documentation will be updated to state this explicitly, and the `Confidence` provenance
field (`source="SourceReliability"`) makes it unambiguous in diagnostics.

**Null/missing stage handling:**

If a stage produces no result (e.g., no fusion result because the document had only one
source), its weight is redistributed proportionally to the remaining stages. An all-null
aggregate defaults to `Confidence.Zero`, which always triggers gate 2 (manual review)
but never makes it to gate 1 silently.

**Addresses:** Inspection §3.2, §3.4.6, §3.5.

---

### D3 — Single Source of Truth for Thresholds

**Bind `ExportGatePolicy` properly:**

`ExportGatePolicy.cs` (`SectionName = "ExportGatePolicy"`) is never bound (Inspection §4.2).
The policy will be bound in both composition roots using:

```csharp
// Reconciliator Program.cs + ProcessingOrchestrator composition
services.Configure<ExportGatePolicy>(configuration.GetSection(ExportGatePolicy.SectionName));
```

**Promote the hardcoded `70` to config:**

Add `ClassificationConfidenceThreshold` (double 0–1, default `0.70`) to `ExportGatePolicy`.
Remove the two hardcoded `70` constants at `ReconciliationOrchestrator.cs:42` and
`ProcessingOrchestrator.cs:56`. The `ReconciliationOrchestrator.EvaluateExportGate` method
reads the threshold from the injected `IOptions<ExportGatePolicy>`.

**Add `AggregateConfidenceThreshold` to `ExportGatePolicy`:**

A second config key `AggregateConfidenceThreshold` (double 0–1, default `0.65`) enables
future gate 1b on the aggregate score without a code change. Disabled by default for
initial MVP deployment.

**Reconcile the `0.70-vs-70` collision:**

After D1 and D3, both the fusion `ManualReviewThreshold = 0.70` (`FusionCoefficients.cs:136`)
and the new classification gate `ClassificationConfidenceThreshold = 0.70` are expressed in
the same 0–1 unit. They are different semantic concepts (source-reliability minimum vs.
classification gate); keeping them as two separate config keys with coincidentally equal
defaults is intentional and must be documented.

**Retire the stray `< 80` threshold:**

`ManualReviewerService.cs:646-660` uses `< 80` as an independent low-confidence detection.
After migration this must be updated to `< ManualReviewThreshold * 100` (or the 0–1
equivalent if the field is migrated) to keep the three thresholds logically consistent.
This is a companion change to D3, not a deferred item.

**Addresses:** Inspection §4.2, §4.3, §3.4.7.

---

### D4 — Migration Strategy (keeping §4.4 tests green)

**Sequencing:**

```
Phase A — Domain VO + shims (tests still on old scalars via shims)
  1. Author Confidence VO + PipelineStage enum in 01 Core/Domain/ValueObjects/
  2. Add Confidence fields to OCRResult, FusionResult, ClassificationResult,
     ImageQualityAssessment; shim old scalar fields as [Obsolete] wrappers
  3. Add DocumentConfidence aggregate in Application layer
  4. Wire ExtractionOrchestrator.cs:403 to populate QualityIndex (advisory)
  5. Update ExportGatePolicy bindings; replace two hardcoded 70 constants
     (ReconciliationOrchestrator.cs:42 and ProcessingOrchestrator.cs:56)
  6. Build 0/0; all §4.4 tests still green (shims preserve old behavior)

Phase B — Fix ClassificationCompletedEvent dual-scale (event versioning)
  7. Collapse ClassificationCompletedEvent to carry a single Confidence field;
     remove the parallel int Confidence and double ConfidenceScore fields
     (ClassificationCompletedEvent.cs:26,53)
  8. Version the event type string (e.g., ClassificationCompletedEvent/v2) so
     any in-flight v1 events are consumed before the schema is changed;
     OutboxEvents table retains v1 rows until drained
  9. Update all event producers and consumers; update §4.4 tests that read
     Confidence or ConfidenceScore from the event directly

Phase C — Remove [Obsolete] shims
  10. After all consumers are migrated (verified by 0 CS0618 warnings),
      remove the deprecated scalar fields
  11. Run full §4.4 test suite green; build 0/0; no warnings-as-errors
```

**Compatibility shim rule:** a `[Obsolete("Use ConfidenceValue.Value. Removed in Phase C.")]`
attribute plus a `#pragma warning disable CS0618` in any test file is the explicit contract
that a given test has not yet been migrated. The CI gate `TreatWarningsAsErrors=true`
enforces that Phase C is not skippable — it will fail to build if any shim consumer
remains.

**Event versioning for ClassificationCompletedEvent:**

The dual-scale event is a domain event that is persisted in `OutboxEvents`
(`EventPersistenceWorker` subscribes to `GetAllEventsStream()`). A schema change must
not silently break in-flight rows. The versioning approach:

- Add `EventType = "ClassificationCompletedEvent/v2"` in the new single-Confidence form.
- Keep a `ClassificationCompletedEventV1` deserialization shim that reads the outbox row
  and maps `Confidence` int → `Confidence.FromPercent(...)` for any un-drained v1 rows.
- Remove the v1 shim after a configured drain window (default: 24 hours in production).

**Tests to update (Inspection §4.4):**

| Test file | Change required |
|-----------|----------------|
| `ReconciliationOrchestratorExportGateTests.cs:149,201,251,314,361,403` | Express boundary values as double (e.g., `0.50` not `50`); update factory helpers |
| `ProcessingOrchestratorTests.cs:541` | Update `45` → `0.45` in `ClassificationResult` builder |
| `FusionExpedienteServiceMutationTests.cs:236-281` | No change — already uses 0–1 for fusion |
| `ReconciliationPipelineServiceTests.cs:199,204` | Update reason-string assertions if threshold text changes |

All four test files must be updated in **Phase A** (not Phase C) so that the §4.4 suite
acts as a continuous regression signal through the migration.

**Addresses:** Inspection §4.4, §3.4.6.

---

### D5 — Mapping to Epic-2 Stories

The Tier-3 work is decomposed into four independently-shippable stories. The story IDs
below are canonical backlog identifiers; each story corresponds to one migration phase.

| Story | Title | Phase | Addresses |
|-------|-------|-------|-----------|
| **2.6** | Introduce `Confidence` VO and per-stage shims | Phase A (steps 1–3) | D1, §3.1, §3.4 |
| **2.7** | Bind `ExportGatePolicy` + promote thresholds to config; wire quality advisory flow | Phase A (steps 4–6) | D2, D3, §4.2, §3.2 |
| **2.8** | Fix `ClassificationCompletedEvent` dual-scale; event versioning; Phase B | Phase B (steps 7–9) | D4, §3.4.6 |
| **2.9** | Remove `[Obsolete]` shims; final migration sweep; Phase C test update | Phase C (steps 10–11) | D4 cleanup, §4.4 |

Story 2.6 is the only structural prerequisite; stories 2.7, 2.8, and 2.9 may proceed
in parallel once 2.6 is merged. Story 2.9 is gated on 2.6 + 2.8 (shim removal requires
all consumers migrated including the event).

Stories 2.6–2.9 are **independently shippable** and do **not** depend on or block
Tier-1 (classifier body-text wiring) or Tier-2 (keyword-table broadening). The §2 gate
can be made green via Tier-1 alone before any of these stories begin.

---

## Alternatives Considered

### A1 — Keep the four bare scalars; add only the threshold config binding

**Rejected.** Binding `ExportGatePolicy` alone (D3 alone, without D1 or D2) fixes the
config-unbound defect but does not eliminate the 0.70-vs-70 collision, does not allow
OCR or fusion confidence to influence the gate, and does not fix the dual-scale event.
The systemic gap remains and is deferred to a later date where it will be harder to
refactor.

### A2 — Use `double` everywhere immediately (no VO)

**Rejected.** A raw `double` has no provenance, no invariant enforcement, and no stage
identity. The 0.70-vs-70 collision recurs as soon as a second developer adds a `double`
threshold. A value object adds less than one file of code and permanently closes the
collision surface. The migration cost is identical.

### A3 — Let Quality enter the gate at equal weight with Classification

**Rejected for MVP.** `PolynomialImageQualityAnalyzer.cs:129` hardcodes `Confidence = 0.9f`
with stub coefficients; using this as a gate signal at any non-zero weight will produce
systematically overconfident quality scores and mask real document problems. Quality enters
as advisory-only until PRISMA-GATED-S2 (corpus calibration) completes. The `AggregationWeights`
config key makes the promotion a zero-code-change operation.

### A4 — Migrate ClassificationCompletedEvent first (reverse Phase order)

**Rejected.** The event dual-scale defect is a producer-consumer coordination problem.
Fixing the event before the VO exists forces the event to carry a raw `double` that is
indistinguishable from the old `ConfidenceScore` double. The shim approach (Phase A first)
ensures that by the time Phase B runs, the canonical scale is already defined and the
event migration is a one-step replacement rather than a two-step rename.

---

## Consequences

### Positive

- The 0.70-vs-70 collision is permanently eliminated. Both thresholds live in the same
  unit (`double 0–1`) and in a bound config class; a misconfiguration is immediately
  visible in the config diff.
- The export gate can now consider OCR and fusion evidence (at configurable weights)
  without a code change — config only.
- Quality confidence flows end-to-end (advisory) even before corpus calibration; the
  telemetry is available for the calibration step (PRISMA-GATED-S2).
- `ClassificationCompletedEvent` carries a single unambiguous confidence value; the
  dual-scale inconsistency that confused any consumer reading `Confidence` vs
  `ConfidenceScore` is eliminated.
- The `ExportGatePolicy` toggle bools (`BlockOnLowConfidence`, `BlockOnFusionManualReviewRequired`,
  `BlockOnUnresolvedConflicts`) are now actually bindable from config — the feature was
  wired but dead since the class was never bound.
- The `[Obsolete]` + `TreatWarningsAsErrors=true` enforcement guarantees Phase C is not
  skipped accidentally; the build will fail until shims are removed.

### Negative / Trade-offs

- **Temporary compilation noise.** During Phase A, all callers of the old scalar fields
  receive `CS0618` obsolete warnings. These are suppressed per file with `#pragma warning
  disable` in test files, which adds mechanical overhead but is the correct pattern.
- **Event versioning overhead.** The `ClassificationCompletedEventV1` shim adds a short-
  lived deserialization path that must be removed after the drain window. A missed removal
  leaves dead code; the drain window must be monitored.
- **Aggregation weights are initial estimates.** The 0.50 / 0.30 / 0.20 weights for
  classification / OCR / fusion are engineering estimates, not corpus-derived. They should
  be re-evaluated after PRISMA-GATED-S2.
- **Fusion weight interpretation is approximate.** Fusion confidence is source reliability,
  not calibrated probability (Inspection §3.5). Treating it as a 0.20-weight confidence
  signal is a pragmatic approximation until a probability-calibrated fusion scorer is built.
  The `source="SourceReliability"` provenance field makes this visible in diagnostics.

### No Impact On

- The Tier-1 fix (body text wiring at `ProcessingOrchestrator.cs:530` and
  `ReconciliationOrchestrator.cs:353-356`). That fix is merge-independent and should land
  before this ADR's Phase A begins.
- The Tier-2 classifier hardening (stories TBD). Keyword tables and the
  `ExpedienteClasifierService` routing decision are classification-internal and do not
  depend on the `Confidence` VO.
- The OCR engine selection (ADR-001). Tesseract remains the engine of record; the VO
  wraps its float output; no OCR behavior changes.
- The 3-process security spine (ADR-012). Process identity and clearance tokens are
  independent of confidence scoring.

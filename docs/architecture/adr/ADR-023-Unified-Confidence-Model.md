# ADR-023: Unified Confidence Model for the Processing Pipeline

**Date**: 2026-06-25
**Status**: Accepted
**Revised**: 2026-06-26 — reconciled against shipped implementation (stories 2.6–2.9, branch `Liv`); all five decision sections updated to match code. Original draft described `PipelineStage`, shim migration, event versioning, and a disabled aggregate gate; the shipped implementation uses `ConfidenceSource`, direct migration, plain event replacement, and an aggregate gate enabled by default at 0.65 (owner-ruled 2026-06-26).
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

**After (shipped — stories 2.6–2.9, branch `Liv`):**

```
Stage 1 Quality         Confidence(Value=double 0-1, Source=ConfidenceSource.Quality)
                              ↓ QualityIndex advisory — weight 0.0; NOT populated (stub
                                coefficients, PRISMA-GATED-S2). Promotion = config change only.

Stage 2 OCR             Confidence(Value=float/100, Source=ConfidenceSource.Ocr)
                              ↓ Confidence.FromOcr(float zeroTo100) — Confidence.cs:70
                                Carried to Reconciliator via Expediente.OcrConfidence
                                (ExtractionPipelineService.cs:212); gate reads
                                ocrResult?.Confidence ?? fusionResult?.FusedExpediente?.OcrConfidence
                                (ReconciliationOrchestrator.cs:159)

Stage 3 Fusion          Confidence(Value=double 0-1, Source=ConfidenceSource.Fusion)
                              ↓ Confidence.FromFusion(double) — Confidence.cs:94
                                (source-reliability proxy; see D2 rationale)

Stage 4 Classification  Confidence(Value=int/100→double, Source=ConfidenceSource.Classification)
                              ↓ Confidence.FromInt(int zeroTo100) — Confidence.cs:82

                    ┌── Gate 1: classification.Confidence.Value < ClassificationConfidenceThreshold (0.70)
                    │         ReconciliationOrchestrator.EvaluateExportGate:228-237
                    │
                    └── Gate 1b: Confidence.WeightedAverage(weighted, ConfidenceSource.Aggregate)
                                   Classification weight=0.50, Ocr weight=0.30, Fusion weight=0.20
                                   Quality weight=0.0 (advisory; not added to weighted list)
                                   Missing signal → weight redistributed proportionally
                                   aggregate.Value < AggregateConfidenceThreshold (0.65)
                                 ReconciliationOrchestrator.EvaluateExportGate:239-266

                    ExportGatePolicy (Application layer, 01 Core/Application/ExportGatePolicy.cs)
                    ClassificationConfidenceThreshold: 0.70   ← gate 1 (BlockOnLowConfidence)
                    AggregateConfidenceThreshold:      0.65   ← gate 1b (BlockOnLowAggregateConfidence)
                    Both gates ENABLED by default (owner-ruled 2026-06-26)
```

---

## Decision

### D1 — Introduce a `Confidence` Value Object

**SHIPPED in Story 2.6.** `Confidence` is a **Domain value object** in
`01 Core/Domain/ValueObjects/Confidence.cs` and `ConfidenceSource.cs`.

```csharp
// Shipped canonical definition — 01 Core/Domain/ValueObjects/Confidence.cs
public readonly record struct Confidence
{
    public double Value { get; init; }        // always [0, 1] — clamped in constructor
    public ConfidenceSource Source { get; init; } // provenance enum (see below)

    public Confidence(double value, ConfidenceSource source) { Value = Math.Clamp(value, 0.0, 1.0); ... }

    // Named factories — preferred API (no implicit numeric conversion operators by design)
    public static Confidence FromOcr(float zeroTo100)       // ÷100; Source=Ocr      :70
    public static Confidence FromInt(int zeroTo100)         // ÷100; Source=Classification :82
    public static Confidence FromFusion(double zeroTo1)     // Source=Fusion         :94
    public static Confidence FromQuality(double zeroTo1)    // Source=Quality        :106

    // Combination helpers
    public static Confidence Min(IEnumerable<Confidence> sources)  // returns lowest :121
    public static Confidence WeightedAverage(
        IEnumerable<(Confidence Confidence, double Weight)> weighted,
        ConfidenceSource resultSource)                             // Σ(v×w)/Σw     :152
    public static Confidence Combine(IEnumerable<Confidence> sources) // delegates to Min :178
}

// 01 Core/Domain/ValueObjects/ConfidenceSource.cs
public enum ConfidenceSource { Quality, Ocr, Fusion, Classification, Aggregate }
```

There are **no** implicit numeric conversion operators (intentional — a bare `float` could
be either 0–1 or 0–100). `Combine` is conservative (Min); the gate uses `WeightedAverage`
directly at the call site in `ReconciliationOrchestrator.EvaluateExportGate:257`.

**Rationale:** The four bare scalars (`float`, `double`, `int`, `enum`) have no shared
type, no invariant (value ranges not enforced), and no provenance. A single value object
fixes all three and eliminates the 0.70-vs-70 collision by making the scale canonical at
construction. The `ConfidenceSource` provenance field allows diagnostics (and the gate
reason strings) to show which stage a given confidence came from.

**Migration — direct replacement (no shims):** All four result types were migrated
directly to the VO in one compiling change (Story 2.6). The `[Obsolete]` shim approach
was considered (see D4 note) but not used — every downstream copy-site was identified and
rescaled at the copy point in the same commit to preserve persisted numeric values.

| Result type | Old field | Shipped field | Factory used |
|-------------|-----------|---------------|--------------|
| `OCRResult.cs:18` | `float ConfidenceAvg` (0–100) | `Confidence Confidence` | `Confidence.FromOcr(confidenceAvg)` |
| `FusionResult.cs:29` | `double OverallConfidence` (0–1) | `Confidence Confidence` | `Confidence.FromFusion(overallConfidence)` |
| `ClassificationResult.cs:30` | `int Confidence` (0–100) | `Confidence Confidence` | `Confidence.FromInt(score)` |
| `ImageQualityAssessment.cs:19` | `float Confidence` (0–1) | `Confidence Confidence` | `new Confidence(value, ConfidenceSource.Quality)` |

**Addresses:** Inspection §3.1 (four-scale problem), §3.4 (scale/semantic inconsistencies 1–4).

---

### D2 — Explicit Aggregation Contract and Quality's Role

**SHIPPED in Story 2.7.** The weighted document-confidence aggregate and gate 1b are
implemented in `ReconciliationOrchestrator.EvaluateExportGate` (lines 239–266) and
`ExportGatePolicy` / `AggregationWeights` (`01 Core/Application/ExportGatePolicy.cs`).

**What "document confidence" means:**

Document confidence is the aggregated confidence that the pipeline's output is correct and
complete enough to auto-export without manual review. It is a weighted combination of stage
signals, not their minimum, because each stage provides independent evidence.

**Aggregation weights (tunable via `AggregationWeights` in `ExportGatePolicy` config):**

| Stage signal | Default weight | Rationale |
|---|---|---|
| Classification | 0.50 | Primary functional signal; determines routing and compliance category |
| OCR | 0.30 | Document readability; a low OCR score predicts all downstream errors |
| Fusion | 0.20 | Source-reliability proxy; not a calibrated probability (Inspection §3.5) |
| Quality | **0.0** | Advisory; weight 0 means not added to the weighted list. See below. |

`AggregationWeights` defaults: `ExportGatePolicy.cs:122–131`.
`BlockOnLowAggregateConfidence = true` (default): `ExportGatePolicy.cs:94`.
`AggregateConfidenceThreshold = 0.65` (default, **enabled by default — owner-ruled 2026-06-26**): `ExportGatePolicy.cs:105`.

**OCR confidence cross-boundary threading (Story 2.7):**

In the 3-process split the raw `OCRResult` does not cross the Extractor→Reconciliator
boundary. Story 2.7 threads the OCR confidence via a new `Expediente.OcrConfidence`
field (`Domain/Entities/Expediente.cs:135`). `ExtractionPipelineService.cs:212` assigns
`expediente.OcrConfidence = extraction.OcrResult?.Confidence` before `SaveAsync`.
`ReconciliationOrchestrator.cs:159` reads the signal as
`ocrResult?.Confidence ?? fusionResult?.FusedExpediente?.OcrConfidence` — the live
(monolith-path) value takes precedence, the carried field is the 3-process fallback.

**Quality's role — weight 0.0 until corpus calibration:**

Quality confidence is currently unreliable (`PolynomialImageQualityAnalyzer.cs:129`
hardcodes `0.9f`) and training data is stub (`TrainedDate=null`, `TrainingDataSize=0`
— PRISMA-GATED-S2 is corpus-gated). `ExtractionOrchestrator` carries an explicit comment
that `QualityIndex` is deliberately not populated (stub-trained coefficients; would also
perturb fusion reliability) until corpus calibration. Weight 0.0 means the quality
signal is not added to the weighted list and does not enter the aggregate at all.
Promotion to a non-zero weight after PRISMA-GATED-S2 is a config-only change to
`AggregationWeights.Quality` without a code change.

**Fusion confidence as reliability, not probability:**

`FusionResult.Confidence` (`FusionExpedienteService.cs:148`, via `Confidence.FromFusion`)
is a 70/30 weighted average of per-field source-reliability fractions
(`FusionExpedienteService.CalculateOverallConfidence:2477-2504`), not a calibrated
probability. It is included in the aggregate at 0.20 weight as a source-reliability
signal. The `ConfidenceSource.Fusion` provenance tag makes this unambiguous in diagnostics.

**Known limitation — `AutoridadNombre` structural correlation:**

The Tier-1 corpus sets `AutoridadNombre` to the constant string
"Comisión Nacional Bancaria y de Valores" — the value that the Txt and Docx adaptive
extractors always produce for CNBV oficios. This means that field's contribution to the
fusion confidence score is structural (always present, always consistent) rather than
independent evidence. It is a known limitation to revisit when fusion confidence is
calibrated against a broader corpus; this is part of why Fusion carries only 0.20 weight
and Quality 0.0 in the initial configuration.

**Null/missing stage handling:**

`Confidence.WeightedAverage` divides by the sum of the weights of the signals actually
present (`Confidence.cs:165`), so a missing signal's weight is redistributed
proportionally to the remaining signals. An empty weighted list (all stages null) skips
gate 1b entirely (guarded at `ReconciliationOrchestrator.cs:255`); gates 2 and 3 still
evaluate independently.

**Addresses:** Inspection §3.2, §3.4.6, §3.5.

---

### D3 — Single Source of Truth for Thresholds

**SHIPPED in Story 2.8.**

**`ExportGatePolicy` binding:**

`ExportGatePolicy` (`SectionName = "ExportGatePolicy"`, `01 Core/Application/ExportGatePolicy.cs`)
is now bound in both composition roots:

```csharp
services.Configure<ExportGatePolicy>(configuration.GetSection(ExportGatePolicy.SectionName));
```

The `ReconciliationOrchestrator` receives the bound policy via constructor injection and
falls back to `new ExportGatePolicy()` (all defaults on) when `null` is passed (useful in
tests without DI).

**Hardcoded `70` promoted to config:**

`ClassificationConfidenceThreshold` (double 0–1, default `0.70`) was added to
`ExportGatePolicy` (`:71`). The two hardcoded `70` constants at the original
`ReconciliationOrchestrator.cs:42` and `ProcessingOrchestrator.cs:56` are removed;
`EvaluateExportGate` compares `classificationResult.Confidence.Value <
_exportGatePolicy.ClassificationConfidenceThreshold` (`:230`).

**`AggregateConfidenceThreshold` — ENABLED by default at 0.65 (owner-ruled 2026-06-26):**

`AggregateConfidenceThreshold` (double 0–1, `ExportGatePolicy.cs:105`) and
`BlockOnLowAggregateConfidence` (bool, `ExportGatePolicy.cs:94`) are both defaulted to
`true` / `0.65`. The gate is active out of the box; set `BlockOnLowAggregateConfidence:
false` in `appsettings.json` to fall back to classification-only gating.

**`0.70-vs-70` collision resolved:**

`FusionCoefficients.ManualReviewThreshold = 0.70` (`FusionCoefficients.cs:136`) and
`ExportGatePolicy.ClassificationConfidenceThreshold = 0.70` are both on the 0–1 scale
post-Story 2.6. They are different semantic concepts (source-reliability minimum vs.
classification gate); equal defaults are intentional and documented.

**`ManualReviewThreshold` in `ExportGatePolicy`:**

A `ManualReviewThreshold` (double 0–1, default `0.80`, `ExportGatePolicy.cs:85`) is also
present in `ExportGatePolicy`; `ManualReviewerService` reads this on the 0–1 scale
(replacing the former hardcoded `< 80` at `ManualReviewerService.cs:646-660`).

**Addresses:** Inspection §4.2, §4.3, §3.4.7.

---

### D4 — Migration Strategy (as executed)

**The `[Obsolete]` shim approach was considered but not used.** After surveying all
consumers of the four old scalar fields, the team determined that a direct single-commit
migration was feasible: all copy-sites were identified and the rescale (÷100 or direct)
was applied at each site in the same change. This avoided the CS0618 noise phase entirely.
`TreatWarningsAsErrors=true` confirms zero shim survivors: the build is clean.

**Executed sequence (Stories 2.8 → 2.6 → 2.7 → 2.9, branch `Liv`):**

```
Story 2.8 — ExportGatePolicy binding + threshold consolidation
  1. Bind ExportGatePolicy in both composition roots.
  2. Add ClassificationConfidenceThreshold (double 0–1, default 0.70) to ExportGatePolicy;
     remove hardcoded 70 at ReconciliationOrchestrator.cs:42 + ProcessingOrchestrator.cs:56.
  3. Update §4.4 test boundary values to double (0.50, 0.45, etc.).
  4. Build 0/0; §4.4 suite green.

Story 2.6 — Confidence VO + direct four-type migration (no shims)
  5. Author Confidence VO + ConfidenceSource enum in 01 Core/Domain/ValueObjects/.
  6. Replace old scalar fields in OCRResult, FusionResult, ClassificationResult,
     ImageQualityAssessment with Confidence directly; rescale at each copy-site.
     MAJOR: also deduplicated the SolicitudSiara confidence value assignment in
     FusionExpedienteService.CalculateOverallConfidence (was double-counted).
  7. Build 0/0; §4.4 suite green.

Story 2.7 — Aggregation contract + OCR confidence cross-boundary threading
  8. Add BlockOnLowAggregateConfidence + AggregateConfidenceThreshold (0.65, default ON)
     + AggregationWeights to ExportGatePolicy.
  9. Add Expediente.OcrConfidence field; wire in ExtractionPipelineService.cs:212.
  10. Implement gate 1b in ReconciliationOrchestrator.EvaluateExportGate:239-266.
  11. Build 0/0; §4.4 suite green.

Story 2.9 — Fix ClassificationCompletedEvent dual-scale
  12. Replace int Confidence + double ConfidenceScore with single Confidence Confidence
      (Domain/Events/ClassificationCompletedEvent.cs:29). EventType stays as
      nameof(ClassificationCompletedEvent) — no v2 suffix (see event note below).
  13. Update all producers and consumers. Build 0/0; §4.4 suite green.
```

**ClassificationCompletedEvent — direct replacement, no versioning:**

The event was updated by replacing the two old fields with a single `Confidence Confidence`
field in one commit (Story 2.9). No `ClassificationCompletedEvent/v2` event type, no
`ClassificationCompletedEventV1` deserialization shim, and no drain window were used.

The persisted JSON shape changed from `{"Confidence":95,"ConfidenceScore":0.95,...}` to
`{"Confidence":{"Value":0.95,"Source":"Classification"},...}`. Pre-existing outbox rows
with the old shape are not migrated (no converter registered). This is a deploy
consideration: on a fresh database there are no stale rows. On an existing deployment,
un-drained rows with the old shape will fail deserialization when consumed — acceptable
because the outbox rows in question pre-date the VO migration and carry no information not
already captured in the pipeline audit log.

**Tests updated (Inspection §4.4):**

| Test file | Change applied |
|-----------|----------------|
| `ReconciliationOrchestratorExportGateTests.cs:149,201,251,314,361,403` | Boundary values expressed as double (0.50, 0.95, 0.30, 0.90); factory helpers updated |
| `ProcessingOrchestratorTests.cs:541` | `45` → `0.45` in `ClassificationResult` builder |
| `FusionExpedienteServiceMutationTests.cs:236-281` | No change — already used 0–1 for fusion |
| `ReconciliationPipelineServiceTests.cs:199,204` | Reason-string assertions updated for new threshold-percent text |

**Addresses:** Inspection §4.4, §3.4.6.

---

### D5 — Mapping to Epic-2 Stories

All four stories are **DONE** on branch `Liv`. Canonical order and mapping per
`docs/planning-artifacts/remediation/EPIC-2-CONFIDENCE-SPRINT-PLAN.md` Sprint 3:

| Story | Title | Status | Addresses |
|-------|-------|--------|-----------|
| **2.8** | Bind `ExportGatePolicy` + consolidate thresholds to config | DONE | D3, §4.2, §4.3 |
| **2.6** | Introduce `Confidence` VO + direct migration of 4 result types (incl. `SolicitudSiara` confidence dedup in `FusionExpedienteService.CalculateOverallConfidence`) | DONE | D1, §3.1, §3.4 |
| **2.7** | Aggregation contract — weighted gate 1b; OCR confidence cross-boundary threading via `Expediente.OcrConfidence` | DONE | D2, §3.2 |
| **2.9** | Fix `ClassificationCompletedEvent` dual-scale — replace `int Confidence` + `double ConfidenceScore` with single `Confidence Confidence` | DONE | D4, §3.4.6 |

**Execution order within Sprint 3:** 2.8 first (lowest blast radius — pure config binding,
no domain change), then 2.6 (the VO — depends on a clean single-threshold baseline), then
2.7 (aggregation — needs the VO), then 2.9 (dual-scale event fix — consumes the VO).
Stories 2.6 and 2.8 were independent and ran in parallel.

Stories 2.6–2.9 are **independent of** Tier-1 (classifier body-text wiring) and Tier-2
(keyword-table broadening). The §2 gate was made green via Tier-1 alone (Story 2.1)
before Sprint 3 began.

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
- The export gate now considers OCR and fusion evidence via gate 1b (weighted aggregate)
  and is ENABLED by default at 0.65 — no code change required, config-tunable.
- `ClassificationCompletedEvent` carries a single unambiguous `Confidence` value object;
  the dual-scale inconsistency (`Confidence` int vs `ConfidenceScore` double) is eliminated.
- The `ExportGatePolicy` toggle bools (`BlockOnLowConfidence`, `BlockOnFusionManualReviewRequired`,
  `BlockOnUnresolvedConflicts`, `BlockOnLowAggregateConfidence`) are now bound from config.
- No `[Obsolete]` shim phase was needed: the direct migration kept the build at 0 warnings
  throughout; `TreatWarningsAsErrors=true` confirms no stale consumers exist.
- OCR confidence now reaches the Reconciliator gate across both the monolith and 3-process
  paths (via `Expediente.OcrConfidence`), closing the signal gap identified in Inspection §3.2.

### Negative / Trade-offs

- **Event JSON shape break.** Replacing the two raw numeric fields with a structured
  `Confidence` VO changes the persisted outbox JSON. Pre-existing un-drained outbox rows
  with the old `"Confidence":95` shape will fail deserialization on consumption (no
  converter is registered). On a fresh database this is moot; on an existing deployment
  see the D4 deploy consideration.
- **Aggregation weights are initial estimates.** The 0.50 / 0.30 / 0.20 weights for
  classification / OCR / fusion are engineering estimates, not corpus-derived. They should
  be re-evaluated after PRISMA-GATED-S2.
- **Fusion weight interpretation is approximate.** Fusion confidence is source reliability,
  not calibrated probability (Inspection §3.5). Treating it as a 0.20-weight confidence
  signal is a pragmatic approximation until a probability-calibrated fusion scorer is built.
  The `ConfidenceSource.Fusion` provenance tag makes this visible in diagnostics.
- **`AutoridadNombre` structural correlation in Tier-1 corpus.** See D2 "Known limitation"
  — this is part of the rationale for the conservative Fusion weight (0.20) and Quality
  weight (0.0) in the initial configuration.

### No Impact On

- The Tier-1 fix (body text wiring at `ProcessingOrchestrator.cs:530` and
  `ReconciliationOrchestrator.cs:353-356`). That fix landed before Sprint 3 began (Story 2.1,
  branch `Liv`).
- The Tier-2 classifier hardening (stories TBD). Keyword tables and the
  `ExpedienteClasifierService` routing decision are classification-internal and do not
  depend on the `Confidence` VO.
- The OCR engine selection (ADR-001). Tesseract remains the engine of record; the VO
  wraps its float output; no OCR behavior changes.
- The 3-process security spine (ADR-012). Process identity and clearance tokens are
  independent of confidence scoring.

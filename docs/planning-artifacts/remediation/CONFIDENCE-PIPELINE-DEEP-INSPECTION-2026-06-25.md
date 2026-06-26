# Deep Inspection — The Confidence Pipeline (classification → export gate) and its hardening / re-architecture

**Authored:** 2026-06-25 · **Branch:** `Liv` · **Status:** OPEN — this is **Blocker 3** of the §2 max-fidelity gate (the export hold that remains after the OCR-segfault and corpus-consistency fixes landed, `ea90aa17` + `3c7532a2`).
**Type:** investigation + architecture decision. Every claim below is grounded in code (file:line) gathered by a four-track parallel inspection of the running gate's exact failure.
**Trigger:** `Stage 5 BLOCKED by export gate ...: Classification confidence 10% is below the required threshold of 70% (BlockOnLowConfidence)` (`ReconciliationOrchestrator.cs:230`).

---

## 0. Executive verdict (read this first)

Two findings, one shallow and one deep. **Both are evidence-proven; neither is what the original Blocker-3 note guessed** (it guessed "synthetic docs lack legal phrasing / classifier needs tuning" — that is **refuted**).

1. **The immediate cause is a WIRING DEFECT, not a corpus gap and not a calibration problem.** Stage 4 classification is fed only two short fused fields (`AreaDescripcion` + `NumeroExpediente`); the **1284-char OCR body text is never passed to the classifier**, and `LegalReferences` is never populated. The synthetic document *does* contain the trigger word `ASEGURAMIENTO` 4+ times in its body — if that text reached the classifier the case would score **~90%**, not 10%. The "10% / Aseguramiento" output is a deterministic artifact of *all categories tied at the no-match floor* plus a dictionary-insertion-order tie-break — a **"right label for the wrong reason."**

2. **The deep cause is that there is no coherent confidence model.** Four stages emit four "confidences" on four different scales (`enum` quality, `float 0–100` OCR, `double 0–1` fusion, `int 0–100` classification), there is **no shared `Confidence` abstraction**, the numbers are **never composed** into one document confidence, and the single most load-bearing decision in the system — *hold for manual review vs. auto-export* — hinges entirely on the **weakest, most brittle** of the four (a keyword `int`) while the three richer signals (OCR 95.71%, fusion 0.83, quality `Q3_Low`) **do not enter the comparison at all**.

**Recommendation:** ship the one-line wiring fix to unblock the gate **now** (Tier 1), then harden the classifier (Tier 2), then do the grounded re-architecture of the confidence model (Tier 3). Tiers are independently shippable; Tier 1 alone turns the gate green.

---

## 1. The exact failure, reconstructed from the gate run

From the live §2 gate (case `IMSS-2023-171230`, tier-1 synthetic):

| Stage | Output (gate log) | Scale | Entered the export-gate decision? |
|------|-------------------|-------|-----------------------------------|
| 1 Quality | `Q3_Low` | enum (1–5) | **No** — quality never gates export |
| 2 OCR | `95.71%` (1284 chars) | float 0–100 | **No** |
| 3 Fusion | `0.83, Conflicts: 0, NextAction: Revisar recomendado` | double 0–1 | NextAction only (→ gate 2); the 0.83 number itself: **No** |
| 4 Classification | `Type: Aseguramiento, Confidence: 10` | int 0–100 | **Yes — this one value decided the hold** |
| 5 Export | `BLOCKED ... 10% < 70% (BlockOnLowConfidence)` | — | gate 1 fired |

The decisive line: `ReconciliationOrchestrator.cs:225-231`, threshold `ClassificationConfidenceThreshold = 70` (`ReconciliationOrchestrator.cs:42`).

---

## 2. Root cause of the 10% — the wiring defect (Tier-1 evidence)

### 2.1 What the classifier reads
`FileClassifierService` (the `IFileClassifier` wired into Stage 4 — `Reconciliator Program.cs:151`, `Athena Program.cs:163`) classifies over **only**:
`combinedText = (AreaDescripcion + " " + NumeroExpediente + " " + string.Join(" ", LegalReferences)).ToUpperInvariant()` — `FileClassifierService.cs:40-44, 78`.

### 2.2 What Stage 4 actually passes it
`ProcessingOrchestrator.ClassifyDocumentRopAsync:530-535` (and the same shape at `ReconciliationOrchestrator.cs:353-356`):
```csharp
var metadata = new ExtractedMetadata { Expediente = ctx.FusionResult?.FusedExpediente };
var classResult = await _classifier.ClassifyAsync(metadata, cancellationToken);
```
`metadata.LegalReferences` is **never set** (→ empty) and **the OCR body text is never attached.** The orchestrator even admits it: `ReconciliationOrchestrator.cs:96` — *"The OCR result from the Extractor (currently unused by Stage 4…)."*

For `IMSS-2023-171230`, `combinedText` was literally `"FISCALÍA ESPECIALIZADA EN MATERIA DE DERECHOS HUMANOS EXP-8217-2021 "` — which contains **none** of the Level-1 keywords.

### 2.3 The body text the classifier never saw DOES contain the signal
Generator `core/legal_catalog.py:190-194, 222-226` emits, for `aseguramiento`: *"se solicita el **aseguramiento precautorio** de las cuentas bancarias"*, *"Proceder al **aseguramiento** inmediato"*, *"Notificar el monto total **asegurado**"*. Confirmed present in the actual rendered DOCX `word/document.xml` of the case, and the XML companion carries `<TieneAseguramiento>true</TieneAseguramiento>`. **The corpus is rich with the exact trigger words.**

### 2.4 The arithmetic of "10 / Aseguramiento" (so it is not mistaken for a calibrated score)
Keyword scoring assigns each of 6 categories `90` (strong hit), `70` (weak), or `10` (no hit) — `FileClassifierService.cs:85/89/93` etc. With no keyword present, **all six scores = 10**. Then `CalculateConfidence` (`FileClassifierService.cs:205-236`):
- `maxScore=10`, `averageScore=10`, `scoreDifference = 10 − (empty set→0) = 10`
- `10 < 40` → `Math.Min(70, averageScore)` = **10**.

And `DetermineLevel1Category` (`:238-251`) does `OrderByDescending(value).First()` over a dictionary whose **first inserted key is `Aseguramiento`** → on an all-tie it returns Aseguramiento. **So any no-signal document is labeled `(Aseguramiento, 10)`.** Confidence 10 means *zero keywords matched*, not "weak aseguramiento evidence."

### 2.5 The minimal fix (turns the gate green)
Thread the document body text into the classifier input (populate `metadata.LegalReferences`, or a new body-text field, with the Stage-2 OCR text and/or the fused-source text) at `ProcessingOrchestrator.cs:530` / `ReconciliationOrchestrator.cs:353`. With the body text present, the Aseguramiento branch hits score 90 → `scoreDifference 80 ≥ 60` → confidence **90%** → gate passes. **One wiring change, no generator change, no threshold change.**

---

## 3. The systemic finding — there is no unified confidence model (Tier-3 evidence)

### 3.1 Four stages, four scales, zero composition

| Stage | Producer (file:line) | Type | Scale |
|-------|----------------------|------|-------|
| 1 Quality | `PolynomialImageQualityAnalyzer.cs:174-184`; `Confidence = 0.9f` **hardcoded** `:129` | `ImageQualityLevel` SmartEnum + separate `float` | enum 1–5; conf 0–1 (but constant) |
| 2 OCR | `TesseractOcrExecutor.cs:207,232-250` | `float` | **0–100** |
| 3 Fusion | `FusionExpedienteService.CalculateOverallConfidence:2477-2504` | `double` | **0–1** |
| 4 Classification (gate uses this) | `FileClassifierService.CalculateConfidence:205-236` | `int` | **0–100** |
| 4′ Classification (Expediente-level, **not** gated) | `ExpedienteClasifierService.cs:365-409` | `double` | **0–1** (hardcoded 0.80–0.95) |

### 3.2 Confidences do not propagate or aggregate
- **Quality → dropped.** Fusion *can* read `ExtractionMetadata.QualityIndex` (`FusionExpedienteService.cs:301-304`) but the worker path **never sets it** (`ExtractionOrchestrator.cs:403-408` sets only `MeanConfidence`); the `Q3_Low` enum is never turned into a number.
- **OCR → Fusion** only via a lone ad-hoc bridge `confidence = ocrResult.ConfidenceAvg / 100.0` (`ExtractionOrchestrator.cs:377`). Meanwhile static metadata extractors hardcode the same field on the 0–1 convention (`PdfMetadataExtractor.cs:623 =0.85`, `DocxMetadataExtractor.cs:386 =0.70`) — **same field, two conventions.**
- **Classification ignores OCR and fusion confidence entirely** (keyword-only).
- **Nothing combines the four numbers.** There is no "overall document confidence." The export gate evaluates three *independent* sub-gates (`ReconciliationOrchestrator.EvaluateExportGate:214-258`); the fusion `0.83` is never compared to a threshold there.

### 3.3 No shared abstraction
There is **no `Confidence` value object, no scoring interface, no contract** in the 5-stage pipeline — confidence is a bare scalar re-declared per result type. (The only `ConfidenceGuard`, `Veriqan.Domain/Verification/ConfidenceGuard.cs:32`, lives in the **separate Veriqan bounded context** and is unreferenced by this pipeline.)

### 3.4 Concrete scale/semantic inconsistencies (both sides cited)
1. Quality enum never numerically combined; ordering is non-monotonic/undocumented (`Q3_Low=3`, `Q4_VeryLow=4`, `Pristine=5`) — `Domain/Enum/ImageQualityLevel.cs:10-16`.
2. Quality `Confidence` **hardcoded `0.9f`** with a "trained model" comment over stub coefficients — `PolynomialImageQualityAnalyzer.cs:129`.
3. OCR `0–100` (`OCRResult.cs:16`) vs fusion `MeanConfidence 0–1` — bridged only at `ExtractionOrchestrator.cs:377`.
4. Fusion `0–1` (`FusionResult.cs:28`) vs classification `0–100` compared to `70` (`ReconciliationOrchestrator.cs:227,42`) — never on the same axis.
5. Two classification confidences on two scales (`ClassificationResult.cs:28` int vs `ExpedienteClassificationResult.cs:33` double); the gate uses the int one.
6. One event carries **both** scales: `ClassificationCompletedEvent.Confidence` int (0-100) **and** `.ConfidenceScore` double (0-1) — `Domain/Events/ClassificationCompletedEvent.cs:24-26, 51-53`.
7. The **0.70-vs-70 collision**: fusion `ManualReviewThreshold = 0.70` (`FusionCoefficients.cs:136`) and gate `ClassificationConfidenceThreshold = 70` (`ReconciliationOrchestrator.cs:42`) are the same intent in two units — impossible to keep in sync automatically.

### 3.5 "Fusion confidence" is reliability, not probability
`FieldFusionResult.Confidence` is set to `SourceReliability / maxReliability` (`FusionExpedienteService.cs:214,245`); `OverallConfidence` is the 70/30 weighted average of those (`:2501`). So `0.83` is a source-reliability figure, not a calibrated likelihood — worth knowing before any "combine the confidences" design.

---

## 4. The export gate & threshold inventory (Tier-2/3 evidence)

### 4.1 The gate
`ExportGatePolicy.cs` holds three **toggles** (all default `true`: `BlockOnLowConfidence:35`, `BlockOnFusionManualReviewRequired:42`, `BlockOnUnresolvedConflicts:49`); the **logic + the threshold value** live in `ReconciliationOrchestrator.EvaluateExportGate:214-258`:

| Gate | Condition | Threshold | Input | Hardcoded/Config |
|------|-----------|-----------|-------|------------------|
| 1 | `classificationResult.Confidence < 70` (null classification = no-op) | **70** | `ClassificationResult.Confidence` int 0–100 | **threshold hardcoded** `:42` |
| 2 | `(NextAction ?? ManualReviewRequired) == ManualReviewRequired` (null fusion = blocks) | enum | `FusionResult.NextAction` | toggle unbound |
| 3 | `ConflictingFields.Count > 0` | `>0` | `FusionResult.ConflictingFields` | toggle unbound |

### 4.2 `ExportGatePolicy` is dead config
It has a `SectionName = "ExportGatePolicy"` (`:28`) and settable bools but is **never bound** — no `Configure<ExportGatePolicy>` / `GetSection` / `.Bind` anywhere, no `ExportGatePolicy` key in any appsettings. Both production sites construct the all-ON default (`ReconciliationOrchestrator.cs:88`, call sites `Reconciliator Program.cs:157`, `ProcessingOrchestrator.cs:116`). **The toggles can turn gates off but cannot change the `70`.**

### 4.3 Every confidence-based gate/branch (note the disagreeing thresholds)
- Stage 4 review flags: `< 70` → `DocumentFlaggedForReviewEvent` and `RequiresManualReview` (`ReconciliationOrchestrator.cs:369,397`) — same hardcoded 70, **duplicated** in `ProcessingOrchestrator.cs:56`.
- Fusion `NextAction`: `< 0.70` (or conflict/required-field-missing) → ManualReview; `>= 0.85` → AutoProcess (`FusionExpedienteService.cs:2506-2526`, `FusionCoefficients.cs:129,136`).
- Manual-review "LowConfidence" case uses **`< 80`** — a *third* threshold (`ManualReviewerService.cs:646-660`).
- OCR thresholds elsewhere: `BulkProcessingService = 75.0f` (`:16`), `DocxFieldExtractor = 0.3f` (`:227`).

### 4.4 Tests that pin these numbers (change-impact)
`ReconciliationOrchestratorExportGateTests.cs` (lines 149/201/251/314/361/403 — uses 50/95/30/90 around the 70), `ProcessingOrchestratorTests.cs:541` (45 vs 70 flag), fusion mutation tests `FusionExpedienteServiceMutationTests.cs:236-281` (0.85/0.70), `ReconciliationPipelineServiceTests.cs:199,204` (reason strings). Any threshold/scale change must update these.

---

## 5. Hardening / re-architecture plan (grounded, tiered, independently shippable)

### Tier 1 — Unblock the gate (small, surgical, high-confidence)
- **T1.1 Wire the body text into Stage 4.** Populate the classifier input with the OCR/fused document text at `ProcessingOrchestrator.cs:530` and `ReconciliationOrchestrator.cs:353-356`. Add a regression test asserting `IMSS-2023-171230` classifies Aseguramiento ≥ 70. *(This alone makes the §2 gate green.)*
- **T1.2 Stop mislabelling no-signal as a real type.** In `DetermineLevel1Category`/`CalculateConfidence` (`FileClassifierService.cs:205-251`), an all-tied-at-floor result must return an explicit `Unknown`/`Unclassified` (or confidence 0), **never `(Aseguramiento, 10)`**. A default type that *looks* like a real classification at a real-looking confidence is a correctness hazard well beyond this gate.

### Tier 2 — Harden the classifier (still local to classification)
- **T2.1 Consult structured signals, not just substrings.** `Expediente.TieneAseguramiento == true` should drive the Aseguramiento decision directly; today it's ignored (`ExpedienteClasifierService.cs:379` already returns 0.90 for it but is **not** the wired classifier). Decide: route Stage 4 through `ExpedienteClasifierService`/`ClassificationDictionary` (richer, fuzzy, handles the boolean) or fold those signals into `FileClassifierService`.
- **T2.2 Broaden narrow keyword sets.** PLD relies on `LAVADO`/`OPERACIONES ILICITAS` but the generator emits *"recursos de procedencia ilícita"* / *"operaciones inusuales"* — a genuine miss for that type (`FileClassifierService.cs:159-172` vs `legal_catalog.py`). Reconcile the keyword tables against the generator's actual prose per requirement type.
- **T2.3 Make the classifier confidence a real score,** not a spread-of-keyword-buckets heuristic (`FileClassifierService.cs:205-236`) — at minimum document the band semantics; ideally derive from match strength/coverage.

### Tier 3 — Re-architect the confidence model (the grounded "rearchitecture")
- **T3.1 Introduce one `Confidence` value object** (fixed scale 0–1, carries provenance/stage, with explicit `combine`/`min`/`weighted` operators) and migrate `OCRResult`/`FusionResult`/`ClassificationResult`/`ImageQualityAssessment` onto it. Removes the four-scale problem (§3.1) and the `0.70`-vs-`70` collision (§3.4.7).
- **T3.2 Define an explicit aggregation contract** — *what is "document confidence" and which signals compose it?* Decide whether the export hold should consider OCR/fusion/quality (today it ignores all three: §3.2). Make quality actually flow (populate `QualityIndex`, `ExtractionOrchestrator.cs:403`) or formally declare it advisory.
- **T3.3 Single source of truth for thresholds.** Promote the hardcoded `70` into bound `ExportGatePolicy` config (§4.2), de-duplicate the two `=70` constants and reconcile the stray `<80` (`ManualReviewerService.cs:646`) and fusion `0.70/0.85`. One config object, bound at both composition roots, covered by the §4.4 tests updated in lockstep.
- **T3.4 Fix the dual-scale event** `ClassificationCompletedEvent` (§3.4.6) to carry a single `Confidence`.

### Suggested sequencing
T1.1 + T1.2 first (unblocks the gate, fixes a correctness hazard, ~hours). T2 next (raises real classification quality). T3 as a deliberate, test-guarded refactor (the mutation/gate tests in §4.4 are the safety net). T1 and T2 do **not** require T3.

---

## 6. Definition of done

- **Tier 1:** §2 gate (`RealSiaraCase_...`) reaches **green** — Stage 4 classifies the tier-1 case ≥ 70%, export fires (SIRO XML + 24-col DatosCarga xlsx + audit rows ≥2 processes). No-signal documents return `Unknown`, not `(Aseguramiento,10)`. Build 0/0; gate + classifier tests green.
- **Tier 2:** classifier consults `TieneAseguramiento` and the reconciled keyword tables; PLD/other types verified against generator prose; tests pin the new behavior.
- **Tier 3:** a single `Confidence` abstraction + one bound thresholds config replace the four scales, the duplicated `70`s, and the dual-scale event; the §4.4 tests are migrated; an ADR records the aggregation contract.

---

## 7. Provenance

This document is the synthesis of a four-track parallel code inspection (classification internals; export gate & thresholds; pipeline-wide confidence model; corpus-vs-classifier cross-check) run 2026-06-25 against the exact gate failure. Every file:line above was cited by those tracks. Companion blockers (now fixed): `TASK-OCR-SEGFAULT-LINUX.md`, `TASK-GATE-CORPUS-SYNTHETIC-WORKLOAD.md`. Gate-run evidence: the 2026-06-25 combined run recorded in `CLIENT-DEMO-CAPTURE-RUNBOOK-2026-06-15.md` §A3.

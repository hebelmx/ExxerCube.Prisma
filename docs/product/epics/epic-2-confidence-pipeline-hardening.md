# Epic 2: Confidence Pipeline Hardening & Re-architecture

**Epic Goal:** Eliminate a proven wiring defect that causes the §2 max-fidelity gate to block at Stage 5, then progressively harden the classifier and re-architect the confidence model so that the export decision is grounded in a coherent, unified signal rather than a single brittle keyword integer.

**Status:** Draft
**Created:** 2026-06-25
**Version:** 1.0

---

## Background & Findings

A four-track parallel code inspection run on 2026-06-25 against the live §2 gate failure (`Stage 5 BLOCKED ... Classification confidence 10% is below the required threshold of 70%`) produced two findings, both evidence-proven:

**Finding 1 — Wiring defect (immediate cause).**
Stage 4 classification receives only two short fused fields (`AreaDescripcion` + `NumeroExpediente`); the 1284-char OCR body text is never passed in, and `LegalReferences` is never populated (`ProcessingOrchestrator.cs:530-535`, `ReconciliationOrchestrator.cs:353-356`, `FileClassifierService.cs:40-44,78`). The synthetic case `IMSS-2023-171230` contains the word ASEGURAMIENTO 4+ times in its body — if that text reached the classifier it would score ~90%, not 10%. The reported `(Aseguramiento, 10)` is a deterministic artifact of all six categories tying at the no-match floor and dictionary-insertion-order tie-break (`FileClassifierService.cs:205-251`). This is the "right label for the wrong reason" — a correctness hazard.

**Finding 2 — No unified confidence model (systemic cause).**
Four pipeline stages emit four independent confidences on four different scales (quality enum 1–5, OCR float 0–100, fusion double 0–1, classification int 0–100) with no shared `Confidence` abstraction, no composition, and no aggregation into a single document confidence. The sole export gate decision (`ReconciliationOrchestrator.cs:225-231`) uses only the weakest, most brittle of the four while OCR 95.71%, fusion 0.83, and quality Q3_Low never enter the comparison. The threshold `70` is hardcoded in two places and is disconnected from the dead `ExportGatePolicy` config section.

Full evidence: `docs/planning-artifacts/remediation/CONFIDENCE-PIPELINE-DEEP-INSPECTION-2026-06-25.md`.

---

## Goals

1. Turn the §2 gate green — Stage 4 must classify a real corpus document at ≥ 70% confidence and reach export.
2. Eliminate the "no-signal document labeled as a real type at a real-looking confidence" correctness hazard; no-signal must return `Unknown`/`Unclassified` (confidence 0).
3. Harden the classifier by consulting structured boolean signals and reconciling keyword tables against generator prose.
4. Introduce a single `Confidence` value object and one bound thresholds configuration that all pipeline stages share.
5. Record the aggregation contract in an ADR so the export gate is explainable and maintainable.

---

## Out of Scope

- ML/trained models for classification (deterministic keyword/rule approach retained).
- Reactivation of dormant Python/CSnakes VLM path (optionality-by-design per ADR-001).
- Sentinel monitoring service integration (OUT-OF-MVP).
- PDF digital signing (Story 1.8).
- Any changes to the SIRO XML schema or export format.

---

## Three-Tier Structure

| Tier | Label | Priority | Independently shippable? |
|------|-------|----------|--------------------------|
| Tier 1 | Unblock the gate | P0 | Yes — Tier 1 alone turns the gate green |
| Tier 2 | Harden the classifier | P1/P2 | Yes — does not require Tier 3 |
| Tier 3 | Re-architect the confidence model | P1/P2 | Yes — after Tier 1+2 are stable |

---

## Success Metrics

| Metric | Target |
|--------|--------|
| §2 gate result | Green (export fires: SIRO XML + DatosCarga xlsx + ≥ 2 audit rows) |
| IMSS-2023-171230 classification confidence | ≥ 70% (Aseguramiento) |
| No-signal document result | `Unknown` / confidence 0 — never `(Aseguramiento, 10)` |
| Confidence abstraction | Single `Confidence` value object in Domain; all four result types use it |
| Thresholds | One bound `ExportGatePolicy` config section; zero hardcoded numeric literals |
| Build | 0 errors / 0 warnings |
| Existing tests | All §4.4 pinning tests (`ReconciliationOrchestratorExportGateTests.cs`, `ProcessingOrchestratorTests.cs`, `FusionExpedienteServiceMutationTests.cs`) green |

---

## Integration Requirements

- All changes preserve the `Result<T>` pattern; no exceptions for business logic.
- `CancellationToken` propagated on every new or modified async method.
- Hexagonal architecture boundaries respected: interfaces in Domain, implementations in Infrastructure.
- No breaking changes to `IFileClassifier`, `IFusionService`, or `IOcrExecutor` public contracts without a coordinated migration.
- The dormant Python/CSnakes path must not be disturbed.

---

## Stories

| ID | Priority | Tier | Slug | Summary |
|----|----------|------|------|---------|
| 2.1 | P0 | 1 | wire-stage4-classifier-input | Pass OCR/fused body text into Stage 4 classifier input — unblocks the gate |
| 2.2 | P0 | 1 | no-signal-returns-unknown | All-tied-at-floor result returns Unknown/confidence 0, never a fake type |
| 2.3 | P1 | 2 | classifier-consults-structured-signals | Consult `Expediente.TieneAseguramiento` boolean and/or wire `ExpedienteClasifierService` |
| 2.4 | P1 | 2 | reconcile-keyword-tables | Reconcile classifier keyword sets against generator prose; fix the PLD miss |
| 2.5 | P2 | 2 | classifier-confidence-real-score | Replace keyword-bucket spread heuristic with a documented real score |
| 2.6 | P2 | 3 | confidence-value-object | Introduce unified `Confidence` value object; migrate all four result types |
| 2.7 | P2 | 3 | confidence-aggregation-contract | Define document confidence contract + ADR; make quality signal flow or declare advisory |
| 2.8 | P1 | 3 | single-thresholds-config | Bind the dead `ExportGatePolicy` config; remove all hardcoded threshold literals |
| 2.9 | P2 | 3 | fix-dual-scale-classification-event | Collapse dual int/double confidence on `ClassificationCompletedEvent` to one field |

---

## Story Sequencing and Dependencies

```
[2.1] Wire body text ──┐
[2.2] No-signal fix    ┘ → Tier 1 complete → gate green

[2.3] Structured signals ──┐
[2.4] Keyword tables       ┤ → Tier 2 complete
[2.5] Real score           ┘

[2.6] Confidence VO ───────┐ (2.6 must precede 2.9)
[2.7] Aggregation ADR ─────┤ → Tier 3 complete
[2.8] Thresholds config    ┤
[2.9] Dual-scale event ────┘ (depends on 2.6)
```

Tier 2 can begin once Tier 1 is merged; Tier 3 can begin in parallel with Tier 2 but Story 2.9 must wait for Story 2.6.

---

## Risk Mitigation

- Tier 1 is surgical (two wiring lines); risk of regression is minimal given the existing gate and classifier tests in `ReconciliationOrchestratorExportGateTests.cs`.
- The §4.4 tests (`ReconciliationOrchestratorExportGateTests.cs` lines 149/201/251/314/361/403; `ProcessingOrchestratorTests.cs:541`; `FusionExpedienteServiceMutationTests.cs:236-281`) are the safety net for all threshold/scale changes.
- Any Tier-3 refactor that touches a confidence field must update those tests in the same commit.
- The `ExportGatePolicy` toggle mechanism means gates can be turned off independently if a Tier-3 change requires it.

---

## Rollback Considerations

- Tier 1 stories are additive wiring changes; rollback is a one-line revert.
- Tier 2 keyword changes are data-in-code; the classifier test suite pins the expected outputs.
- Tier 3 introduces a new value object; existing callers can be migrated incrementally with implicit conversion operators.

---

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-06-25 | 1.0 | Initial epic creation from deep-inspection `CONFIDENCE-PIPELINE-DEEP-INSPECTION-2026-06-25.md` | John (PM) |

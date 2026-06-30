---
epic: 5 (deferred follow-up F1) → folds into Epic 6 hardening
story: F1 — RESUMEN parse-failed vs genuinely-absent honesty gap
status: DONE
branch: Liv
driver: bmad-orchestrator
date: 2026-06-30
source: docs/planning-artifacts/HANDOFF-veriqan-epic5-2026-06-30.md (F1)
---

# F1 — Honest extractor status for RESUMEN amounts (Adeudo / Pagos)

## Problem
`ExtractResumenField` / `ScanResumenColumn` (`PdfPigStatementFieldExtractor.cs:1352/1382`)
return `ExtractedField<decimal>.Missing` (`NotExtracted`) for BOTH:
  (a) genuinely-absent row — Banamex suppresses zero-value RESUMEN rows → label never typeset, AND
  (b) label found but the amount fails to parse (unusual locale / OCR-corrupted digit).
CL-21's guarded implied-zero then silently substitutes `0m` for case (b) too — a latent honesty
gap: a future PDF with a printed-but-unreadable Adeudo/Pagos amount becomes 0m instead of abstaining.

## Fix (one cohesive chunk)
1. **Extractor** — when a RESUMEN label IS matched but no parseable amount is found, return
   `ExtractedField<decimal>.InvalidFormat(...)` (status `ExtractedInvalidFormat`) instead of `Missing`.
   When NO label is matched at all → keep `Missing` (genuinely-absent / suppressed-zero). The status
   value already exists in `ExtractionStatus.cs`. Propagate "label-seen" across the two passes in
   `ExtractResumenField` (right column then left column).
2. **CL-21 rule** — implied-zero ONLY when `Status == NotExtracted`. When
   `Status == ExtractedInvalidFormat` → `InsufficientData` (abstain). Applies to both
   `AdeudoPeriodoAnterior` and `PagosYAbonos` (`Cl21PagoParaNoGenerarInteresesRule.cs:128-143`).
3. **Tests** — add unit test: label-present-amount-unparseable → CL-21 abstains (InsufficientData).
   Confirm genuinely-absent → implied-zero unchanged. Keep arithmetic + E2E suites green.

## Non-regression guarantees (verified before delegation)
- `Cl17AdeudoPeriodoAnteriorRule` (`:106`) and `Cl20PagosYAbonosSumaDesgloseRule` (`:105`) both
  guard `!= Extracted` → already abstain whether status is NotExtracted or ExtractedInvalidFormat.
- `bad-math-cl21` and `good.pdf` rows are genuinely absent (label unmatched) → still `Missing` →
  CL-21 still fires RED on `bad-math-cl21`; `good.pdf` CL-21 stays Pass.

## Tasks
- [x] T1 — Extractor: InvalidFormat on label-matched-amount-unparseable (+ propagate across passes)
- [x] T2 — CL-21: ExtractedInvalidFormat → InsufficientData; implied-zero only on NotExtracted
- [x] T3 — Tests: 3 new tests (Adeudo/Pagos InvalidFormat→InsufficientData; NotExtracted→implied-zero regression guard)
- [x] T4 — Verified ground truth: build 0/0; Validation 516/516; Orchestration 91/91; Extraction 187/187
- [x] T5 — Consumer trace (CL-17/CL-20 abstain both; MandatedBoldFields skips !=Extracted); committed + pushed; handoff closed

## Result (verified from ground truth 2026-06-30)
- Build: 0 errors / 0 warnings.
- `Veriqan.Infrastructure.Validation.Tests` 516/516 (incl. `Cl21_AdeudoExtractedInvalidFormat_ReturnsInsufficientData`,
  `Cl21_PagosExtractedInvalidFormat_ReturnsInsufficientData`, `Cl21_NotExtracted_AdeudoAndPagos_StillAppliesImpliedZero_ReturnsPass`).
- `Veriqan.Orchestration.Tests` 91/91 — CL-21 still fires RED on `bad-math-cl21` (no demo regression).
- `Veriqan.Infrastructure.Extraction.Tests` 187/187 — no extractor regression.
- Reverted `docs/qa/calibration/calibration-report.md` (test side-effect, never committed).

---
epic: 5
story: 5.1
status: DONE (with one tracked follow-up — F1)
branch: Liv
commits:
  - 1adbf4af  feat(veriqan): Epic 5 — CL-21 fires RED on bad-math-cl21 (guarded implied-zero)
  # (a follow-up commit closes review findings F2/F3 — see below)
generatedBy: bmad-orchestrator
date: 2026-06-30
supersedes: none
relatedDocs:
  - docs/planning-artifacts/epics-veriqan-vec-demo-2026-06-27.md  (Epic 5 / Story 5.1, FR-D1)
---

# Veriqan Epic 5 / Story 5.1 — Handoff & Design Record

## Outcome

CL-21 (`Cl21PagoParaNoGenerarInteresesRule`) now **fires RED (Fail/Critical)** on
`bad-math-cl21.pdf` instead of abstaining (InsufficientData). FR-D1 satisfied.

Verified from ground truth (`dotnet exec`, net10.0 MTP):
- `Veriqan.Infrastructure.Validation.Tests` — green (CL-21 implied-zero/Pass/Fail/guard tests).
- `Veriqan.Orchestration.Tests` E2E — green; the demo fixture asserts CL-21 ∈ `FailCheckIds`
  and ∉ `InsufficientDataCheckIds`.
- Build 0 errors / 0 warnings.

## Why the obvious approaches did NOT work (the diagnosis)

The Story 5.1 AC assumed the operands were either extractable or the fixture needed
re-targeting. Neither was the real situation:

1. **`AdeudoPeriodoAnterior` and `PagosYAbonos` are genuinely absent from this layout.**
   It is a real Banamex format that **suppresses zero-value RESUMEN rows** (new account →
   no prior debt; no payments in period). The two-pass column scanner is correct; those
   rows were never typeset. Confirmed via `pdftotext` on the fixture.

2. **The injected defect was sub-tolerance.** The old `_inject_math` only flipped the cents
   (`.99`), a **$0.44** delta — *below* the **$0.50** CONDUSEF `currencyToleranceMxn`. So even
   after the rule could reach the arithmetic, CL-21 *correctly* PASSED. By the system's own
   legal definition, $0.44 is not a violation.

## Design decision: "Guarded implied-zero" (the canonical handling for this layout)

> **DECISION (owner-confirmed 2026-06-30):** When `AdeudoPeriodoAnterior` or `PagosYAbonos`
> is absent, CL-21 treats it as an implied **`0m`** rather than abstaining. CONDUSEF does not
> mandate printing zero-value rows; their suppression is legally valid.

Guard structure in `Cl21PagoParaNoGenerarInteresesRule.cs`:
- **Hard `!= Extracted` guards retained** on the 5 CORE operands (`CargosRegularesNoMeses`,
  `CargosComprasAMesesCapital`, `MontoIntereses`, `MontoComisiones`, `IvaInteresesYComisiones`)
  and on the target `PagoParaNoGenerarIntereses`. A missing core operand or target means the
  RESUMEN block / payment block genuinely failed to parse → **still InsufficientData**.
- **Relaxed only** `AdeudoPeriodoAnterior` + `PagosYAbonos`: implied `0m` when absent; if
  *present (Extracted) but low-confidence*, still abstain (a misread digit in a real non-zero
  row is dangerous).

This preserves Epic 4's honesty contract (an extraction gap is never a manufactured verdict)
for the whole-block / target / present-but-low-confidence cases.

## Fixture change

`scripts/veriqan-corpus/anonymize.py` `_inject_math` now injects a deterministic **+$11.00**
"fat-finger" overcharge (`$12,604.55 → $12,615.55`) — a genuine violation well above the $0.50
tolerance. `Prisma/Fixtures/PRP2/demo/bad-math-cl21.pdf` regenerated. `Saldo cargos regulares`
($12,604.55, the true sum) is untouched, so the delta is real.

## Collateral (intended, disclosed): CL-22 also fires

The single injected error legitimately violates **two** arithmetic identities:
- **CL-21:** sum-of-operands ($12,604.55) ≠ printed Pago ($12,615.55).
- **CL-22:** `SaldoCargosRegulares` ($12,604.55) ≠ printed Pago ($12,615.55).

This is honest — a real fat-finger on that figure would trip both. The E2E now asserts BOTH
CL-21 and CL-22 in `FailCheckIds` (review finding F2). `good.pdf` stays RED-via-structural with
CL-21 = **Pass** (no false RED); CL-22 also Pass.

## Adversarial review (commit 1adbf4af) — disposition

| # | Sev | Finding | Disposition |
|---|-----|---------|-------------|
| F1 | Major | Implied-zero cannot distinguish *genuinely-absent* row from *label-present-but-amount-unparseable* — the extractor returns `NotExtracted` (via `ExtractedField.Missing`, `PdfPigStatementFieldExtractor.cs:1441`) for **both**, so a future PDF with a printed-but-unreadable Adeudo/Pagos amount would silently become `0m`. **NOT a problem for `bad-math-cl21`** (rows truly absent) or `good.pdf`. | **CLOSED 2026-06-30** (orchestrator F1 pass). `ScanResumenColumn`/`ExtractResumenField` now return `ExtractedInvalidFormat` (raw `0m` placeholder) when a RESUMEN label is matched but no parseable amount follows; only a never-matched label stays `Missing`/`NotExtracted`. CL-21 abstains (InsufficientData) on `ExtractedInvalidFormat` for both Adeudo & Pagos; implied-zero applies **only** to `NotExtracted`. 3 new unit tests. CL-17/CL-20 unaffected (guard `!= Extracted`); MandatedBoldFields unaffected (skips `!= Extracted`). Verified: build 0/0, Validation 516/516, Orchestration 91/91 (CL-21 still RED on bad-math-cl21), Extraction 187/187. Tracker: `docs/planning-artifacts/TRACKER-veriqan-f1-honesty-fix.md`. |
| F2 | Minor | CL-22 collateral firing undisclosed/unasserted | **CLOSED** (follow-up commit): asserted + documented. |
| F3 | Minor | Missing symmetric `AdeudoPeriodoAnterior` present-but-low-confidence unit test | **CLOSED** (follow-up commit): test added. |
| F4 | Minor | No durable design-decision record | **CLOSED**: this document. |

## Tracked follow-up (F1) — for the owner

Treating a *parse-failed* (not absent) Adeudo/Pagos as `0m` is a latent honesty gap for future
real-world PDFs (unusual locale, OCR-corrupted digit). Recommended fix: extractor returns a
distinct status when a RESUMEN label is located but its amount is unparseable, so CL-21 abstains
(InsufficientData) in that case instead of substituting zero. Scoped OUT of Epic 5 because it
touches the shared extractor status model (other rules check `!= Extracted`) and warrants its own
verified pass. See Epic 6 (Production-Readiness Hardening).

## Files touched (Epic 5 total)

- `Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Validation/Rules/Cl21PagoParaNoGenerarInteresesRule.cs`
- `scripts/veriqan-corpus/anonymize.py` (`_inject_math`)
- `Prisma/Fixtures/PRP2/demo/bad-math-cl21.pdf` (regenerated)
- `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Veriqan.Infrastructure.Validation.Tests/ArithmeticRulesTests.cs`
- `Prisma/Code/Src/CSharp/08 Tests/03 Orchestration/Veriqan.Orchestration.Tests/VecChecklistDemoE2ETests.cs`

## Next in the plan

Epic 6 (Production-Readiness Hardening / Tier B) — 8 self-contained stories. The F1 follow-up
naturally folds into that hardening epic.

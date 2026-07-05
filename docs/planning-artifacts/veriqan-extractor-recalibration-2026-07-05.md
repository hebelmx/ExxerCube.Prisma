# Veriqan extractor recalibration + robustness plan (2026-07-05)

Branch: `Liv`. Owner: hebelmx. Orchestrated via bmad-orchestrator.

## Problem (ground-truthed)

`PdfPigStatementFieldExtractor` (5,620 lines, positional) reports several fields as
`NotExtracted` — or mis-extracts them to wrong values — on the **real demo statements**
(`Prisma/Fixtures/PRP2/demo/{good,compliant-master,bad-math-cl21,bad-font-cl35}.pdf`),
even though the fields are present. `scanned.pdf` is image-only → all-`NotExtracted` is
**correct by design** (ExtractionGap case).

**Root cause:** the header identity fields were dual-calibrated (Dummie VEC + real
Banamex) and work. The **period/summary and movements extractors are still calibrated
only to the synthetic "Dummie VEC" fixture** — its exact label phrasing, page-1
coordinates, and literal section headers. The demo statements use a different
Banamex-style layout (different wording, positions, 9–14 pages).

### Authoritative field-status (real ExtractFullAsync, all 4 text fixtures identical)

**① Present but reported NotExtracted (real bugs):**
- `Movements` (→ `TotalCargos`/`TotalAbonos`): dozens of real rows exist (e.g.
  `27-mar-2026 … EBAY … +$4,392.93`; `04-mar-2026 04-mar-2026 MERCADO PAGO 003 de 012 +$953.69`).
  Section detection keys on literal word `"DESGLOSE"` (OrdinalIgnoreCase) → matches the
  lowercase prose "…en el desglose de movimientos" on the WRONG page (`sectionFound=true`,
  status `NoRowsParsed`) while the real table pages are never row-scanned. Column X-ranges
  also calibrated to Dummie ~540pt width, not letter 612pt.
- `Tasa` / `Cat`: mandatory VEC fields; present in a real % box. `ExtractTasaAndCat`
  expects a caps `"TASA DE INTERES ANUAL"` heading + two-percent band. "CAT" also appears
  as *"Centro de Atención Telefónica CAT:"* (phone) → must disambiguate.
- `PaymentDueDate`: real phrasing *"Fecha límite **para realizar el** pago"* vs Dummie
  token run `Fecha/límite/de/pago`.

**② Mis-extracted to WRONG value (false confidence — worse than NotFound):**
- `Product` = `"Número de tarjeta 4111000000070001"` (grabbed card-label row).
- `Address` = `"4-mar-2026 al 01-abr-2026"` (grabbed right-column period text).

**③ Likely-correct abstention (verify, don't force):** `AdeudoPeriodoAnterior`,
`PagosYAbonos`, `PagoMinimoMasMeses`, `SaldoDeudorTotal` — those exact labeled lines
don't appear on `good.pdf` page 1 (RESUMEN block composed differently). Abstaining is the
Veriqan-desired honest behavior ("misread ≠ false non-compliant").

Structural note: `ExtractPeriodSummary` scans page-1 words only, so any summary field on a
later page in the real multi-page layout is invisible by construction.

## Phase 1 — Recalibration (additive, verified per-field)

Constraint: **additive only** — handle BOTH layouts; keep the Dummie VEC fixture tests
(`Veriqan.Infrastructure.Extraction.Tests`) green. Same pattern already used for the
header fields. Result<T> + CancellationToken conventions. Build 0/0, warnings-as-errors.

- **P1.0** ✅ Reusable diagnostics (PdfPig word-coordinate dump + field-status dump).
- **P1.1** ✅ Address → abstain (postal-code gate). **Product-abstain REVERTED** in P1.4
  (`00f1b3e0`): making Product null flipped all 4 demo E2E verdicts to ExtractionGap
  (`UnknownProduct`) — `VerificationPipeline:657` gates the whole verdict on a non-null
  product, and the "wrong" token `"Número de tarjeta 4111…"` is a *load-bearing* TC-BSSB
  alias in the demo bundle BY DESIGN. Real product resolution → Phase 2. (commits `bf8629c2`+`00f1b3e0`)
- **P1.2** ✅ RE-SCOPED → deferred to Phase 2. Ground truth: on this corpus PaymentDueDate
  has no clean labeled value, and TASA/CAT have no clean positional anchor (rates are a
  fragile split footer `7.77%/29/.72%`; the only literal `CAT:` is a phone-center label).
  Positional branches would be brittle. Honest abstention now; semantic/LLM fallback (P2)
  is the right tool. Same for ③ (`Adeudo`/`PagosYAbonos`/`PagoMinimoMasMeses`/`SaldoDeudorTotal`).
- **P1.3** Movements real-table detection + column mapping. Real table (good.pdf p3): no
  literal `DESGLOSE` header; columns date≈32/96, desc≈150–320, **sign≈469.5** (Dummie 423–438),
  amount≈483. Adjacent installments table (`DISPONIBLE IndFusion`, 5 money cols) must NOT be
  parsed as movements. Additive — keep Dummie movement tests green.
- **P1.4** Full verification + adversarial review; remove temp diagnostics; commit/push.

Definition of done (Phase 1): targeted ① fields flip to `Extracted` on `good.pdf`; ②
fields abstain (not wrong); `scanned.pdf` still all-abstain; Dummie fixtures green;
build 0/0.

## Phase 2 — Robustness: progressive fallback chain (BMAD party, plan only)

The real world will vary far more than these 4 fixtures. Design a **per-field progressive
fallback chain**, escalating only as needed (cheap→expensive), NOT all fields to the heavy
path:

1. exact / positional (current)
2. fuzzy match
3. Levenshtein distance
4. semantic search
5. LLM extraction

Party (architect + qa + dev + analyst + pm) produces a design doc + multi-epic/story plan.
This will span several topics — treat as a program, not one epic. No code in Phase 2.

## Phase-2 hardening backlog (from adversarial review 2026-07-05)

Confirmed safe on the current corpus; residual risk only for unseen layouts → fold into
the fallback-chain design:
- **Product false-confidence (F1).** On any *non-demo* Banamex statement, `ExtractProductName`
  returns the card-label row as `Extracted` (load-bearing only via the demo bundle's
  `TC-BSSB` alias). For real inputs it is a wrong value that won't alias-resolve →
  `UnknownProduct`. Real product resolution (semantic/lookup) is the fix.
- **Movements description overlap (F2).** `DesgloseDescriptionXMin=145` overlaps
  `DesgloseChargeDateXMax=157`; `BuildDescription` takes any token in [145,422] without
  re-checking the date pattern. No date leak on any current fixture, but a cheap hardening
  is to exclude `DesgloseDatePattern` tokens from the description. (Also: Dummie descriptions
  now silently include merchant-RFC tokens — harmless, tests use `.Contains`.)
- **Address gate (F6).** Postal-code gate can (a) emit a false address if a period/pago block
  ever contains a stray 5-digit token, and (b) abstain on a real address printed without a CP
  (honest, but a completeness reduction).

## Verification harness

- Field-status: real `ExtractFullAsync` over 5 fixtures → per-field `ExtractionStatus`.
- Coordinate dump: PdfPig words `(Left, Bottom, Right, Text)` per page (extractor's own
  bottom-left coord system — do NOT calibrate from PyMuPDF/pdftotext, coord systems differ).

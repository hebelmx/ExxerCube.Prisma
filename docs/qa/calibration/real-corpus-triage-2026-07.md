# RC1.S3 Triage — Real-Corpus Baseline Findings (2026-07-22)

Classification of every non-GREEN outcome from `real-corpus-baseline-2026-07.md` (12 real
anonymized statements A/B/C + 4 defect specimens). Taxonomy: **(a)** true statement defect ·
**(b)** extractor/section-detection miscalibration · **(c)** rule miscalibration ·
**(d)** honest abstention · **(e)** anonymization-tooling artifact · **(ref)** reference-data /
owner-input gap. Evidence: two independent read-only investigations (code paths + out-of-repo
baseline JSON + PDF text layers). PII rules of the epic doc apply — neutral labels only.

## Headline dispositions

1. **No true statement defect was proven anywhere.** The presumption-of-compliance survived every
   check; the bank's verifiable arithmetic is exact to the cent on all 4 B months.
2. **`defect-good` (== demo `good.pdf`) is literally statement B-2026-04** — identical finding
   values, not just an identical signature. The long-standing "good.pdf residual RED (tool
   artifacts)" is now fully dispositioned by this triage.
3. The 14-check structural signature is a property of the **real-Banamex layout family vs
   demo-calibrated tooling** (anchors/legends/thresholds calibrated to Dummie-VEC fixtures),
   not of these specific months.

## Verdict-gate triage (A and C series)

| Series | Signal | Gate (file:line) | Class | Detail |
|---|---|---|---|---|
| A (checking) ×4 | ExtractionGap | Stage-2b coverage floor, `VerificationPipeline.cs:245-264` (floor 10, `TenantProfile.cs:183`) | **(d)** | Extractor targets the credit-card layout; 3 fields < floor. Designed abstain-safe behavior (GH#19). |
| C (Mastercard) ×4 | ExtractionGap `UnknownProduct` | Empty-token branch `ProductResolver.cs:48-53` | **(ref)** | Product IS extracted ("…PLATINUM BANAMEX", 0.9, E7 header-OCR) then **erased** by the E3 terminal membership validator (fuzzy ~82 < 85 vs a 2-row Visa-oriented catalog). Abstain-safe held (3 pts from a silent wrong-product match). Unblock = owner catalog row whose **alias** carries the full literal (pass-1 exact match ignores ProductName). |

**Instrument artifact (harness, not product):** the baseline JSON's per-field snapshot calls
`ExtractFullAsync` without the catalog bundle, so it shows C's Product as extracted while the
pipeline's runtime model abstained it. Coverage counts trustworthy; Product state not
pipeline-faithful. Also: C's recorded reason string ("Product token must not be null or empty")
masks the real mechanism — triage from JSON alone would misdiagnose.

## B-series structural signature (14 checks, fail ×4)

| Check | Class | Root cause (file:line) | Conf |
|---|---|---|---|
| CL-18 | **(c)** | Rule sums ALL non-MSI charge rows; desglose contains interest/commission/IVA rows the printed "Cargos regulares" excludes. Delta == MontoIntereses+MontoComisiones+IVA **to the cent on 4 independent months** (`Cl18CargosRegularesSumaDesgloseRule.cs:103-137`). | HIGH |
| CL-31 | **(b)** | No text-layer page labels (image footer); page-wide "N de M" fallback (`PdfPigStatementFieldExtractor.cs:3499-3508`) grabs **MSI installment fragments** → constant fake totals {3,12}. | HIGH |
| CL-32 | unresolved (a)/image-rendered | "COMPARA TU TARJETA" absent from text layer; pages are image-heavy (900+ images/page) — needs visual/OCR look. | MED |
| CL-46 | **(ref)** | Bundle legend expects "…SIN VALIDEZ FISCAL"; real statement prints SAT-standard "…DE UN CFDI" (twice). Bundle CSV value wrong, not the statement. | HIGH |
| CL-48 | **(c)** | Blank-gap computed over text words only; image-rendered regions count as blank (`ComputeMaxVerticalGap`, `PdfPigStatementFieldExtractor.cs:3413-3429`). | HIGH |
| CL-50 | **(b)** | Fiscal-block fast-path requires the Epic-3 *synthetic* legend, 0 occurrences in real docs → QR decode never runs (`PdfPigStatementFieldExtractor.cs:3772,3816-3818`). | HIGH |
| CL-51 | **(b)** | Same root; latent second bug: real UUID wraps across two lines → regex will still miss post-fix. | HIGH |
| CL-52 | **(b)** | Same root; issuer RFC IS present in text layer, extraction never ran. | HIGH |
| CL-53 | **(b)** | Same root; post-fix may correctly abstain (receiver RFC value appears image-rendered; first/second-RFC heuristic would misassign). | HIGH |
| LAW-SEC-PRESENCE | **(b)** | Anchor table self-documents calibration to "Dummie VEC fixture PDFs" (`PdfPigStatementFieldExtractor.cs:4034-4035,4079-4130`); real headings differ/are images → 18/23 "missing". | HIGH |
| LAW-SEC-ORDER-GAP | **(b)** | Anchors false-hit **cross-reference sentences** ('Ver notas en la sección "…"') → ordering over false positions. | HIGH |
| LAW-TYPO-MINSIZE | **(e)** majority + owner call | **Most sub-8pt spans (~27/31 per doc) are anonymizer-written replacement tokens** — PyMuPDF redaction auto-shrinks longer replacements to fit the original rect (`anonymize.py:532-560`). **Adversarial review correction:** ~4 spans/doc are ORIGINAL footnote-reference superscript digits (~6pt) present pre-anonymization — re-anonymizing will shrink, not zero, the breach. Whether footnote markers are legally in-scope for the size floor = owner ruling. | HIGH |
| LAW-§26-NOTAS | **(c)** + owner call | §26-b is near-verbatim (one word) but the 1.5× matcher window mathematically caps non-substring similarity ~0.67-0.8, below the 0.82 threshold; multi-column word order defeats the substring fast-path (`VerbatimBlockMatcher.cs:39-104`). §26-i = genuine minor wording delta — legal/owner judgment. | HIGH/MED |
| LAW-§27-GLOSARIO | unresolved image-rendered/(a) | Glossary content absent from text layer (only the cross-reference exists); likely image pages — needs visual/OCR. | MED |

## CL-21 variance (B series)

All five core operands match the printed text to the cent — **no operand misreads**. Both guarded
operands (AdeudoPeriodoAnterior, PagosYAbonos) NotExtracted 0/4 → formula always ran with both
zeroed (Epic-5 implied-zero).

- B-2026-03 Fail (+409.13) and B-2026-05 Fail (+2159.45): **implied-zero artifacts** — the
  subtraction rows are printed as images (bare "−" glyph in text layer, no label/amount), so
  "label never typeset ⇒ zero" is **falsified on real statements**.
- B-2026-04 Pass: same zeroing; net happened to be ≈0 that month (this is the good.pdf month —
  why Epic-5's premise looked safe on the demo).
- B-2026-06 InsufficientData: honest abstention (target field NotExtracted).
- Safe fix direction: abstain (or E7-style image-OCR the two rows) when they can't be confirmed
  zero — never silently compute a wrong Expected.

## C1/C2 non-vacuity question — ANSWERED: NO

Tasa/Cat/TotalCargos/TotalAbonos are NotExtracted 0/4 on both credit-card series. The real corpus
does not make the C1/C2 synthetic-only confidence slices non-vacuous. (Separately:
AdeudoPeriodoAnterior/PagosYAbonos/SaldoDeudorTotal/PagoMinimoMasMeses/PaymentDueDate are 0/4 on
all three series — a layout gap independent of product.)

## S4 candidate ledger

**Mechanical, high-confidence code fixes (candidate S4.a…):** CL-18 row taxonomy; CL-31 fallback
constraint; CL-48 image-aware content; CL-50–53 fiscal-block recalibration (+UUID wrap, RFC
assignment); LAW-SEC anchor recalibration + cross-reference guard; LAW-§26 matcher scoring;
CL-21 implied-zero retirement for image-rendered rows.

**Corpus-pipeline fix (not product):** anonymize.py replacement sizing → re-anonymize →
re-measure (clears LAW-TYPO-MINSIZE; changes corpus sha256s → regenerate index).

**Owner-gated:** CL-46 bundle legend value; §26-i wording + 0.82 threshold (legal); CL-32 + §27
visual/OCR confirmation; C product catalog row (owner reference data); checking-layout support
for A (scope decision).

**Not triaged:** CL-42 single fail on B-2026-06 (out of signature; carry to S4 scoping).

## Adversarial-review gate (2026-07-22, post-S3)

Independent skeptic pass over the five load-bearing claims: CL-18 CONFIRMED (deltas independently
recomputed, all 4 months to the cent); CL-31 CONFIRMED (zero "página" text-layer hits; the {3,12}
totals are the two MSI plan lengths); CL-21 CONFIRMED (deltas reproduced; Pass month is exactly
0.00; note the true image values remain unproven-non-zero pending OCR); C-UnknownProduct chain
CONFIRMED end-to-end (terminal-validator abstain → second Resolve on null → empty-token branch);
LAW-TYPO-MINSIZE **corrected** (see table row).

New risks S4 MUST account for:
1. **LAW-TYPO residual:** ~4 legitimate sub-floor footnote-marker spans per doc survive any
   anonymizer fix — decide their legal scope before calling the check "cleared".
2. **Dual product catalog:** a second, 4-row production reference-bundle catalog exists alongside
   the 2-row fixture the harness measures. Any catalog/alias fix must land consistently in both,
   or S4 "fixes" a check that runs differently in production.
3. **Instrument blast radius:** the baseline JSON's per-field snapshot bypasses bundle-gated
   validators (root cause of the Product mismatch) — this can affect ANY field with a
   validatorOverride. Audit before trusting the JSON's field states for further S4 scoping.

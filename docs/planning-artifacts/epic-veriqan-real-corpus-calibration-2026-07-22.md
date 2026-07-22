# Epic RC1 — Real-Corpus Calibration Baseline (Veriqan)

**Date opened:** 2026-07-22 · **Branch:** `Liv` · **Status:** IN PROGRESS
**Authorized by:** owner ruling #3, `DECISIONS-2026-07-21-owner-rulings.md` (real CONDUSEF corpus =
next Veriqan lever; calibrate recompute rules (Epic 11 residual) + validators against ground truth).

## Material that landed (ground truth, verified 2026-07-22)

- **12 unique real Banamex/Citibanamex statements**, anonymized, at
  `~/Downloads/vec-corpus-staging/`: 3 accounts × 4 consecutive months
  (A = Cuenta Priority checking, 2026-02..05; B = Visa credit card, 2026-03..06;
  C = Mastercard credit card, 2026-02..05). Plus 4 defect specimens
  (`defects/good.pdf`, `bad-math-cl21.pdf`, `bad-font-cl35.pdf`, `scanned.pdf`).
  Staging subfolder names are NOT reproduced here — see PII rule 3.
- Source manifest: `~/Downloads/statement-manifest.json` (per-file sha256, periods, dedup record).
- Anonymization map: `~/Downloads/vec-anonymization-map.json` — **KEEP OUT OF GIT**.
- **NOT landed:** owner-verified bank checklist CSV; CONDUSEF dictámenes (gold labels for law tier).
  → Law-tier gold-label calibration stays out of this epic's scope.

## Hard PII rules (from `scripts/veriqan-corpus/README.md` — non-negotiable)

1. No PDF from `~/Downloads/` is ever committed. Anonymized or not.
2. The mapping JSON never enters the repo.
3. In-repo artifacts (index, reports, test code, and THIS DOC) must use **neutral account labels
   (A/B/C)** — the staging folder names carry each real account's last-4 digits and must not be
   reproduced in committed files. (An earlier draft of this doc violated this; scrubbed 2026-07-22.)

## What "gold" means here (design decision)

There is no god's-eye manifest for real statements (contrast `prisma-domain-3-docs-unreliable`:
synthetic gold = generator manifest). For this corpus, calibration truth is:

- **Printed-document truth:** the operands/totals as physically printed. Extractor calibration =
  "did we read what is printed", verified by human/PdfPig cross-check during triage.
- **Presumption of compliance:** these are production statements a regulated bank actually issued.
  A hard RED on them is *presumptively* a false positive until triage proves the statement is
  genuinely defective. (YELLOW/abstention is acceptable honesty; silent false GREEN on the defect
  specimens is the symmetric failure.)

## Stories

| # | Story | Status | Gate |
|---|-------|--------|------|
| RC1.S1 | **Corpus gate + harness plumbing.** PII leak scan of all 16 staging PDFs against the map (zero real tokens); build out-of-repo `corpus-index.json` beside the PDFs (neutral labels, sha256, period, product type); env-gated test category (`Category=RealCorpus`, root via `VERIQAN_REAL_CORPUS_ROOT`, graceful skip when absent) wired into `Veriqan.Orchestration.Tests`. | DONE (`bd9dc3c3`; 242/242 corpus-present, 225+2skip corpus-absent; PII scan CLEAN) | none |
| RC1.S2 | **Baseline measurement run.** Full Veriqan verdict pipeline over the 12 statements + 4 defects; emit machine-readable per-statement report (verdict, per-check findings, per-field extraction coverage + confidence) + human `real-corpus-baseline-2026-07.md` (committed, PII-free). No assertions on outcomes — measurement first. | DONE (`01fbc30e`; 243/243 present, 225+3skip absent; B=Red×4, A/C=ExtractionGap×4) | S1 green |
| RC1.S3 | **Triage.** Classify every non-GREEN finding: true statement defect / extractor miscalibration / rule miscalibration / honest abstention. Also: do the 8 real credit-card statements make the C1/C2 synthetic-only confidence slices (Tasa/Cat/TotalRow) non-vacuous? Output = evidence table in this doc + tracker items. | DONE (evidence: `docs/qa/calibration/real-corpus-triage-2026-07.md`; NO true statement defects; C1/C2 non-vacuity = NO; defect-good == B-2026-04) | S2 report |
| RC1.S4 | **Evidence-gated fixes.** Owner authorized all 3 tracks 2026-07-22 + ruled: CL-46 → SAT "…DE UN CFDI" legend; PLATINUM catalog row in BOTH catalogs; **§26-i → tolerate as accepted variant**; **TYPO footnote markers → stay IN scope (fail stands — first owner-ratified true finding on real statements)**. Sub-stories: **S4.a rules DONE** (`c377788b`: CL-18 real fails 4→1, §26 4→0, CL-21 grounded-zero abstains; demo pin CL-21→CL-22, RED preserved — forced by evidence, labels absent from that PDF family) · S4.b extractor feeders (CL-31, CL-48, CL-50–53, LAW-SEC anchors+cross-ref guard) TODO · **S4.c data DONE** (`500e83a6`: SAT CFDI legend both catalogs — CL-46 B×4→Pass, C-2026-02 honest residual; TC-PLATINUM row both catalogs — C-series ExtractionGap→Red×4, real rules running; owner-TODO: PLATINUM annualCommission/rewards ungrounded) · **S4.d corpus DONE** (`4e28136a`+`1c81836e`, 3 rounds: auto-shrink, global-map leak fix (S1 gate caught it), boundary-aware placement + PyMuPDF spurious-hit guard; CL-28 artifact eliminated, LAW-TYPO ×4 retained as owner-ratified genuine finding). **Residual:** CL-18 still fails 1/4 (B-2026-06) — triage before S5 pins. | IN PROGRESS | S3 evidence |
| RC1.S5 | **Regression pin.** Pin the post-S4 calibrated expectations as an env-gated `[Theory]` over the corpus index (mirror of the synthetic `corpus-manifest.json` pattern; loud loader + completeness fact — no vacuous zero-row pass). | TODO | S4 done |

## Handoff — next session picks up here (2026-07-22, orchestrated session end)

**Done this session:** S1 (`bd9dc3c3`), S2 (`01fbc30e`), S3+adversarial gate (`6782937e`),
S4.d (`4e28136a`+`1c81836e`), S4.a (`c377788b`), S4.c (`500e83a6`). All pushed to `Liv`;
working tree clean; Orchestration 243/243 corpus-present, 225+3skip corpus-absent;
Validation 547; ReferenceData 46.

**Next: S4.b extractor feeders** (one story, biggest remaining): CL-31 pagination fallback
(kill/constrain page-wide "N de M" — currently matches MSI fragments), CL-48 image-aware blank
detection (`ComputeMaxVerticalGap` text-only), CL-50–53 fiscal-block recalibration (fast-path
requires the retired synthetic legend — align with the new SAT CFDI legend; + UUID line-wrap;
+ first/second-RFC assignment heuristic), LAW-SEC anchor recalibration to the real layout family
+ cross-reference-sentence guard. All in `PdfPigStatementFieldExtractor.cs` + Visual rules.
Warning: anchors are demo-calibrated — expect demo-pin movement; handle like S4.a (honest,
documented, evidence-forced only).

**Then S5 regression pin**, incorporating residuals: CL-18 1 fail B-2026-06 (untriaged),
CL-46 C-2026-02 (no legend in text layer), CL-32 + LAW-§27 (need visual/OCR confirmation —
unresolved since S3), LAW-TYPO ×4 = owner-ratified genuine finding (stays RED).

## Constraints (carry into every delegation)

- Test floors before any commit: Extraction **484** / Validation **532** / Orchestration **225** /
  Application **158**. Run test DLLs bare from `bin/<asm>/net10.0/` (not `Debug/`); MTP filter
  syntax is `-- --filter-not-trait`, never VSTest `--filter`.
- `Result<T>` + `CancellationToken` patterns per root CLAUDE.md; xUnit v3 + Shouldly + NSubstitute.
- CI must stay green with no corpus present (skip semantics like `Category=LiveOcr`).
- `calibration-report.md` regenerates as a test side effect — revert before commit unless the
  change is intentional.

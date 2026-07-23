# TRACKER — Veriqan RC1 residuals: visual/OCR track

**Opened:** 2026-07-23 · **Branch:** Liv · **Baseline:** `b69ae575` (RC1 closed, S5 pins green)
**Owner ruling:** owner selected this epic 2026-07-23 (RC1 residuals visual/OCR track).

## Intended solution (spec)

Resolve the three image-blind residuals pinned as-is by RC1.S5, **measure-first**:

- **R1 — CL-48**: ~211pt "truly-empty" region ×8 (all real credit-card statements). Hypothesis:
  vector-drawn content invisible to PdfPig word+image geometry (`ComputeMaxVerticalGap`,
  `PdfPigStatementFieldExtractor.cs:3699`, uses words + `page.GetImages()` only; `page.Paths`
  used nowhere). Determine per-doc what actually occupies the region.
- **R2 — CL-32 + LAW-§27**: "COMPARA TU TARJETA" heading + §27 GLOSARIO are image-rendered;
  rules are text-layer-only (`Cl32ComparaTuTarjetaRule` Contains; `Section27GlosarioRule`
  gated on `DetectedSection`). Confirm visually/OCR whether the content exists on the page.
- **R3 — LAW-SEC**: all 23 §-anchors graphic-rendered on the real family → PRESENCE honest
  Fail×8, ORDER-GAP InsufficientData×8, §26 InsufficientData. Establish which headings are
  actually visually present per statement.

**Decision gate after S1:** classify each residual as (a) real statement defect → pin stays,
(b) tooling blind spot fixable in text/geometry domain (e.g. vector-path-aware gap), or
(c) requires production OCR/raster escalation → **owner checkpoint before building (c)**.

## Key facts (from re-ground 2026-07-23)

- Corpus: `~/Downloads/vec-corpus-staging` (`VERIQAN_REAL_CORPUS_ROOT`), `corpus-index.json`,
  16 files; findings-bearing = B-2026-03..06 (visa) + C-2026-02..05 (mc) + 3 defect specimens.
- Pins: `Veriqan.Orchestration.Tests/Calibration/RealCorpus/RealCorpusCalibrationPinTests.cs`.
- Existing raster/OCR: PDFtoImage 150dpi (header crop, fiscal page; 300 escalated),
  `TesseractHeaderProductOcrEngine` (spa, accepts any PNG), ZXing QR ladder.
- Floors to preserve: Extraction 503 / Validation 553 / Orch corpus-present 260 / App 158 / RefData 46.
- PII discipline: renders/crops stay in scratchpad, never committed; no verbatim statement
  content in docs beyond check-relevant fragments.

## Stories

| ID | Story | Status |
|----|-------|--------|
| S0 | Re-ground: code/corpus map (Explore) | DONE 2026-07-23 |
| S1 | Ground-truth probe: PyMuPDF gap+drawings analysis of CL-48 regions; render pages; OCR/visual sweep for 23 anchors + CL-32 + §26/§27 across B/C×8 | DONE 2026-07-23 — see findings |
| S2 | Triage findings → per-residual classification; owner checkpoint | DONE 2026-07-23 — owner rulings below |
| S3.1 | CL-48 footer-aware gap fix + pin updates | DONE `04a6c67c` — CL-48 Fail×8→Pass×8, no other real-corpus movement; compliant-master BankTier Yellow→Green (CL-48 was its sole Bank-tier fail); Extraction 506, Validation 553, Orch 260, build 0/0 |
| S3.2 | OCR §-anchor escalation ladder (R2/R3) + pin updates | DONE 2026-07-23 — `SectionAnchorOcrEscalationStage` (fires only when text layer finds <2 present sections; reuses E7 Tesseract singleton + same anchor table/heading-shape gate; page-hint locators only, never fabricated geometry; SectionGaps untouched → ORDER-GAP stays honestly abstained). CL-32 reworked to consult §11 detection first (raw-text fallback kept). Measured movement: CL-32 Fail×8→Pass×8, LAW-§26-NOTAS →Pass×8, honest new Fails LAW-§11-URLS/§17-LEGENDS/§27-GLOSARIO ×8 (verbatim content genuinely absent — owner-review queue per S2 ruling), BankTier Yellow→Green where CL-32 was sole bank fail (B×4, C-03/04/05, defect-good, defect-bad-math). Extraction cost ~1.8s→10–22s/doc on raster-headed family. Verified: Extraction 521, Validation 561, Orch 260, App 158, RefData 46; Worker+Web.UI build 0/0 |
| S3.3 | S4-Blocker fix: verbatim-content rules abstain (InsufficientData) instead of Fail when host-section presence is OCR-sourced and the text-layer content scan finds nothing (text-layer scan cannot prove absence in a raster region) | DONE 2026-07-23 — guard applied to ALL FIVE verbatim rules: §11-URLS/§17-LEGENDS/§27-GLOSARIO (the observed false Criticals), §24-QUEJAS (latent, same shape — dev-found), §26-NOTAS (latent, same shape — orchestrator-closed; currently Passes on corpus). §23-STATUS needs no guard (empty-SectionText abstain already fires first on OCR-sourced sections); §23-ABONO-LINK needs none (cross-references structured DisputeRows/Movements, not text-layer content). Positive text-layer match still Passes regardless of source; TextLayer-sourced behavior unchanged. Pins corrected (§11/§17/§27 → InsufficientData; §24=Pass added; §23 5-Pass/3-abstain mix documented); baseline doc corrected + dated correction note; stale demo E2E doc-comments fixed. Verified: Validation 571 (+10 guard tests), Orchestration 260 (real corpus), builds 0/0 |
| S4 | Adversarial review vs this spec; close epic | REVIEW DONE 2026-07-23 — **BLOCKER found** (→ S3.3): the 24 new Critical Fails (LAW-§11-URLS/§17-LEGENDS/§27-GLOSARIO ×8) shipped in `41350630` are FALSE non-compliance — S1 probe OCR text (`s1-probe/ocr/B-2026-03-p1/p2/p5.txt`) shows the exact mandated URLs, all 4 §17 legends, and most glosario terms ARE legibly present on the raster-rendered pages; only the text layer lacks them. Content rules scan text-layer only while presence is now OCR-sourced. UPHELD: no-op reference-preserving guarantee, geometry honesty (ORDER-GAP still abstains), anchor-index alignment, CL-32 fallback, LiveOcr tagging (stage tests fully mocked), PII discipline, non-tautological tests. MEDIUM findings: (F2) LAW-§23-ABONO-LINK/STATUS (→5 Pass/3 abstain) + LAW-§24-QUEJAS (→Pass×8) moved undisclosed in `41350630` — legit side effects, now to be pinned/documented in S3.3; (F3) footer-gap exclusion is unbounded (a 1-content-block page passes CL-48 regardless of gap size) — covered by the S2 owner ruling for THIS family but generalizes to future docs; recorded as accepted residual risk. OWNER FOLLOW-UP (not built, owner-gated): content-level OCR verification for §11/§17/§27 — probe evidence suggests these could become honest PASSES, not just abstains. S3.3 verified green → **EPIC CLOSED 2026-07-23**. |

## S1 findings (ground truth, verified 2026-07-23; orchestrator eyeballed key renders)

- **R1 (CL-48): BOTH hypotheses refuted — the regions are GENUINELY blank.** All 26 gaps
  >56.7pt across the 8 B/C docs contain no hidden vector content (only tiny border-bleed
  fills, <0.5% coverage) and no invisible text. Mechanism: every gap's lower bound ≈746.5pt
  = the raster footer (logo image y746.5–767.5 + form-code text line) on 792pt pages, so
  trailing whitespace between the last content box and the footer counts as an intra-page
  gap. The ~211pt canonical case (B-2026-04 p3, y535.5–746.6) is blank space below the
  ATENCIÓN DE QUEJAS box. Large gaps (400–640pt) are near-empty continuation pages
  (address-only p11; image-rendered Aviso de Privacidad p6). Layout artifact of the genuine
  family — every authentic statement fails CL-48 as calibrated.
- **R2/R3 (headings): raster-image-rendered, OCR-recoverable.** Tesseract spa @150dpi finds
  18–20 of 27 non-empty anchors per doc; **COMPARA TU TARJETA on p1 of all 8 docs**;
  GLOSARIO + NOTAS ACLARATORIAS section headings on the late body pages of all 8. C# text
  layer: 0 anchors (confirmed; the 6 text-layer anchor strings present are lowercase prose
  cross-references the heading-shape gate correctly rejects — no invisible-text tricks).
  **~7 anchors genuinely absent/reworded in the real layout** (§3, §4, §14, §15 "NUMERO DE
  CUENTA" vs printed "Número de tarjeta", §21/§25/§28 conditional; §10/§23 wording drift) —
  real layout divergence, not OCR failure.
- Artifacts (not committed): `scratchpad/s1-probe/` — gap_probe.json, ocr_sweep.json,
  26 gap crops, 80 page renders + OCR texts, probe.py.

## S2 owner rulings (2026-07-23)

- **R1 → footer-aware gap:** redefine the max-vertical-gap to run between CONTENT blocks
  only — a gap whose lower boundary is the page-footer zone (bottom band holding the logo
  raster + form-code line) is trailing whitespace, excluded. Whole-blank-page branch stays.
  Expected movement: CL-48 Fail×8 → Pass×8; internal between-content gaps still fail.
- **R2/R3 → build OCR escalation ladder:** extend existing machinery (PDFtoImage 150dpi +
  Tesseract spa singleton from E7) into a section-anchor OCR stage firing only when
  text-layer detection finds ~0 sections. Heading checks evaluate the rendered page;
  wording-divergent anchors surface as honest findings for owner review.

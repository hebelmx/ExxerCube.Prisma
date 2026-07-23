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
| S3.1 | CL-48 footer-aware gap fix + pin updates | PENDING |
| S3.2 | OCR §-anchor escalation ladder (R2/R3) + pin updates | PENDING (after S3.1 verified) |
| S4 | Adversarial review vs this spec; close epic | PENDING |

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

# RC.1 — Requirement→Evidence Trace · EXTRACTION cluster (Veriqan VEC)

**Date:** 2026-06-18 · **Branch:** `Liv` · **Auditor:** Claude Code (RC.1 extraction-cluster agent, read-only)
**Anchor:** `docs/planning-artifacts/READINESS-CHALLENGE-BRIEF-2026-06-18.md` (§2 dimensions, §1/§4 the bar)
**Builds on:** RC0a (scope register) + RC0b (reality map). Ground truth = wiring + file:line, not comments.
**Bar:** end-to-end evidence on a *real* (non-synthetic) statement, NOT a green unit test.

Production code inspected:
- `02 Infrastructure/Veriqan.Infrastructure.Extraction/PdfPigStatementFieldExtractor.cs` (4488 lines — the sole real adapter)
- `01 Core/Veriqan.Domain/Extraction/*` (StatementModel, ExtractedField, FieldLocator, FinancialTable, TableCell, FontUsage, DetectedSection, etc.)
- Tests: `08 Tests/02 Infrastructure/Veriqan.Infrastructure.Extraction.Tests/*` (14 test files)
- Wiring: `02 Infrastructure/.../DependencyInjection/VeriqanExtractionExtensions.cs`; consumer `03 Orchestration/Veriqan.Orchestration/Pipeline/VerificationPipeline.cs:146`

---

## Cross-cutting facts (apply to every row below)

1. **The extractor IS wired into the real pipeline.** `IStatementFieldExtractor → PdfPigStatementFieldExtractor` (Singleton) registered in `VeriqanExtractionExtensions.cs:23`; `VerificationPipeline.cs:146` calls `_extractor.ExtractFullAsync(submission.Pdf, ct)`. BUT the pipeline itself is only reachable from test harnesses — the Veriqan Worker has **no statement-submission entry point** (RC0b Part 1). So extraction is **wired but not deployable-reachable**: nothing in production can hand it a PDF.

2. **Extraction runs on REAL PDF bytes, but only SYNTHETIC PDFs.** All fixture-driven tests (`PdfPigStatementFieldExtractorTests.cs`, `...PeriodTests.cs`, `...ResumenTests.cs`, `...MovementsTests.cs`, `FinancialTableExtractionTests.cs`, etc.) call the real `ExtractFullAsync` against the 3 `Prisma/Fixtures/PRP2/*.pdf` Dummie VEC files. These are **`KnownSynthetic`, non-compliant** (corpus-manifest.json; 12–14 format defects each). **Zero real CONDUSEF statements exist in the repo.** Validation/Visual tests (468 + Visual) use hand-built `StatementModel` objects and do not touch the extractor at all.

3. **Coordinates are hardcoded to "fixture #1".** Header Y-bands (`HeaderYMin=530`, `LabelColumnXMin=285`, `ValueColumnXMin=410`, `ClientNameYMin=630`), DESGLOSE column X-ranges (`DesgloseDescriptionXMin=158…XMax=422`, sign `423–438`), §19/§20/§8 column X-ranges, and the layout comment block (`PdfPigStatementFieldExtractor.cs:46-62`) are **empirically calibrated to the Dummie VEC fixture geometry**. A real bank's statement with different page layout would silently mis-associate or miss fields. This is the dominant real-data risk.

4. **"Confidence scores" are NOT real scores — they are 3-valued constants.** `ExtractedField<T>.Found` hardcodes `Confidence = 1.0`; `InvalidFormat = 0.7`; `Missing = 0.0` (`ExtractedField.cs:69-83`). `TableCell` is identical (`TableCell.cs:30-59`). There is no measured/probabilistic confidence. The "low-confidence → InsufficientData/abstain" promise (NFR-8, E9.5) is therefore driven by a binary found/not-found signal, not a calibrated threshold. Locators ARE real (computed PdfPig bounding boxes), except `Missing`/`NotFound` cases which return a page-hint only.

5. **Locators are real** where a value is found: `BoundingBoxOf()` (`:1908`) computes min/max over PdfPig word bounding boxes → real page + region. Absent fields get `FieldLocator.PageHint(1)` (page-only) or `NoPage()`.

---

## Requirement → Evidence table

| Requirement-ID | Intent (1 line) | Built-state class | Evidence (file:line / test / "none") | Gap to E2E-readiness (1 line) |
|---|---|---|---|---|
| **FR-4** | Header identity fields (name, address, branch, card#, CLABE, client#, RFC) with confidence + page/region locator; missing = `not-extracted`. | **Partial** | `ExtractFullAsync`/`ExtractHeaderOnly` `:196-207, :333-351`; card 16-digit `:1809`, CLABE 18-digit `:1820`, RFC pattern `:1831`; tests `PdfPigStatementFieldExtractorTests.cs` (real extraction on 3 synthetic PDFs, asserts CLABE→`ExtractedInvalidFormat` for the synthetic 13-digit value). | Proven only on synthetic fixtures; confidence is a constant not a score; positional name/address heuristics (`ClientNameXMin/YMin`) are fixture-geometry-locked. |
| **FR-5** | Period/summary fields (product, rate, CAT, dates, day-count, summary amounts); day-count = span; rate matched vs bundle TASA. | **Partial** | `ExtractPeriodSummary` `:363-458`; day-count `DayCountVerification.Compute` `:433`; TASA/CAT `:788-874`; tests `...PeriodTests.cs` assert exact dates/day-count on fixture #1. | TASA *match-vs-bundle* is a downstream rule, not in extractor; all label/X-column guards (`Left>100`, `maxX:300`) are fixture-tuned; footnote-marker stripping is fixture-specific ("spurious 1/2 tokens"). |
| **FR-9 input** (font dictionary) | Read embedded font family per text run from the PDF font dictionary (deterministic). | **Real-unwired→pipeline** | `ExtractFontRuns` `:1955-2011` reads `letter.FontName`, strips `ABCDEF+` subset prefix + style suffix → `FontUsage` per (family,page); `FontExtractionStatus.NotFound` when no letters. Tests `FontExtractionSmokeTests.cs`. | Genuinely deterministic + real; but only validated on synthetic Aptos-embedded fixtures — no real statement with mixed/subset-embedded fonts tested. |
| **FR-10 input** (glyph boxes / header style) | Overlapping-text glyph-box intersection + section header bold/uppercase facts. | **Partial** | Overlap `ExtractTextOverlapIncidents` `:2177-2231` (2 pt epsilon, consecutive-pair scan); header style `ExtractSectionHeaderStyles` `:2285-2362` (bold via `FontName.Contains("Bold")`, uppercase via matched-phrase). Tests `VisualFactsExtractionSmokeTests.cs`. | Bold = substring("Bold") heuristic (fails on subset-mangled names → should abstain but reports false); header phrase list `:2246-2254` is a fixed 6-phrase allowlist, not the 28 legal sections. |
| **FR-11 input** (pages / per-page facts) | Pagination, blank pages, logo + card# per page. | **Partial** | `ExtractPageInspectionFacts` `:2485-2583`: pagination "N de M" regex `:2469`, `ImageCount` as logo proxy `:2510`, card-digits contains-check `:2517-2528`. Tests `PageInspectionFactsSmokeTests.cs`. | Logo presence = `GetImages().Count()` (any image, not the *bank* logo); blank-page = word-count==0 only; proven on synthetic layout. |
| **FR-12 input** (catalog images) | Perceptual-hash image presence vs catalog. | **Missing (in extractor)** | No pHash/image-content extraction in `PdfPigStatementFieldExtractor`; only `ImageCount` is captured. AR-5 names a perceptual-hash package but no extractor code uses it. | Image-presence-by-pHash extraction not built in this adapter; cannot feed FR-12 beyond raw image counts. |
| **FR-13 input** (legends / sections text) | Normalized full text for mandatory-legend + "COMPARA TU TARJETA" presence. | **Real** | `BuildNormalizedFullText` `:2814-2837` (concat all page words → `VecTextNormalizer.Normalize`); §-anchor "COMPARA TU TARJETA" in section table `:2930`. Tests `NormalizedFullTextSmokeTests.cs`. | Solid + deterministic; presence-matching quality unproven on real legend typography/wrapping. |
| **FR-14 input** (fiscal block: QR, code, RFCs) | Extract QR + fiscal code + issuer/receiver RFC when present. | **Partial** | `ExtractFiscalBlock` `:2636-2782`: legend-gated, PDFtoImage render @150 DPI + ZXing QR decode `:2713-2747`, UUID `:2617` + RFC `:2609` regex from page text; `FiscalBlock.NotPresent()` when legend absent. Tests `...FiscalTests.cs`. | Real CV/QR path (genuine) but issuer/receiver = "first RFC, second RFC" ordering assumption `:2690-2696`; only synthetic QR tested; render dependency (PDFtoImage/Skia) untested at volume. |
| **FR-25 input** (DESGLOSE movements) | Reconstruct transaction rows (dates, description, sign, amount) + printed totals. | **Partial** | `ExtractMovements` `:1130-1229`, row parse `:1314-1405`, totals `:1240-1308`, FX-continuation folding `:1182-1199`, truncated-year repair `:1453-1465`. Tests `...MovementsTests.cs` / `...TotalsTests.cs`. | All column X-ranges + sign tokens + "Total cargos/abonos" recognition are fixture-calibrated; truncated-date repair guesses last year digit. |
| **FR-28 / E10.1 input** (§1–28 section map) | Locate all 28 CONDUSEF sections (page+region) or report missing; conditional §16/23/25 NA; §1 indeterminate. | **Partial** | `ExtractDetectedSections` `:2992-3107` + 28-row anchor table `:2904-2955` (band-only detection; §1 logo `Indeterminate`; §16/21/23/25/28 `IsConditional`). Tests `SectionDetectionSmokeTests.cs`. | Anchors are normalized phrase substrings tuned against the Dummie fixtures (comments `:2877-2901` document false-positive fights); real-statement heading variants untested → silent miss/mis-order risk. |
| **E10.2 input** (section gaps) | Inter-section vertical blank gap geometry. | **Partial** | `ComputeSectionGaps` (called `:253`); per-page `Width/Height` captured `:2552-2553`. Tests `SectionGapExtractionSmokeTests.cs`. | Geometry real but depends entirely on correct section detection (above) which is fixture-locked. |
| **FR-29..33 / E11.1 input** (§6/§8/§16/§19/§20 financial grids) | Reconstruct grids into typed rows+cells w/ per-cell confidence; unresolvable → InsufficientData (not guessed rows). | **Partial (corpus-gated)** | `ExtractFinancialTables` `:3364-3380` → §8 `:3393`, §19 `:3498`, §20, §16, §6; `TableExtractionStatus.{Extracted,NoRowsParsed,SectionNotFound,Indeterminate}` (`TableExtractionStatus.cs`); abstain path `FinancialTable.Indeterminate` on exception `:3483`. Tests `FinancialTableExtractionTests.cs` assert *structure only* (row counts, NA handling), explicitly "not regulatory compliance" `:14-17`. | Per-cell "confidence" is a 1.0/0.7/0.0 constant (`TableCell.cs`), NOT measured; column X-ranges hardcoded; E11.1 demands "column/row association validated against ground-truth corpus fixtures" — **corpus absent**, so association correctness is unproven. |
| **FR-36 / E12.1-2 input** (typography samples) | Per-word rendered point-size + font name/weight for legal-floor + bold checks; indeterminate weight → abstain. | **Partial (corpus-gated)** | `ExtractTypographySamples` `:2036-2090`: `letter.PointSize` + `IsBold = FontName.Contains("bold")`; `TypographyExtractionStatus.NotFound` when no words. Tests `TypographyExtractionSmokeTests.cs`. | Bold = substring heuristic (can't distinguish subset-mangled → should be Indeterminate but isn't at extractor level); point-size thresholds uncalibrated against a real corpus. |
| **E3.1-AC1** (header acceptance) | card#16 / CLABE18 / RFC pattern + confidence + locator + `not-extracted` hint. | **Partial** | Validators `:1809-1840`; `ExtractedField.InvalidFormat`/`Missing` carry locator. Tests assert `ExtractedInvalidFormat` for the 13-digit synthetic CLABE. | Met on synthetic data; AC's "confidence score" satisfied only nominally (constant). |
| **E3.2-AC1** (period acceptance) | start/cut dates + day-count == span + rate matched vs bundle TASA. | **Partial** | `:493-523`, `:575-613`, `DayCountVerification` `:433`; TASA-vs-bundle is in Validation rules (not extractor). Tests `...PeriodTests.cs`. | Extractor side proven on synthetic only; the "matched against bundle" half lives in Validation cluster. |
| **NFR-5** (determinism) for extraction | Identical PDF input → identical extracted model. | **Real** | Pure PdfPig reads + deterministic LINQ ordering (`OrderByDescending(Bottom).ThenBy(Left)` etc.); no RNG/time except `RepairTruncatedDate` fallback `DateTimeOffset.UtcNow.Year` `:1360` (non-deterministic edge). | One non-deterministic seam: truncated-date year-repair falls back to current UTC year when no operation-date context. |
| **NFR-8 / E9.5** (abstain-safety) extraction inputs | Low-confidence field → InsufficientData, never false FAIL. | **Partial** | Status enums (`ExtractionStatus`, `MovementsExtractionStatus`, `TableExtractionStatus`, `Font/TypographyExtractionStatus`) all carry NotFound/Indeterminate; rules consume these. | The "low-confidence" trigger is binary (found vs not), not a real confidence band → a *wrongly-extracted* value reports `Extracted`@1.0 and can drive a false FAIL on real-statement layout drift. |
| **PG-17** (malformed PDF degrades) extraction | Corrupt/partial PDF → graceful, no crash. | **Real (partial)** | `ExtractFullAsync` wraps in try/catch → `Result.WithFailure` `:321-326`; empty bytes guarded `:184`; per-page failures swallowed (e.g. `:2566`, `:2830`). | Never stress-tested on genuinely malformed/encrypted/image-only PDFs; "swallow per-page" can silently under-extract without surfacing degradation. |

---

## Per-class tally (extraction cluster, 17 rows)

| Class | Count | Rows |
|---|---|---|
| Real+Wired+E2E | **0** | — (nothing is proven E2E on a real statement; Worker has no entry point) |
| Real-unwired | 1 | FR-9 font input (real+deterministic; reachable only via test/pipeline, not deployable) |
| Real | 3 | FR-13 normalized text; NFR-5 determinism; PG-17 (graceful-fail) |
| Partial | 12 | FR-4, FR-5, FR-10, FR-11, FR-14, FR-25, FR-28/E10.1, E10.2, FR-29..33/E11.1, FR-36/E12, E3.1, E3.2, NFR-8 |
| Stub | 0 | — |
| Missing | 1 | FR-12 (catalog-image pHash extraction — only raw image counts captured) |
| Unknown | 0 | — |

(FR-13, NFR-5, PG-17 grouped under "Real"; counts sum to 17 distinct requirement rows.)

---

## Biggest readiness gaps in this cluster (≤5)

- **No proof on real input.** Every extraction path is exercised only against the 3 `KnownSynthetic` Dummie VEC PDFs (`Prisma/Fixtures/PRP2/`). Zero real CONDUSEF statements exist; the calibration corpus is absent. **No extraction requirement clears the "real-input" bar.**
- **Geometry is hardcoded to fixture #1.** Header/DESGLOSE/§19/§20/§8 X-Y constants (`PdfPigStatementFieldExtractor.cs:80-91, :1084-1092, :3297-3327`) are empirically tuned to the synthetic layout. A real bank layout would silently mis-associate fields — and report them as `Extracted`@confidence 1.0.
- **Confidence is a constant, not a score.** `ExtractedField.cs:69-83` and `TableCell.cs:30-59` return 1.0/0.7/0.0. The NFR-8/E9.5 "abstain on low confidence" guarantee rests on a binary found/not-found signal, so a *wrongly* extracted value cannot self-flag → false-FAIL risk on real-statement drift.
- **Section + table detection is allowlist/substring matching.** §1–28 anchors (`:2904-2955`) and the 6-phrase header list (`:2246-2254`) are normalized substrings fought against fixture false-positives (see comments `:2877-2901`); E11.1's required "validated against ground-truth corpus fixtures" is unmet because the corpus is missing.
- **Extraction is wired but unreachable.** `ExtractFullAsync` is consumed by `VerificationPipeline.cs:146`, but the Worker exposes no submission endpoint (RC0b) — so even the synthetic-proven path has no deployable surface for a real statement to enter.

---

*This trace covers the EXTRACTION cluster only (FR-4, FR-5, and the extraction inputs feeding FR-9..14 / FR-25 / FR-28 / E10 / E11 / E12). Validation-rule behaviour, verdict aggregation, persistence, ingestion, and reporting are other clusters' scope.*

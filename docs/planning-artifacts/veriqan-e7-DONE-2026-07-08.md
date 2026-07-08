# Veriqan Epic E7 (Real Product Resolution) — CLOSED 2026-07-08

**Branch:** `Liv`. **Status:** DONE + adversarially reviewed + verified from ground truth.
**Design authority:** [`veriqan-e7-s72-header-ocr-design-2026-07-08.md`](./veriqan-e7-s72-header-ocr-design-2026-07-08.md).

E7 replaced the load-bearing `TC-BSSB` card-number **alias hack** (`ExtractProductName` returning
the wrong `"Número de tarjeta 4111…"` token, resolved only because the demo catalog aliased it) with
a **real OCR read** of the product-name header image (`"Tarjeta de Crédito COSTCO BANAMEX"`,
rendered-glyph-only on `good.pdf`), resolved against a catalog row that actually names the product.

## What shipped (commit trail on `Liv`)

| Commit | Story | Content |
|--------|-------|---------|
| `34c8a46c` | S7.2/S7.3 | Header-image OCR product resolution slice (atomic): `HeaderImageOcrStage` behind the escalation ladder, `TesseractHeaderProductOcrEngine` (lazy singleton, semaphore, crop-hash cache), `ExtractProductName` neuter, `ProductResolver` FuzzySharp fallback, provenance-aware coverage floor, `TC-COSTCO-BANAMEX` catalog row, Worker Dockerfile natives. |
| `c70a8da4` | S7.2/S7.3 | Adversarial-review **finding #1**: T1 gated (Tesseract-free) OCR-snapshot regression net. |
| `17625098` | S7.2/S7.3 | Epic-boundary adversarial-review **findings F2–F7 + M3** (this session). |
| `0e20334f` | **S7.1** | Synthetic header-raster OCR fixture (`s71-header-ocr-costco`) + LiveOcr proof. |

## Ground-truth verification (this session, native Tesseract 5.5.0 present)

- `dotnet build` — 0 errors / 0 warnings.
- `Veriqan.Application.Tests` — **158/158** (fuzzy resolver + 2 new ambiguity tests).
- `Veriqan.Infrastructure.Extraction.Tests` — **304/304** (OCR stage, T1 gated snapshot, cache, s71 word-geometry `[Theory]` + s71 LiveOcr `[Fact]`, corpus completeness `[Fact]` at 13 specimens).
- `Veriqan.Orchestration.Tests` — **129/129** (5-verdict `VecChecklistDemoE2E` bar unchanged: good→Red LAW-SEC-PRESENCE, bad-math→Red CL-21/22, bad-font→Red CL-35, scanned→ExtractionGap, compliant-master→Red LAW-SEC-PRESENCE).
- **F5 gate-bite proven**: mutating `HeaderCropFraction` 0.30→0.10 turned the *gated* (Tesseract-free) geometry test red (495≠165px), then reverted.
- **S7.1 make-or-break proven**: the fitz-rasterized banner → C# re-render → Tesseract round-trip reads `COSTCO BANAMEX` cleanly and resolves `TC-COSTCO-BANAMEX`.

## Adversarial review (epic boundary) — outcome

Two parallel skeptics (completeness auditor + correctness/honesty). Completeness verdict: **COMPLETE**
(all 13 design tasks + 4 owner rulings git-verified, wired, green). 6 actionable findings fixed
(`17625098`):
- **F2** native `TesseractEngine` leak — 2 sibling E2E classes built the root provider with bare `var sp`; now `await using` (SIGSEGV/leak class).
- **F5** T1 geometry test was tautological (expected derived from the constant under test); pinned to hardcoded 1275×495px literals → now bites on crop-fraction/DPI regression without native Tesseract.
- **F3** fuzzy resolver could nearest-guess between two near-named products both ≥85; added ambiguity-abstain guard (`FuzzyAmbiguityMargin = 5`).
- **F6** unbounded OCR cache on a process-lifetime singleton → bounded at `MaxEntries = 256`.
- **F7** cancellation mid-OCR threw `OperationCanceledException` → now `Cancelled<string>()` per CLAUDE.md.
- **M3** stale retired-alias-hack diagnostic string.

## ⚠️ DEFERRED items — RESOLVED in the 2026-07-08 close pass

All four deferred items were triaged from ground truth and dispositioned (commits `51370580` M2,
`146eb4da` F1/M1 on `Liv`):

1. **F3 ambiguity margin (`FuzzyAmbiguityMargin = 5`)** — ✅ **RATIFIED as-is (owner, 2026-07-08).**
   `FuzzyAmbiguityMargin = 5` and `FuzzyScoreThreshold = 85` are accepted for the current 2-row catalog.
   No code changed. Re-open with the token-set-metric lever (the whole-string `Fuzz.Ratio` dilutes the
   discriminating brand suffix under the shared `"TARJETA DE CRÉDITO "` prefix) only when near-named
   catalog rows are added.
2. **F4 — masked-card under-neuter (LATENT).** ⏸️ **DEFERRED — confirmed no fixture evidence
   (owner, 2026-07-08).** Verified from ground truth: NO masked-card pattern (`****`/bullet/`X`-runs)
   exists in ANY Veriqan PRP2 fixture or synthetic specimen — every card number is unmasked 16-digit
   (`4111000000070001`). F4's failure scenario is hypothetical against held data, so `IsCardNumberBand`
   (`digitCount ≥ 8`) is left as-is rather than writing speculative masking-detection code. Revisit when a
   real masked statement enters the corpus; the fix (abstain-safe masking-char guard + synthetic fixture)
   is understood and small. Abstain-safe today (no fabrication).
3. **F1 / M1 — LiveOcr test architecture.** ✅ **CLOSED (`146eb4da`).** Five classes that construct the
   real native `TesseractHeaderProductOcrEngine` are now tagged `[Trait("Category","LiveOcr")]`
   (`VecChecklistDemoE2ETests`, `SyntheticDefectVerdictE2ETests`, `EscalationSeamBehaviorNeutralTests`,
   `EscalatingExtractorRebuildPathTests`, `PaymentDueDateFuzzyRecoveryCanaryTests`) — the earlier count of
   "2 tagged" left the rest exposed. `HeaderImageOcrStageSnapshotTests` (T1) stays trait-free by design.
   Two borderline classes were verified and deliberately left untagged (no native OCR at runtime):
   `VerificationPipelineEndToEndTests` (Windows-path fixture absent on CI → `File.Exists` early-return) and
   `CalibrationDriverTests` (its 3 seed-corpus PDFs carry a text-layer product heading → stage-1 resolves →
   `StatusGate` never escalates to OCR). CI (`quality-gates.yml`) now excludes `Category=LiveOcr` via the
   correct **MTP** switch `-- --filter-not-trait "Category=LiveOcr"` (this repo runs xUnit v3 on
   Microsoft.Testing.Platform, so the legacy VSTest `--filter "Category=..."` form is not honored; the
   broken E2E `--filter` was fixed to `-- --filter-trait "Category=E2E"` in the same pass). Proven by count:
   Extraction 304→282 (22 excluded), Orchestration 129→118 (11 excluded), both 0 failed / build 0/0.
4. **M2 — duplicate `products.csv`.** ✅ **CLOSED (`51370580`).** The durable/prod copy at
   `Prisma/Data/Veriqan/reference-bundles/Demo_Bank_(Iqubica)/products.csv` (mounted into the containerized
   service by `docker-compose.veriqan.yml:147`) lacked the `TC-COSTCO-BANAMEX` row — a real Docker-path
   demo breaker (product would fall to `UnknownProduct`/`ExtractionGap`), not merely cosmetic. Synced the
   missing row to match the fixture copy.

## Determinism note (carry forward)

PyMuPDF PDF serialization is **not byte-deterministic** — regenerating the corpus churns raw PDF bytes
while word-geometry stays stable. This is by design: the S6.2.6 corpus gates on **word-geometry via
PdfPig round-trip**, not byte hashes, and ships `--write-index` to update the index without PDF churn.
Do **not** run `--profile all` expecting an empty `git diff` on the PDFs; verify via the tests, not
`sha256sum`. Manifest + `corpus-manifest.json` ARE byte-stable.

## Next epic

E7 is closed. Next is an owner-gated pick among the remaining Veriqan tranches (the synthetic corpus is
now the measurement harness, extended by S7.1 to cover header-image OCR). No E7 follow-on work is queued
beyond the deferred owner decisions above.

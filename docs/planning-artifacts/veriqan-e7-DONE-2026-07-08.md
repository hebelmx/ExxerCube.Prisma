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

## ⚠️ DEFERRED — owner decisions still open (NOT silently resolved)

1. **F3 ambiguity margin (`FuzzyAmbiguityMargin = 5`)** — new calibration constant, chosen conservatively
   to honor owner ruling 2 ("abstain, never nearest-guess"). Safe for the current 2-row catalog; **needs
   owner ratification** if/when near-named catalog rows are added. The whole-string `Fuzz.Ratio` also
   dilutes the discriminating brand suffix under the shared `"TARJETA DE CRÉDITO "` prefix — a token-set
   metric is the lever if this ever bites.
2. **F4 — masked-card under-neuter (LATENT, real-corpus).** `IsCardNumberBand` uses `digitCount ≥ 8`.
   A masked band like `**** **** **** 0001` (4 visible digits) is NOT seen as a card band → positional
   `ExtractProductName` returns `Found` → `StatusGate` never fires → OCR recovery is bypassed →
   `UnknownProduct`/`ExtractionGap`. Abstain-safe (no fabrication) but a functional gap for real masked
   statements. Demo unaffected (unmasked 16-digit). Needs real-corpus ground truth to calibrate.
3. **F1 / M1 — LiveOcr test architecture.** The 5-verdict demo bar + 2 escalation-seam tests + the s71
   LiveOcr test now exercise native OCR. They **fail loud** (honesty-safe) on a Tesseract-less lane rather
   than skip. Convention: CI must exclude `Category=LiveOcr` (and provision native Tesseract for the lanes
   that keep them). The design's §6 "T1 is the only Tesseract-free gate" holds only for
   `HeaderImageOcrStageSnapshotTests`.
4. **M2 — duplicate `products.csv`.** A stale second copy at
   `Prisma/Data/Veriqan/reference-bundles/Demo_Bank_(Iqubica)/products.csv` (mounted by the durable/prod
   docker service) lacks the `TC-COSTCO-BANAMEX` row. Pre-existing divergence, out of E7's slice scope;
   flag if the durable-service path matters for the demo.

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

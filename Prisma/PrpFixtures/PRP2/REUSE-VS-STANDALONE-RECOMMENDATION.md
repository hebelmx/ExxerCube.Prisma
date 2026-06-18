# VEC Bank-Statement Verifier — Reuse vs. Standalone: Analysis & Recommendation

**Date:** 2026-06-16
**Branch:** Liv
**Author:** analysis session (Claude) for hebelmx
**Question:** Can the bank-statement verifier (PRP2 / "VEC") be built on the existing
ExxerCube.Prisma codebase, reusing as much as possible — or is it better as a standalone
solution?

---

## Bottom line (TL;DR)

**Build VEC as a MODULE (brownfield extension) of ExxerCube.Prisma — not standalone.**
The two products share the same core competency (*extract fields from documents → cross-validate →
report*), and ~50–60% of the existing infrastructure is genuinely reusable after independent
verification. A standalone product would mean re-implementing OCR, imaging, extraction, export,
persistence, eventing and Python interop for no benefit.

**However:** treat the *existing* VEC code as an early **prototype (~5–15% real)**, NOT the
"95% production-ready" the summary claims. The architecture decision is sound; the current
implementation is mostly non-functional scaffolding.

**And:** lead with a **deterministic-first MVP** (arithmetic validation + embedded-font/geometry
checks via `pdfplumber`/Shapely) and defer the heavy, fragile ML (CLIP/LayoutLMv3 fine-tuning).
That path is demoable fast, covers the highest-$-value checks, and de-risks the program.

---

## 1. What the two products actually are

| | **Solution 1 — Atención a Autoridades (PRP1, MVP/beta)** | **Solution 2 — VEC Bank-Statement Verifier (PRP2, proposed)** |
|---|---|---|
| Input | Regulatory *oficios* (CNBV/courts): PDF + XML + Word | Credit-card statements (PDF) + prior-month statement + client/account data + product rates + promo images |
| Core flow | download → OCR/parse → field extraction → cross-document validation → AI classification → SIRO load layout | extract fields → financial/arithmetic consistency → visual/print-quality → reference lookups → QR/fiscal → marked-PDF + email report |
| Essence | "Extract from messy documents, reconcile across sources, validate, emit structured output" | **Same essence**, different rules and an added *visual quality* dimension |

The shared essence is why reuse is the right call.

## 2. Codebase verdict — reuse is real, not just claimed

The existing solution is a **clean hexagonal architecture** (`01 Core` → `02 Infrastructure`
→ `03 Orchestration` → `04 Services` → `07 UI`) with `Result<T>` railway-oriented error handling
(IndQuestResults), a 3-stage pipeline (Quality → Extraction → Fusion), and CSnakes C#↔Python
interop proven by the GOT-OCR2 path.

**Directly reusable for VEC (verified by code inspection, not just docs):**
- `Infrastructure.Extraction` — `TextSanitizer` already cleans bank account #s and SWIFT/BIC codes
- `Infrastructure.Extraction.Txt` / `.Adaptive` — pattern+fuzzy field extraction over noisy OCR (`IFieldExtractor<T>`)
- `Infrastructure.Imaging` — `EmguCvImageQualityAnalyzer` (blur/noise/contrast/sharpness) → directly feeds VEC visual checks
- `Infrastructure.Export` / `.Adaptive` — `ExcelLayoutGenerator`, `DigitalPdfSigner` (PAdES), template-driven export with schema evolution
- `Infrastructure.FileStorage`, `Infrastructure.Events`, `Infrastructure.Database` (EF Core), `Infrastructure.BrowserAutomation` (Playwright)
- `Infrastructure.Python.*` — the **CSnakes integration pattern + Python env bootstrap** (the most valuable shared asset for VEC's ML)
- `Sentinel` (worker health), `Auth`, `Orion` ingestion shell (minus SIARA), `Result<T>` + core interfaces

**Reusable *as a pattern*, retarget the domain model:**
- `Infrastructure.Classification.FusionExpedienteService` — multi-source reconciliation (exact → fuzzy → weighted-vote → conflict). VEC's cross-section/cross-document consistency checks are the same shape.
- `Athena.ExtractionOrchestrator` / 3-stage pipeline — retarget `IFusionExpediente` to a generic `IDataFuser<T>`.

**NOT reusable (oficio-specific, leave alone):**
- Domain entities/enums (`Expediente`, `Oficio`, `RequirementType` 100–104, CNBV Article 4/17)
- `ExpedienteClasifierService`, `SiroXmlExporter`, `DatosCargaOficio*` templates, SIARA scraping, the oficio Web UI

This matches the architecture doc's own estimate ("~60% reuse, additive-only, zero breaking
changes, one-directional dependency `Veriqan → Prisma`").

## 3. Reality check — the existing VEC code is NOT 95% done

`IMPLEMENTATION_SUMMARY.md` claims "PRODUCTION READY (95% Complete)". Independent verification
contradicts this:

- **It cannot import.** Every functional Python module imports `vec_visual_font_identification.models.*`
  — a Pydantic package **that does not exist** in the repo (yet is listed as a delivered ✅ item).
  A second independent bug (`PdfProcessor` vs `PDFProcessor` in `utils/__init__.py`) also blocks import.
- **It has never run.** No `__pycache__`, no venv, no logs, no output artifacts anywhere.
- **Models aren't fit for purpose.** LayoutLMv3 uses the **base** (non-fine-tuned) checkpoint —
  the 20-field/8-product extraction would emit noise; Table Transformer is loaded but bypassed
  (pdfplumber does the real work); date parsing is `# TODO`.
- **C# side is wiring only.** DI bootstrap + a CSnakes wrapper. No `IVecStatementExtractor`, no
  domain entities, no `Result<T>` mapping, **zero C# tests**. The dev even left
  `logger.LogInformation("Is these project really working on??")` in the demo.
- **Tests are theater.** 4 trivial Python tests (1 skipped, rest broken-on-import); the "17 test
  cases" are static JSON, not executable.

**Genuinely good and worth keeping:** `visual/image_quality.py` (real OpenCV), `font/font_detector.py`
(real `pdfplumber` char-level font extraction — this is how you detect "Aptos" without ML!),
`utils/model_cache.py`, the CSnakes wrapper, and the exact-pinned `requirements.txt`.

**Honest completeness:** Python extraction ~45% *written* / 0% verified; visual/font ~45%;
C# integration ~30%; tests ~10%; **end-to-end runnable ~5%.**

## 4. Why NOT standalone

Standalone would be justified only by a separate team, divergent deploy cadence, a hard security
boundary, or genuinely divergent domains. None apply here: single owner, shared competency, shared
ML interop, and the design is already additive. Standalone would duplicate OCR/imaging/extraction/
export/persistence/eventing/Python-interop — pure waste.

**Nuance (the part that *is* "standalone"):** build VEC as a **module in this repo** but keep it
**independently deployable**, and run the **Python ML as its own GPU service** at scale. That gives
clean separation without forking the platform. Module in source ≠ monolith in deployment.

## 5. The scale reality (correct an assumption)

The brief assumed sampling "won't be very big… even 1,000 is a lot manually." The PRP2 docs state
the real numbers: **13–20M statements/month**, QC sampling at **1% ≈ 130,000–200,000/month**
(~26–40K/day peak), 3–5 business-day window. That is **two orders of magnitude above 1,000** and
**well beyond manual capacity** — which strengthens the business case, but means **throughput and
batch processing are first-class requirements**, not afterthoughts. (100% coverage is computable per
your own calc, but design for sampled volumes first.)

## 6. Recommended path (phased, deterministic-first)

> Key insight: **most of the 55 checks do not need ML.** Embedded font names, text-overlap
> geometry, pagination, blank pages, logo/image presence (perceptual hashing/template match), and
> *all* the arithmetic come from `pdfplumber` + geometry + pure C#. Lead with those; treat
> CLIP/LayoutLMv3 fine-tuning as a later accuracy upgrade, not a dependency.

| Phase | Scope | Why first / value |
|---|---|---|
| **0. Genericize core** | Extract `IDataFuser<T>` + generic pipeline/export out of oficio-specific code; stand up `ExxerCube.Prisma.Veriqan.*` projects (Domain/Application/Infra/Tests) depending on Prisma core. | Enables reuse without touching Solution 1; zero breaking changes. |
| **1. VEC domain + financial validation engine** | `BankStatement`/`Transaction`/`ValidationResult` entities; deterministic `IVecValidationRule` engine with tolerance bands; the arithmetic checks (TASA lookup, CAT, period sums, "pago para no generar intereses", points balance, installment tracking). | **Highest $-impact, lowest risk, no ML, demoable.** Covers a large fraction of the 55 checks. |
| **2. Extraction (deterministic)** | Fix the Python package (create `models/`, fix `PdfProcessor`); use `pdfplumber`/Table-Transformer heuristics + the existing `IFieldExtractor` pattern for header/transaction fields. | Feeds Phase 1 with real data; avoids fine-tuning on the critical path. |
| **3. Visual/print-quality (deterministic-first)** | Font = "Aptos" via embedded font names (`font_detector.py`); overlap via char-bbox geometry (Shapely); pagination/blank-page/header checks; logo & catalog-image presence via perceptual hash/template match (upgrade to CLIP later). | The novel dimension vs Solution 1; start cheap and reliable. |
| **4. Reference data + prior-month** | Define schemas/pipelines for the **TASA table, product image catalog, mandatory legends, transaction-detail** — currently consumed by rules but *never specified* — and prior-month statement retrieval. | **Biggest hidden gap in the design.** Must be nailed down with the client. |
| **5. Reporting** | Marked-PDF highlight annotation (PdfSharp), email alerts. | Closes the loop; client-visible. |
| **6. Scale hardening** | Batch/queue throughput for 130–200K/month, GPU Python service, monitoring (Sentinel). | Production readiness. |
| **7. ML accuracy upgrade (optional)** | Fine-tune LayoutLMv3 / CLIP where deterministic methods fall short of the ≥99.9% target. | Only where measurably needed. |

## 7. Open questions to resolve with the client / stakeholders

1. **Sample size & cadence** — confirm the actual % and SLA (docs say 1%; brief says "unknown").
2. **Reference-data sourcing** — how are TASA rates, image catalogs, legends, prior-month statements delivered (feed? DB? files)? *Schema undefined today.*
3. **Font standard conflict** — checklist says **Aptos**; PRP REQ-030 says **Arial / Times New Roman**. Which governs?
4. **Rule count** — "55" (credit-card checklist) vs "115+" (whole VEC design, which also includes Vector brokerage statements). Scope to credit-card first?
5. **Accuracy / false-positive bar** — ≥99.9% accuracy, <1% FP is aggressive for visual ML; deterministic-first helps but confirm acceptance criteria.
6. **Naming** — `ExxerCube.Prisma.Veriqan` vs `ExxerCube.Veriqan` (code samples drift). Pick one.

## 8. One-line answer to the brief

> Yes — produce it on this codebase as the `Veriqan` module (≈50–60% real reuse), but reset the
> "95% done" expectation (true state ≈5–15%), build deterministic-first to de-risk the visual/ML
> parts, and pin down the undefined reference-data pipelines before committing to dates.

# Addendum — Veriqan VEC PRD

Technical depth, mechanism decisions, and options-considered that inform downstream architecture but
do not belong in the capability-level PRD.

## A. Reuse mapping (Shared Core → Veriqan)

| Veriqan need | Shared Core asset (reuse) | Mode |
|---|---|---|
| Field extraction pattern | `IFieldExtractor<T>`, `Infrastructure.Extraction.Txt/.Adaptive` | Direct |
| Bank token cleanup | `Infrastructure.Extraction.TextSanitizer` (account/SWIFT) | Direct |
| Image quality metrics | `Infrastructure.Imaging.EmguCvImageQualityAnalyzer` | Direct |
| Python ML interop | CSnakes pattern from `Infrastructure.Python.GotOcr2/.VecExtraction` | Direct (pattern) |
| Marked PDF / export | `Infrastructure.Export` (PdfSharp, `DigitalPdfSigner`), `.Adaptive` | Direct/Extend |
| Persistence | `Infrastructure.Database` (EF Core) — additive VEC tables | Extend (additive) |
| Eventing | `Infrastructure.Events.InMemoryEventBus` | Direct |
| Cross-source reconciliation | `Infrastructure.Classification.FusionExpedienteService` → generalize to `IDataFuser<T>` | Adapt pattern |
| Pipeline/orchestration | `Athena.ExtractionOrchestrator` (Quality→Extraction→Fusion) | Adapt pattern |
| Worker health | `Sentinel` | Direct |
| Error handling | `Result<T>` (IndQuestResults) | Direct |

Phase 0 work = extract generic seams (`IDataFuser<T>`, generic pipeline/export) out of oficio-specific
code **additively**, so Solution 1 keeps working unchanged.

## B. Deterministic-first technique choices (no ML in v1)

- **Aptos font check (CL-35):** read embedded font names from the PDF font dictionary via
  `pdfplumber` char-level extraction (the salvageable `font/font_detector.py` already does this). No
  vision model needed.
- **Text overlap (CL-28):** geometric intersection of char/word bounding boxes (Shapely). Salvage
  `font/overlap_detector.py` (fix the A4-hardcoding).
- **Pagination / blank page / per-page elements (CL-31/33/34/48):** deterministic page + content
  inspection.
- **Image presence (CL-27/30/47 v1):** perceptual hashing (pHash/dHash) of rendered regions vs.
  catalog `perceptualHash`; cheap, no GPU.
- **QR/fiscal (CL-50…53):** a QR/barcode decode library (e.g. ZXing.NET) + RFC/fiscal pattern
  validation. `[DECISION PENDING: confirm QR library.]`
- **All financial math (CL-10, 15–26, 36–44):** pure C# in the validation engine.

## C. Deferred ML (v2)

- **LayoutLMv3** (fine-tuned) for header/table extraction where deterministic parsing is insufficient.
- **CLIP** for catalog-order image matching and visual brand compliance (CL-27/30/47 full).
- Runs as a separate GPU service behind the CSnakes/inference boundary; gated on a measured accuracy
  bar before replacing/augmenting deterministic checks.

## D. Salvage vs. rebuild of existing PRP2 scaffolding

- **Salvage (verified good):** `visual/image_quality.py`, `font/font_detector.py`,
  `utils/model_cache.py`, `utils/pdf_processor.py`, the CSnakes wrapper, pinned `requirements.txt`.
- **Rebuild:** the missing `models/` Pydantic package, `vec_orchestrator` wiring, the C# domain
  model + `IVecStatementExtractor` + Result mapping + tests (all absent today).
- **Discard/replace:** placeholders presenting as features (`color_accuracy=1.0`, hardcoded
  confidences, `check_brand_colors` always-true, `typography_analyzer` defaults).

## E. Reference-data contract

Defined in `Prisma/Fixtures/PRP2/reference-data/` (schema + example, schema-validated). Consumed via
`IVecReferenceDataProvider`; adapters: `CsvReferenceDataAdapter`, `DatabaseReferenceDataAdapter`,
`ApiReferenceDataAdapter`. Graceful degradation = missing section ⇒ `INSUFFICIENT_DATA`, never FAIL.

## F. Namespace & compatibility

- `ExxerCube.Prisma.Veriqan.*` for VEC; `ExxerCube.Prisma.*` shared. Dependency `Veriqan → Prisma`
  only, enforced by an architecture test (extend the existing `HexagonalArchitectureTests`).
- Shared-abstraction changes must be additive (new members/overloads/generic params); Solution 1
  regression suite is the release gate.

## G. Source-of-truth reconciliation actions (docs hygiene)

The Excel is authoritative. Update/annotate the other PRP2 docs to match:
- `PRP.md` "115+ rules" → reconcile to the 55-item credit-card checklist (note brokerage/Vector is a
  separate, out-of-scope family).
- Font: remove Arial/Times (REQ-030) → **Aptos**.
- `IMPLEMENTATION_SUMMARY.md` "95% / production ready" → mark superseded; link to the reuse analysis.
- Resolve `ExxerCube.Prisma.Veriqan` vs `ExxerCube.Veriqan` naming.

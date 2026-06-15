# Item D (#10, Step 4) — Field-extraction completeness — Design

**Owner decision:** include Word-image OCR (full scope). Split into **D1** (XML completeness + Docx requerimiento
regex — low risk) and **D2** (Word-image OCR remitente — risky), verified/committed separately.

## Authoritative facts (from the real sample corpus)
- XML namespace `http://www.cnbv.gob.mx`. Per `docs/legal/samples/222AAA-44444444442025.xml`:
  - `Cnbv_NumeroOficio` = `222/AAA/-4444444444/2025` (the oficio number) — **distinct from**
  - `Cnbv_SolicitudSiara` = `AGAFADAFSON2/2025/000084` (the **requerimiento id**).
  - `Domicilio` lives only under `SolicitudEspecifica/PersonasSolicitud/Domicilio` (NOT in `SolicitudPartes`).
  - Name parts under `SolicitudEspecifica/PersonasSolicitud/{Paterno,Materno,Nombre}`.
- Docx (`222AAA-...docx`): requerimiento id is present as TEXT; `DocxFieldExtractor.ExtractExpediente` regex
  (`[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+`) does NOT match it. The working pattern is
  `AdaptiveTxtFieldExtractor.ExtractNumeroOficio` (`[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}`). The .docx has
  `word/media/image1.png` + `image2.png` (signature/stamp area).
- OCR seam: `IOcrExecutor.ExecuteOcrAsync(ImageData, OCRConfig) → Task<Result<OCRResult>>`.
  `ImageData(byte[] Data, string SourcePath, int PageNumber=1, int TotalPages=1)`;
  `OCRConfig(Language="spa", OEM=1, PSM=6, …)`; `OCRResult.Text` + `ConfidenceAvg` (0–100). Accepts in-memory bytes.
  `DocumentFormat.OpenXml` is already referenced; `mainPart.ImageParts` enumerates embedded images.

## D1 — XML completeness + Docx requerimiento regex (do first)
1. **`XmlFieldExtractor`** (`02 Infrastructure/Infrastructure.Extraction/Teseract/XmlFieldExtractor.cs`):
   - Read `Cnbv_NumeroOficio` (root child) → surface on the same field/dictionary key the downstream fusion/layout
     already use for the oficio number (match how `Cnbv_NumeroExpediente` is surfaced).
   - Read `Domicilio` from the first `SolicitudEspecifica/PersonasSolicitud` → surface (e.g. `additional["Domicilio"]`).
   - Compose **Descripción** = `Paterno + " " + Materno + " " + Nombre` (skip blanks, single spaces) from the first
     `PersonasSolicitud` → surface (e.g. `additional["Descripcion"]`) AND ensure the name parts populate the
     `SolicitudParte` the layout/projection reads (item A's projection composes from `SolicitudPartes[0]`), so the
     value actually reaches the Datos-Carga layout.
2. **`DocxFieldExtractor`** (`…/Teseract/DocxFieldExtractor.cs`):
   - Add `ExtractNumeroOficio`/requerimiento extractor reusing the AdaptiveTxt regex
     `[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}`; route a `numerooficio`/`requerimiento` field name to it. Do NOT change
     the existing `ExtractExpediente` behavior (it serves a different field).
3. **Tests** (ITDD): per-field tests that run over the **real sample corpus** (`docs/legal/samples/*.xml`,
   `*.docx`) — Domicilio, NumeroOficio, composed Descripción, requerimiento id — plus the existing synthetic tests
   stay green. Assert the exact expected values from the samples.

## D2 — Word-image OCR remitente (do second; best-effort, deadlock-aware)
4. **`DocxFieldExtractor`** gains an OCR path:
   - Inject the existing `IOcrExecutor` (the SAME registered instance — do NOT new-up a TesseractEngine; the
     codebase has a documented **Tesseract 2nd-init-in-same-process DEADLOCK**). Enumerate `mainPart.ImageParts`,
     read each `GetStream()` → bytes, call `ExecuteOcrAsync(new ImageData(bytes, "word/media/...", …), new OCRConfig("spa"))`.
   - Parse the **remitente** (signing functionary) name from the OCR text (best-effort name heuristic; prefer a
     line near "Atentamente"/signature). If OCR fails, returns low confidence, or yields no plausible name →
     **fail open**: leave remitente unset / fall back to `AutoridadNombre`. NEVER throw; the whole image-OCR path
     is wrapped and logged at Warning.
   - **Opt-in / guard:** make the image-OCR path skippable (e.g. an option/flag) so it can be disabled if it
     destabilizes the in-process Tesseract engine; default behavior must not break existing Docx extraction or the
     PDF-OCR pipeline running in the same process.
5. **Tests:** over the real `222AAA-...docx` — assert the image-OCR path runs, returns a Result (success or
   graceful empty), does not throw, and does not break the existing Docx text extraction. Because OCR output of a
   signature image is non-deterministic, assert robustly (non-throwing + Result + the text-field extraction still
   works), not an exact name string, unless the sample reliably OCRs a known token.

## Definition of done
- D1: build 0/0; `Tests.Infrastructure.Extraction` green incl. new real-corpus tests; existing synthetic tests green.
- D2: build 0/0; image-OCR path tested (non-throwing/fail-open) over the real .docx; no Tesseract deadlock; the
  feature is guardable; existing extraction unaffected.
- Architecture tests stay 22/22. `git diff` confined to the two extractors, any new options/DI, and the new tests.

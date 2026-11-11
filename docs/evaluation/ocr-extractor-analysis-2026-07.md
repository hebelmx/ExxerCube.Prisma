# OCR field-extraction analysis — why `/document-processing` PDF yields so few fields

**Date:** 2026-07-02 · **Fixture analysed:** `Prisma/Fixtures/PRP1/333ccc-6666666662025.pdf` (3 pages).
**Method:** empirical — rasterized all 3 pages (`pdftoppm -r 300`), OCR'd each (`tesseract -l spa`),
compared the raw OCR text to the extracted `Expediente` JSON from the live demo.

## TL;DR

The OCR is **not** the bottleneck and the old "first page only" bug is **not** present. All 3 pages are
rasterized and OCR'd (~4,000 chars total), and the text clearly contains the data that comes back empty.
**The bottleneck is the deterministic regex field extractor** (`AdaptiveTxtFieldExtractor`): it captures a
handful of clean header fields and misses the expediente number, the entire party table, amounts and
accounts — all plainly present in the OCR text. The highest-yield next step is an **LLM extractor over the
OCR text** (the agent track from GH#31), keeping deterministic as the baseline/oracle.

## Evidence

### 1. Page coverage is correct (first-page bug NOT present)
- `PdfToImageConverter.ConvertToImagesAsync` calls `Conversion.GetPageCount` and loops
  `for pageIndex in 0..pageCount` — converts **every** page.
- `PdfOcrFieldExtractor.ExtractTextFromPdfAsync` loops **all** image pages, OCRs each, and joins the text.
- Measured: page1 ≈ 1,150 chars, page2 ≈ 1,012, page3 ≈ 1,881 → ~4,043 chars OCR'd across all 3 pages.

### 2. These PDFs are image-based (OCR is genuinely required)
- `pdfinfo`: 3 pages. `pdftotext`: **3 native-text chars total** → no usable text layer. So native-text
  extraction (PdfPig) would NOT help these fixtures; OCR is the right path.

### 3. The data is in the OCR text but not extracted
| Field (empty/wrong in JSON) | Present in OCR text |
|---|---|
| `NumeroExpediente: ""` | page 2: *"no. De expediente 804/2025"* |
| `SolicitudPartes: []` | *"MARCELO ZU CARNAL"*, RFC *"ZUCM444444555"*, CURP, *"Fecha de nacimiento 01/01/1991"*, *"Carácter: Determinado"*; *"BSSA Banco del Sur… Actor"* |
| amount (none on Expediente) | *"$76,813.31 pesos"*, multa *"4,500 pesos"* |
| `NombreSolicitante: null` | *"MTRO. GUADALUPE PEPITA PEPITA, ADMINISTRADOR…"* |
| accounts / origen | *"CAJA POPULAR DE CHILPANCINGO"*; *"MERCANTIL EJECUTIVO"* |
| `AutoridadEspecificaNombre: "SECRETARÍA DE HACIENDA Y CR"` (truncated) | *"Administración General de Auditoría Fiscal Federal / … de Sonora 2"* |

### 4. OCR quality is moderate — hurts brittle regex
Digit/label noise that breaks exact patterns: office no. reads `…000085` (pages 1/3) but the demo captured
`…000083`; RFC varies across pages (`ZUCMA44444855` vs `ZUCM444444555`); truncations and garbled headings
(`eonocer`, `SON( Ñó`, `¿CÍA`). Real scanned-doc noise.

### 5. The extractor is regex-tuned for clean, specific formats
`AdaptiveTxtFieldExtractor` (~630 lines, many `Regex`) targets NumeroOficio, Autoridad, FundamentoLegal,
Nombre, fecha, expediente, etc. But its expediente pattern expects the CNBV form (e.g.
`A/AS1-2505-088637-PHM`), so it does not match `804/2025`; and there is **no** structured parser for the
party table (name/RFC/CURP/carácter rows), monetary amounts, or accounts-to-investigate. Hence the header
fields it does nail (NumeroOficio, Autoridad, FundamentoLegal, Telefono, Dirección) come through and the
rest stay empty.

## Recommendation — deterministic vs LLM

The lever is **extraction**, not more OCR. Two complementary moves:

1. **(Now, cheap) Extend deterministic patterns** for the obvious misses: expediente `\d+/\d{4}`, monetary
   amounts (`$[\d,]+\.\d{2}`), requestor (`MTRO\.?/LIC\.?/ADMINISTRADOR…`). Quick wins for clean fields.
2. **(Strategic, biggest yield) LLM extractor over the OCR text** — the client-requested agent track
   (GH#31). An LLM robustly handles OCR noise and the semi-structured party/account tables that regex
   cannot, extracting `SolicitudPartes[]` (name/RFC/CURP/DOB/carácter), amounts, expediente, accounts and
   requestor in one structured pass. **Infrastructure already exists:** `IOllamaClient`/`OllamaHttpClient`
   + `SemanticAnalyzerService` (which already has `EnrichInformacionWithLlmAsync`), gated on
   `OllamaOptions.Enabled`. Keep the deterministic extractor as the **baseline/oracle** and fallback; the
   GH#31 corpus + harness already provide the accuracy/latency/cost comparison framework.

**Recommended shape:** hybrid — deterministic for the clean header fields it already extracts well, LLM for
the hard semi-structured fields (parties, amounts, accounts, expediente), with a confidence/validation gate
so a noisy OCR digit never silently produces a wrong-but-confident value.

## Secondary UI note (tracked separately)
The comparison "source" label shows `All` for a single document and a count (`2`) for two, instead of the
concrete set (`Json`, `Pdf`, `Json+Pdf`). Small formatting fix in `ComparisonSection.razor`.

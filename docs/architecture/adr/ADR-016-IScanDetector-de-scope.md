# ADR-016: IScanDetector — De-Scope in Favour of IImageQualityAnalyzer + PdfMetadataExtractor

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, ocr, scan-detection, imaging, prp
**Related**: PRP.md §Feature 13; `IImageQualityAnalyzer.cs`; `EmguCvImageQualityAnalyzer.cs`; `PdfMetadataExtractor.cs`

---

## Context

The PRP (§Feature 13, §Stage 2 Interfaces) defines `IScanDetector`:

> **Purpose**: Identifies scanned (non-searchable) PDF documents.

```
IsScannedPdfAsync(string filePath)   → Result<bool>
GetScanQualityScoreAsync(string filePath) → Result<int>
```

**Production equivalents:**

1. `IImageQualityAnalyzer` (`Domain/Interfaces/IImageQualityAnalyzer.cs`) declares:
   - `AnalyzeAsync(ImageData) → Result<ImageQualityAssessment>`
   - `GetQualityLevelAsync(ImageData) → Result<ImageQualityLevel>`

   `EmguCvImageQualityAnalyzer` (`Infrastructure.Imaging/EmguCvImageQualityAnalyzer.cs`)
   implements this using OpenCV (Emgu.CV): it decodes image bytes with `CvInvoke.Imdecode`,
   applies the Laplacian blur metric, noise threshold analysis, and contrast measurement to
   produce a quality score and level. `PolynomialImageQualityAnalyzer` is a second
   implementation tuned by polynomial regression.

2. `PdfMetadataExtractor` (`Infrastructure.Extraction/Teseract/PdfMetadataExtractor.cs`,
   lines 44-51) performs inline scan detection: it calls `TryExtractTextFromPdfAsync`, and
   if the result is empty or shorter than 50 characters it classifies the PDF as scanned and
   routes to the OCR branch — effectively implementing `IsScannedPdfAsync` inline inside the
   extraction pipeline without a separate interface.

3. The Python pipeline (`CSharp/Python/ocr_modules/pipeline.py`,
   `image_deskewer.py`) also contains scan detection and deskew logic that is kept dormant
   (optionality-by-design per ADR-001). It is not a production dependency.

**The `IImageQualityAnalyzer` interface goes further than `IScanDetector`**: it also
drives filter selection (`IFilterSelectionStrategy`) for the OCR preprocessing stage, so
it serves both scan-detection and image-quality-gating purposes. Isolating scan detection
into a separate interface would be a downgrade in information richness.

## Decision

Do **not** declare `IScanDetector` as a standalone interface. The scan-detection
capability described by PRP Feature 13 is fulfilled by:

- **`IImageQualityAnalyzer`** — authoritative quality assessment including a quality level
  enum that distinguishes scanned/low-quality from native-text PDFs.
- **`PdfMetadataExtractor`** — inline heuristic (text length < 50 chars) for routing to
  the OCR branch; this is the correct place for this check since it only makes sense in
  the context of PDF extraction.

Update PRP.md §Feature 13 to reference `IImageQualityAnalyzer`:

> "Scan detection (F-13) is provided by **`IImageQualityAnalyzer`** (quality level
> assessment) and an inline heuristic inside `PdfMetadataExtractor`. The `IScanDetector`
> placeholder is retired."

## Rationale

1. Scan detection is not a standalone concern; it is a prerequisite step of the OCR
   preprocessing chain, tightly coupled to image quality analysis.
2. `IImageQualityAnalyzer` already returns quality scores (via `ImageQualityAssessment`)
   that subsume the `GetScanQualityScoreAsync` intent.
3. `PdfMetadataExtractor` already implements the `IsScannedPdfAsync` decision inline —
   extracting it to a separate injectable service would break encapsulation with no
   consumer benefit.
4. Two production implementations of `IImageQualityAnalyzer` exist and are tested
   (`PolynomialImageQualityAnalyzerMutationTests.cs`, `AnalyticalFilterSelectionStrategyMutationTests.cs`).

## Consequences

- PRP §Feature 13 mapping to be amended to `IImageQualityAnalyzer`.
- If a future requirement demands a pluggable scan-detection strategy decoupled from image
  preprocessing (e.g., a text-extraction-based detector using PdfPig), a new `IScanGate`
  interface can be introduced in the extraction layer at that time.
- No production code change required.

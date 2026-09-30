# ADR-017: IScanCleaner — De-Scope in Favour of IImagePreprocessor

**Date**: 2026-06-20
**Status**: Accepted
**Deciders**: Development Team
**Tags**: itdd, interface-conformance, ocr, image-preprocessing, imaging, prp
**Related**: PRP.md §Feature 14; `IImagePreprocessor.cs`; `NoOpImagePreprocessor.cs`; `PdfMetadataExtractor.cs`

---

## Context

The PRP (§Feature 14, §Stage 2 Interfaces) defines `IScanCleaner`:

> **Purpose**: Cleans visual artifacts from scanned images to improve OCR accuracy.

```
CleanScanAsync(ImageData, CleaningOptions?) → Result<ImageData>
```

**PRP dependency listed**: `IImagePreprocessor`.

**Production equivalent:**

`IImagePreprocessor` (`Domain/Interfaces/IImagePreprocessor.cs`) is already declared in
the domain and exposes exactly the operations the PRP intended `IScanCleaner` to wrap:

```csharp
PreprocessAsync(ImageData, ProcessingConfig)    → Result<ImageData>
RemoveWatermarkAsync(ImageData)                 → Result<ImageData>
DeskewAsync(ImageData)                          → Result<ImageData>
BinarizeAsync(ImageData)                        → Result<ImageData>
```

`NoOpImagePreprocessor` (`Infrastructure/NoOp/NoOpImagePreprocessor.cs`) provides the
production registration (a pass-through for environments without full imaging dependencies).
`PdfMetadataExtractor` (`Infrastructure.Extraction/Teseract/PdfMetadataExtractor.cs`)
injects `IImagePreprocessor` and calls it before passing images to the Tesseract OCR
executor — confirming the interface is wired and in use.

The Python-side equivalent (`CSharp/Python/ocr_modules/image_deskewer.py`) also contains
deskew and watermark-removal logic; it is dormant per ADR-001 but its presence shows the
team considered these capabilities in both stacks.

**The PRP's own `IScanCleaner` dependency graph lists `IImagePreprocessor` as the only
dependency**, making `IScanCleaner` a thin renaming wrapper with no additive contract.

## Decision

Do **not** declare `IScanCleaner` as a standalone interface. The image-cleaning capability
described by PRP Feature 14 is fully covered by the existing **`IImagePreprocessor`**
interface, which already exposes deskew, binarize, watermark removal, and a general
`PreprocessAsync` orchestration method.

Update PRP.md §Feature 14 to read:

> "Scan cleaning / image preprocessing (F-14) is performed by **`IImagePreprocessor`**
> (deskew, binarize, watermark removal). The `IScanCleaner` placeholder is retired as a
> direct alias."

## Rationale

1. `IImagePreprocessor` is a strict superset of `IScanCleaner`: it exposes each
   individual cleaning operation (`DeskewAsync`, `BinarizeAsync`, `RemoveWatermarkAsync`)
   plus a combined `PreprocessAsync` — covering everything `CleanScanAsync` was intended
   to do and more.
2. The PRP itself declared `IScanCleaner` as depending on `IImagePreprocessor`, which
   means any `IScanCleaner` implementation would simply delegate to it. That delegation
   layer provides no architectural benefit.
3. `IImagePreprocessor` is already registered in DI (via `NoOpImagePreprocessor`) and
   injected into `PdfMetadataExtractor`; the pipeline already uses it.
4. Introducing `IScanCleaner` as an alias would confuse future developers about which
   interface to inject for preprocessing tasks.

## Consequences

- PRP §Feature 14 mapping to be amended to `IImagePreprocessor`.
- If the project later needs a pluggable scan-cleaning strategy distinct from general
  preprocessing (e.g., a separate ML-based artifact remover), a new interface can be
  introduced at that time without retrofitting the current preprocessor contract.
- No production code change required.

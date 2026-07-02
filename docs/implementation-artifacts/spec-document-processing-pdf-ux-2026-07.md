---
title: '/document-processing PDF flow — 3 fixes: blank preview, fake OCR metrics, no processing feedback'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: '2f191af1'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Owner-reported nuances on `/document-processing` when clicking a PRP PDF button (backend works, but):
1. **No "working" feedback** while the (slow) OCR runs — the page looks unresponsive until the
   "OCR Complete!" snackbar.
2. **OCR confidence looks hardcoded to 80%** and **Characters Extracted stays 0**, even though fields
   are extracted (JSON shows real `AdditionalFields`) — i.e. the metrics are a fallback, not real.
3. **The original PDF is not shown** in its preview widget.

## Root causes (traced)

1. `PdfProcessingSection.LoadFixture` never toggled any busy state; the parent's `IsProcessing`
   parameter was unused on this path → zero feedback during the await.
2. `PdfOcrFieldExtractor.ExtractFieldsAsync` runs real OCR, feeds the text into a `TxtSource`, then
   returns the `AdaptiveTxtFieldExtractor` result **verbatim** — which never echoes the OCR text back.
   `PdfProcessingService` reads `AdditionalFields["_OcrText"]`/`["_OcrConfidence"]`; both were absent, so
   it used `""` (→ 0 chars) and the `0.8f` fallback (→ 80%). Half-wired contract.
3. `DocumentProcessing.HandlePdfRenderRequested` called `renderPdf(base64, canvasId)` but the JS is
   `renderPdf(canvasId, base64Data)` — **args swapped** → `getElementById(<base64 string>)` → canvas
   never found → blank preview (silent console error).

## Fixes

1. `PdfProcessingSection.razor` — local `_busy` flag: set + `StateHasChanged()` before the await, cleared
   in `finally`; a "Rasterizing PDF and running OCR…" banner; buttons disable on `_busy || IsProcessing`.
2. `PdfOcrFieldExtractor.cs` — after a successful delegate extraction, attach
   `AdditionalFields["_OcrText"] = ocrText` and `["_OcrConfidence"] = confidence` (invariant) so the UI
   shows the REAL character count + OCR confidence.
3. `DocumentProcessing.razor` — call `renderPdf(canvasId, base64Pdf)` (correct arg order).

## Boundaries & Constraints

**Always:** Keep the OCR-provenance keys exactly `_OcrText` / `_OcrConfidence` (the UI contract).
Confidence stays 0–1 (UI multiplies by 100).

**Never:** Do NOT reintroduce the swapped renderPdf args. Do NOT hardcode a confidence.

## Verification

- Build 0/0; `Tests.Infrastructure.Extraction/PdfOcrFieldExtractorTests` green (added keys are additive).
- Image rebuilt + recreated; OCR native stack (libtesseract50.so + spa tessdata) intact; web-ui 200.
- Visual confirmation (spinner shows, real confidence + char count, PDF renders) = the owner's click.

## Known caveat (not in scope)

`renderPdf` loads PDF.js from a **CDN** (`cdnjs.cloudflare.com`). If the demo box is offline or CSP blocks
the CDN, the preview stays blank even with the arg fix. Bundling PDF.js locally is a follow-up if needed.

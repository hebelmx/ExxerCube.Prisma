# REGRESSION TEST: Multi-Page PDF Processing

## Critical Bug (FIXED)

**Bug ID:** TotalPages Hardcoded to 1
**Date Fixed:** 2025-12-10
**Commit:** 8bbd4c0
**Severity:** CRITICAL - Data Loss

### Bug Description

PdfProcessingService was hardcoding `TotalPages = 1` regardless of actual PDF page count. This caused:

1. ❌ **Only first page processed** - Pages 2, 3, 4... completely ignored
2. ❌ **Missing text/data** - Fields from subsequent pages never extracted
3. ❌ **Low field extraction counts** - 3 fields instead of 12-16
4. ❌ **Similar results across documents** - All multi-page PDFs looked the same (only page 1 data)

### Root Cause

**File:** `PdfProcessingService.cs` (lines 63-68, before fix)

```csharp
// ❌ BUG: Hardcoded TotalPages = 1
var imageData = new ImageData
{
    Data = pdfBytes,           // Entire PDF bytes
    SourcePath = fixtureName,
    PageNumber = 1,
    TotalPages = 1             // ❌ WRONG! Should be actual page count
};
```

This single ImageData with entire PDF bytes was passed to OCR, which only processed the first page.

### Fix Applied

1. **Convert PDF to individual page images** using PDFtoImage library
2. **Process each page separately** through IOcrProcessingService
3. **Combine page texts** with `"\n\n"` separators
4. **Average confidence scores** across all pages
5. **Log page count** at every step for traceability

**Reference Implementation:** `PdfOcrFieldExtractor.cs` (lines 172-259)

### Manual Regression Test

Run this test **manually** after any changes to PdfProcessingService to ensure multi-page PDFs work correctly.

#### Test Fixtures

Use these multi-page PDF fixtures from `PRP1/`:
- `222AAA-44444444442025.pdf` (verify actual page count)
- `333BBB-33333333332025.pdf` (verify actual page count)
- `444CCC-44444444442025.pdf` (verify actual page count)

#### Test Procedure

1. **Start Web.UI application**
   ```bash
   cd Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI
   dotnet run
   ```

2. **Navigate to Document Processing page**
   - URL: https://localhost:5001/document-processing

3. **Load a multi-page PDF**
   - Click "Load 333BBB PDF" (or any multi-page fixture)
   - Wait for processing to complete

4. **Verify in Seq logs** (critical validation):
   ```
   Filter by: 📄 PDF PROCESSING

   Expected log sequence:
   [1] 📄 PDF PROCESSING: START - Requested fixture: 333BBB-33333333332025.pdf
   [2] 🔍 FIXTURE DEBUG: Successfully loaded {Size} bytes from 333BBB-33333333332025.pdf
   [3] 📄 PDF PROCESSING: Converting PDF to individual page images
   [4] 📄 PDF PROCESSING: Converted to {PageCount} pages  <-- VERIFY PageCount > 1
   [5] 📄 PDF PROCESSING: Processing page 1/{PageCount}
   [6] 📄 PDF PROCESSING: Processing page 2/{PageCount}  <-- VERIFY page 2 is processed
   [7] ...
   [N] 📄 PDF PROCESSING: OCR completed for ALL {TotalPages} pages
   ```

5. **Verify OCR text contains multi-page content**:
   - Open browser DevTools → Network → Find OCR response
   - OR check database for extracted Expediente
   - OR check UI display of extracted fields
   - **Verify:** Text from page 2, 3, 4... is present (not just page 1)

6. **Verify field extraction count**:
   - UI should show: "Successfully extracted {N} fields" where N > 3
   - For 333BBB, expect 12-16 fields, NOT 3
   - Seq log: `🔬 FIELD EXTRACTION: AdditionalFields count: {Count}` where Count >= 8

#### Expected Results (PASS)

✅ Seq logs show `Converted to {N} pages` where N matches actual PDF page count
✅ Seq logs show `Processing page 2/{Total}`, `Processing page 3/{Total}`, etc.
✅ OCR text length > 1000 characters (multi-page content)
✅ Field extraction count >= 12 (not 3)
✅ Different PDFs yield different extraction results (not all similar)

#### Failure Indicators (FAIL - BUG RETURNED!)

❌ Seq shows `Converted to 1 pages` for multi-page PDF
❌ No logs for "Processing page 2"
❌ OCR text length < 500 characters (only page 1)
❌ Field extraction count = 3 (only page 1 fields)
❌ All PDFs yield similar results (only page 1 processed)

### Automated Test (Future)

**TODO:** Create unit test that mocks multi-page PDF conversion and verifies:
- `ProcessDocumentAsync` called N times (once per page)
- Each call has correct `PageNumber` (1, 2, 3...)
- Each call has correct `TotalPages` (N for all pages)
- Results combined with `"\n\n"` separator
- Confidence averaged across all pages

**File:** `PdfProcessingServiceTests.cs` (to be implemented with proper mocking infrastructure)

### Related Changes

**Files Modified:**
- `PdfProcessingService.cs` - Fixed multi-page processing
  - Added `ConvertPdfPagesToImages()` method (lines 359-428)
  - Updated `LoadFixtureAsync()` to process all pages (lines 60-155)

**Dependencies:**
- PDFtoImage (cross-platform PDF rendering)
- SixLabors.ImageSharp (image processing)
- Existing IOcrProcessingService (processes single ImageData per call)

### Prevention

To prevent this bug from returning:

1. **Code Review Checklist:**
   - [ ] Any `TotalPages` assignment verified against actual page count
   - [ ] Multi-page PDFs tested manually before PR approval
   - [ ] Seq logs reviewed for "Converted to {PageCount} pages"

2. **CI/CD:**
   - [ ] Add integration test with real multi-page PDF fixture
   - [ ] Assert field extraction count > threshold
   - [ ] Assert OCR text length > threshold

3. **Monitoring:**
   - [ ] Alert if field extraction count drops below historical average
   - [ ] Alert if OCR text length drops for known fixtures

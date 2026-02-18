# Testing & Visual Presentation Guide
## DocumentProcessing.razor - 3-Way Reconciliation Demo

**Date:** 2025-12-08
**Status:** 🔄 Ready for Visual Testing
**Priority:** 🔴 CRITICAL - Stakeholder Demo

---

## 🎯 What We've Implemented

### ✅ **Phase 1 & 2 Complete: Foundation**

1. **Service Integration:**
   - `IFieldExtractor<PdfSource>` / `PdfOcrFieldExtractor` - PDF field extraction
   - `IFusionExpediente` / `FusionExpedienteService` - 3-way reconciliation engine
   - `IFieldMatchingService` - Multi-source field orchestration

2. **State Management:**
   - `pdfExpediente` - Expediente extracted from PDF/OCR
   - `docxExpediente` - Expediente from DOCX (ready for future)
   - `xmlMetadata`, `pdfMetadata`, `docxMetadata` - Extraction quality metrics

3. **Core Methods:**
   - `ExtractExpedienteFromOcrAsync()` - Production field extraction from OCR
   - `MapExtractedFieldsToExpediente()` - Field mapping helper
   - Updated `LoadPdfFixture()` - Now calls field extraction after OCR
   - Updated `CompareResults()` - Uses extracted `pdfExpediente`

---

## 🚀 Testing Instructions

### **Step 1: Build the Project**

```bash
cd "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI"
dotnet build --no-incremental
```

**Expected Result:** Build succeeds with no errors

---

### **Step 2: Run the Application**

```bash
dotnet run --project "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj"
```

**Expected URL:** `https://localhost:5001` or `http://localhost:5000`

---

### **Step 3: Navigate to Document Processing Page**

**URL:** `https://localhost:5001/document-processing`

---

## 📋 Visual Testing Checklist

### **Test 1: XML Extraction (Already Working)**

1. ✅ Click any XML button (e.g., "PRP1: 222AAA")
2. ✅ Verify extraction succeeds
3. ✅ Check that fields are displayed in the table
4. ✅ Verify "View Source XML" button works

**Expected Visual:**
- Green success alert
- Field count displayed (e.g., "Extracted 15 fields")
- Data table with NumeroExpediente, NumeroOficio, etc.
- Theme-aware colors (works in dark/light mode)

---

### **Test 2: PDF Extraction with NEW Field Extraction** 🆕

1. ✅ Click any PDF button (e.g., "PRP1: 222AAA PDF")
2. ✅ **NEW:** Verify "Extracting fields..." status appears
3. ✅ **NEW:** Check that `pdfExpediente` is populated (check browser console logs)
4. ✅ Verify real PDF viewer displays on the left
5. ✅ Verify OCR text displays on the right
6. ✅ Check OCR confidence percentage is shown

**Expected Visual:**
- Processing progress bar shows steps:
  - "Loading PDF..." (20%)
  - "Processing with OCR..." (40%)
  - "Extracting text with Tesseract..." (60%)
  - **NEW:** "Extracting fields..." (80%)
  - "Completed" (100%)
- Side-by-side layout:
  - **Left:** PDF viewer (actual PDF rendered)
  - **Right:** Extracted OCR text
- Success message with confidence score

**Browser Console Check:**
Open browser DevTools (F12) → Console tab
- Look for log: `"Successfully extracted Expediente from PDF: {NumeroExpediente}"`
- If error, note the message for debugging

---

### **Test 3: 2-Way Comparison (XML vs PDF)** 🆕

1. ✅ Load XML fixture: Click "PRP1: 222AAA"
2. ✅ Load matching PDF: Click "PRP1: 222AAA PDF"
3. ✅ **NEW:** Click "Compare Results" button
4. ✅ **NEW:** Verify comparison uses extracted `pdfExpediente` (not stub data)

**Expected Visual:**
- Comparison button enables after both sources loaded
- Comparison table displays with:
  - Field-by-field comparison
  - Similarity scores
  - Match/Different/Partial status colors
- **NEW:** Fusion result section (if FusionService works)
  - Overall confidence percentage
  - Source reliabilities (XML, PDF, DOCX)
  - Reconciled values

**Look for:**
- Green = Match
- Yellow = Partial match
- Red = Different
- Gray = Missing

---

### **Test 4: Visual Quality Check**

#### **Theme Compatibility:**
1. ✅ Switch to dark mode (if available)
2. ✅ Verify all components are readable
3. ✅ Check that PDF viewer, XML viewer, OCR viewer work in both themes

#### **Responsive Design:**
1. ✅ Resize browser window
2. ✅ Verify PDF + OCR side-by-side collapses to vertical stack on narrow screens
3. ✅ Check buttons remain accessible

#### **Professional Appearance:**
1. ✅ No broken layouts
2. ✅ Consistent spacing
3. ✅ Clear visual hierarchy
4. ✅ Smooth transitions/progress indicators
5. ✅ Readable fonts and colors

---

## 🐛 Known Issues to Check

### **Issue 1: PdfOcrFieldExtractor May Not Be Registered in DI**

**Symptom:** Error when loading PDF: "Cannot resolve IFieldExtractor<PdfSource>"

**Fix:** Add to `Program.cs`:
```csharp
services.AddScoped<IFieldExtractor<PdfSource>, PdfOcrFieldExtractor>();
```

**Location:** `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Program.cs`

---

### **Issue 2: Field Extraction Returns Null/Empty**

**Symptom:** `pdfExpediente` is null after PDF processing

**Check:**
1. Browser console for error logs
2. Application logs in terminal
3. Verify `PdfOcrFieldExtractor.ExtractFieldsAsync()` is being called

**Possible Causes:**
- Pattern matching fails (expediente number not recognized)
- Field definitions mismatch
- OCR text quality too low

**Debugging:**
Add breakpoint or log in `ExtractExpedienteFromOcrAsync()` method

---

### **Issue 3: Comparison Fails**

**Symptom:** "PDF Expediente not extracted" warning when clicking Compare

**Cause:** `pdfExpediente` is null (see Issue 2)

**Workaround:** Fix field extraction first

---

## 📸 Screenshots to Capture for Demo

1. **Initial Page Load**
   - Clean UI with all 4 XML and 4 PDF buttons

2. **XML Extraction Success**
   - Green alert, fields displayed, data table populated

3. **PDF Processing Steps**
   - Progress bar at each stage
   - Side-by-side PDF viewer + OCR text

4. **Comparison Results**
   - Field-by-field comparison table
   - Confidence scores
   - Match/mismatch highlighting

5. **Fusion Results** (if working)
   - 3-way reconciliation table
   - Source reliability visualization
   - Overall confidence meter

---

## 🎨 Visual Quality Standards

### **Must Have:**
- ✅ All text is readable (contrast ratio >= 4.5:1)
- ✅ Buttons have clear hover states
- ✅ Loading states show progress (not just spinning)
- ✅ Error messages are user-friendly (not technical stack traces)
- ✅ Success messages are celebratory (emojis/colors)

### **Nice to Have:**
- ✅ Smooth animations (fade-in, slide-in)
- ✅ Tooltips on hover for technical terms
- ✅ Icons that reinforce meaning (checkmarks, warnings)
- ✅ Color-coded confidence levels (green/yellow/red)

---

## 🔧 If Build Fails

### **Common Errors:**

**Error: CS8602 - Dereference of possibly null reference**
- **Cause:** Null safety violation
- **Fix:** Add null checks or `!` operator where appropriate

**Error: Cannot resolve IFieldExtractor<PdfSource>**
- **Cause:** Service not registered in DI
- **Fix:** Register in `Program.cs`

**Error: Method not found**
- **Cause:** Interface/implementation mismatch
- **Fix:** Verify method signatures match interface

---

## 📊 Success Criteria

### **Functional:**
1. ✅ PDF loads and displays
2. ✅ OCR extraction works (text extracted)
3. ✅ **NEW:** Field extraction works (`pdfExpediente` populated)
4. ✅ Comparison uses real extracted data (not stub)
5. ✅ No console errors
6. ✅ No build errors

### **Visual:**
1. ✅ Professional appearance
2. ✅ Clear progress indicators
3. ✅ Readable in dark and light themes
4. ✅ Responsive layout works on different screen sizes
5. ✅ Smooth user experience (no jarring transitions)
6. ✅ Data is easy to read and understand

---

## 🚀 Next Steps After Visual Testing

### **If Everything Works:**
1. Create video recording of the demo flow
2. Take screenshots for presentation
3. Document any visual improvements needed
4. Proceed to Phase 3: 3-way reconciliation UI

### **If Issues Found:**
1. Document each issue with:
   - Screenshot
   - Error message
   - Steps to reproduce
2. Prioritize by severity (critical/high/medium/low)
3. Fix critical issues first
4. Retest after fixes

---

## 📝 Testing Notes Template

```markdown
### Test Date: _______
### Tester: _______
### Browser: _______
### Screen Size: _______

#### Test 1: XML Extraction
- [ ] Success
- [ ] Issues: _______________

#### Test 2: PDF Extraction
- [ ] Success
- [ ] Field extraction worked: _______________
- [ ] Issues: _______________

#### Test 3: Comparison
- [ ] Success
- [ ] Uses real pdfExpediente: _______________
- [ ] Issues: _______________

#### Visual Quality
- [ ] Theme compatibility: _______________
- [ ] Responsive design: _______________
- [ ] Professional appearance: _______________
- [ ] Issues: _______________

#### Overall Rating: ___ / 10

#### Recommendations:
1. _______________
2. _______________
3. _______________
```

---

**Last Updated:** 2025-12-08
**Status:** 🟢 READY FOR TESTING
**Next Action:** Build, run, and perform visual testing

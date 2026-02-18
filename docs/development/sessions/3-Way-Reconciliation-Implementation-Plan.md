# 3-Way Reconciliation Pipeline - Implementation Plan

## 📋 Overview

**Goal:** Build complete 3-way document reconciliation on document-processing page
**Timeline:** 2-5 days
**Demo Date:** CRITICAL for funding
**Status:** 🟢 IN PROGRESS

---

## 🎯 Vision: Single Source of Truth from 3 Untrusted Sources

```
┌─────────────────────────────────────────────────────────────┐
│                  3-Way Reconciliation Engine                │
│                                                               │
│  DOCX (Adaptive) ──┐                                         │
│                    ├──> Field Extraction ──┐                 │
│  XML (Untrusted)  ──┤                      ├──> RECONCILE ───┼──> ✅ Single Source of Truth
│                    ├──> Field Extraction ──┤                 │
│  PDF → OCR       ──┘                      ┘                 │
│                                                               │
│  Confidence Scoring | Conflict Resolution | Best Effort     │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Current State (Session 2025-12-08)

### ✅ **Completed:**
1. XML Extraction - Working with field display
2. PDF OCR Extraction - Working with text display
3. Real PDF Viewer - Side-by-side with OCR text (PDF.js)
4. Theme-aware UI components
5. Clear buttons for both XML and PDF
6. Fixed fixture filename mismatches

### ❌ **Missing for Full Pipeline:**
1. OCR → Field Extraction (extracts structured fields from OCR text)
2. DOCX Adaptive Extraction integration
3. 3-Way Reconciliation Engine
4. Comparison visualization (3-way table/matrix)
5. DOCX viewer component

---

## 🗓️ Implementation Phases (2-5 Day Plan)

### **Phase 1: Complete 2-Way (XML vs OCR)** - Day 1
**Goal:** Solid foundation with working 2-way reconciliation

#### Tasks:
1. ✅ Add OCR Field Extraction
   - Wire `IFieldExtractionService` after OCR completes
   - Extract: Expediente, Causa, Autoridad, Fechas, Montos
   - Store in `ocrProcessingResult.ExtractedFields`

2. ✅ Fix Comparison Button
   - Enable when both `xmlExpediente` and `ocrProcessingResult.ExtractedFields` exist
   - Update `canCompare` property logic

3. ✅ Implement 2-Way Comparison
   - Compare XML fields vs OCR extracted fields
   - Calculate similarity scores (Levenshtein, fuzzy matching)
   - Generate comparison result with confidence

4. ✅ UI Updates
   - Display extracted fields from OCR (same table format as XML)
   - Show 2-way comparison table
   - Highlight matches/mismatches

**Deliverable:** Working demo showing XML vs OCR reconciliation

---

### **Phase 2: Add DOCX Adaptive Extraction** - Day 2
**Goal:** Integrate third data source (DOCX)

#### Tasks:
1. ✅ Add DOCX Button Section
   - 4 DOCX fixture buttons (222AAA, 333BBB, 333ccc, 555CCC)
   - `LoadDocxFixture()` method
   - Processing status indicators

2. ✅ DOCX Field Extraction
   - Wire `AdaptiveDocxFixtureService`
   - Extract fields using adaptive patterns
   - Store in `docxProcessingResult`

3. ✅ DOCX Viewer Component
   - Display DOCX content (convert to HTML or use Word viewer)
   - Side-by-side with extracted fields
   - Alternative: Show first page as image

4. ✅ UI Layout Update
   - 3-column layout: DOCX | XML | PDF/OCR
   - Or tabbed interface: Source 1 (DOCX) | Source 2 (XML) | Source 3 (PDF)

**Deliverable:** All 3 sources loading and extracting fields

---

### **Phase 3: 3-Way Reconciliation Engine** - Day 3
**Goal:** Implement intelligent reconciliation algorithm

#### Tasks:
1. ✅ Create Reconciliation Service
   - Interface: `IReconciliationService`
   - Method: `ReconcileThreeWay(docxFields, xmlFields, ocrFields)`
   - Returns: `ReconciliationResult` with confidence scores

2. ✅ Reconciliation Algorithm
   - **Step 1:** Normalize fields (trim, lowercase, remove special chars)
   - **Step 2:** Compare each field across 3 sources
   - **Step 3:** Calculate similarity matrix (3x3 for each field)
   - **Step 4:** Determine "best" value using:
     - **Majority vote** (2/3 agree)
     - **Highest confidence** (if all different)
     - **Longest/most complete** (for text fields)
     - **Date validation** (for dates)
   - **Step 5:** Assign confidence score (0-100%)

3. ✅ Conflict Resolution Rules
   ```csharp
   public class ReconciliationRules
   {
       // If 2/3 sources match exactly → 95% confidence
       // If all 3 match → 99% confidence
       // If fuzzy match (90%+ similarity) → Use average, 80% confidence
       // If all different → Use DOCX (most reliable), 60% confidence
       // If DOCX missing → XML > OCR priority
   }
   ```

**Deliverable:** Working reconciliation engine with test cases

---

### **Phase 4: Visualization & UI** - Day 4
**Goal:** Make reconciliation results crystal clear for stakeholders

#### Tasks:
1. ✅ 3-Way Comparison Table
   ```
   ┌─────────────┬──────────┬─────────┬─────────┬───────────────┬────────────┐
   │ Field       │ DOCX     │ XML     │ OCR     │ Reconciled    │ Confidence │
   ├─────────────┼──────────┼─────────┼─────────┼───────────────┼────────────┤
   │ Expediente  │ 123/2025 │ 123/25  │ 123/025 │ 123/2025 ✓    │ 95%        │
   │ Causa       │ PENAL    │ PENAL   │ PENAL   │ PENAL ✓✓✓     │ 99%        │
   │ Autoridad   │ FGR      │ -       │ FOR     │ FGR ✓         │ 80%        │
   └─────────────┴──────────┴─────────┴─────────┴───────────────┴────────────┘
   ```

2. ✅ Visual Indicators
   - ✓✓✓ = All 3 sources agree (green)
   - ✓✓ = 2 sources agree (yellow)
   - ⚠️ = All different, best effort (orange)
   - ❌ = Low confidence (red)

3. ✅ Confidence Heat Map
   - Color-coded cells based on match quality
   - Tooltip showing similarity scores
   - Expandable details for conflicts

4. ✅ Export Functionality
   - Download reconciled data as JSON
   - Excel export with 3-way comparison
   - PDF report with confidence metrics

**Deliverable:** Impressive, stakeholder-ready UI

---

### **Phase 5: Polish & Testing** - Day 5
**Goal:** Production-ready demo

#### Tasks:
1. ✅ Parallel Batch Processing
   - Process 4 documents in parallel
   - Show performance improvement
   - Real-time progress indicators

2. ✅ Error Handling
   - Graceful failures if source missing
   - Show "2-way reconciliation" if only 2 sources available
   - Clear error messages

3. ✅ Verify Smart Implementations
   - Check DI container uses advanced extractors
   - Performance testing (< 10s for 3-way reconciliation)

4. ✅ End-to-End Testing
   - Test all 4 fixtures (222AAA, 333BBB, 333ccc, 555CCC)
   - Verify reconciliation accuracy
   - Check UI responsiveness

5. ✅ Demo Preparation
   - Create talking points document
   - Prepare scenario walkthrough
   - Test on clean browser

**Deliverable:** Polished, tested, demo-ready application

---

## 🏗️ Architecture

### **Key Services:**

1. **`IAdaptiveDocxExtractor`**
   - Input: DOCX file bytes
   - Output: Structured `Expediente` object
   - Location: `Application.Services`

2. **`IXmlExtractionService`**
   - Input: XML file bytes
   - Output: Structured `Expediente` object
   - Status: ✅ Already implemented

3. **`IOcrProcessingService`**
   - Input: PDF file bytes
   - Output: OCR text
   - Status: ✅ Already implemented

4. **`IFieldExtractionService`**
   - Input: OCR text
   - Output: Extracted fields (Expediente, Causa, etc.)
   - Status: ❌ Need to wire after OCR

5. **`IReconciliationService`** (NEW)
   - Input: 3x `Expediente` objects
   - Output: `ReconciliationResult` with confidence
   - Status: ❌ Need to create

### **Data Models:**

```csharp
public class ReconciliationResult
{
    public Expediente ReconciledData { get; set; } // Best effort single source
    public Dictionary<string, FieldReconciliation> FieldResults { get; set; }
    public float OverallConfidence { get; set; } // 0-100%
    public int MatchCount { get; set; }
    public int ConflictCount { get; set; }
}

public class FieldReconciliation
{
    public string FieldName { get; set; }
    public string? DocxValue { get; set; }
    public string? XmlValue { get; set; }
    public string? OcrValue { get; set; }
    public string ReconciledValue { get; set; }
    public float Confidence { get; set; }
    public ReconciliationStrategy Strategy { get; set; } // MajorityVote, HighestConfidence, Longest, etc.
    public string? ConflictReason { get; set; }
}

public enum ReconciliationStrategy
{
    AllAgree,           // 3/3 sources match
    MajorityVote,       // 2/3 sources match
    HighestConfidence,  // All different, use most reliable source
    FuzzyMatch,         // Similar but not exact
    SingleSource,       // Only one source available
    BestEffort          // Low confidence, using heuristics
}
```

---

## 📈 Success Metrics for Demo

### **Must Have:**
- ✅ All 3 sources (DOCX, XML, PDF) load and extract
- ✅ Reconciliation completes in < 10 seconds
- ✅ Visual 3-way comparison table displays clearly
- ✅ Confidence scores show > 85% for matching fields
- ✅ Side-by-side viewers for all 3 sources

### **Nice to Have:**
- ✅ Batch processing of 4 documents in parallel (< 30s)
- ✅ Export to Excel/JSON
- ✅ Heat map visualization
- ✅ Conflict resolution explanations

### **Demo Talking Points:**
1. **Problem:** "Legal documents arrive in 3 formats, all incomplete/untrusted"
2. **Solution:** "Our adaptive reconciliation creates single source of truth"
3. **Technology:** "Intelligent field extraction with confidence scoring"
4. **Results:** "95%+ accuracy, 3x faster than manual review"
5. **Scale:** "Handles hundreds of documents in parallel"

---

## 🚀 Next Immediate Action

**START HERE:** Add OCR field extraction (Phase 1, Task 1)

Let's begin implementing the field extraction after OCR processing!

---

**Last Updated:** 2025-12-08
**Owner:** Abel Briones
**Status:** 🟢 Ready to implement

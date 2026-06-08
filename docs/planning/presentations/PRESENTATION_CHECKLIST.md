> **⚠️ Status correction (2026-06-07):** This document's "production-ready" claims are aspirational / MVP-demo framing. The system is at **almost-ready, beta-test stage with important gaps remaining** — not production-ready. See docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md.

# MVP PRESENTATION CHECKLIST
**Incremental Implementation & Testing Strategy**

Last Updated: 2025-01-25
Status: 🔴 Not Started

---

## PIPELINE ARCHITECTURE

```
Step 1: Download → Step 2a: Pre-Parse Storage (date/filename) →
Step 3: OCR + Classify → Step 2b: Post-Parse Storage (date/type/filename) →
Step 4: Report Generation → Step 5: Search (database query)
```

---

## STEP 1: MULTI-SOURCE DOCUMENT DOWNLOAD

### 1.1 Foundation - Download to Disk ⏳
**Goal**: Prove we can download files and save to known location

- [ ] **Unit Test**: Download file to known path
  - [ ] Create test: `DownloadServiceTests.cs`
  - [ ] Test downloads file from URL
  - [ ] Test saves to specified disk location
  - [ ] Test verifies file exists after download
  - [ ] Test verifies file size > 0
  - [ ] **Acceptance**: Green test, file on disk

- [ ] **UI Test**: Download from UI button
  - [ ] Add download button to Home.razor
  - [ ] Wire up download service
  - [ ] Test downloads file when button clicked
  - [ ] Display success toast notification
  - [ ] **Acceptance**: Manual click → file appears on disk

### 1.2 SIARA Simulator Downloads ⏳
**Goal**: Download CNBV documents from SIARA simulator

- [ ] **Unit Test**: SIARA navigation + download
  - [ ] Test navigates to SIARA simulator URL
  - [ ] Test finds document links on page
  - [ ] Test downloads XML file
  - [ ] Test downloads PDF file
  - [ ] Test downloads DOCX file
  - [ ] **Acceptance**: All 3 file types downloaded successfully

- [ ] **UI Test**: SIARA button downloads documents
  - [ ] Click "Open SIARA" button
  - [ ] Select documents from simulator
  - [ ] Trigger download
  - [ ] Verify files saved to disk
  - [ ] **Acceptance**: Can download all 3 CNBV document types

### 1.3 Playwright - Internet Archive (Visible Mode) ⏳
**Goal**: Demonstrate real browser automation on public site

- [ ] **Unit Test**: Playwright automation
  - [ ] Test launches browser in visible mode (headless: false)
  - [ ] Test navigates to archive.org
  - [ ] Test searches for sample document
  - [ ] Test downloads document
  - [ ] Test closes browser
  - [ ] **Acceptance**: Browser visible during test, document downloaded

- [ ] **UI Test**: Internet Archive download
  - [ ] Click "Internet Archive" button
  - [ ] UI shows "Browser automation running..."
  - [ ] Browser opens visibly
  - [ ] Document downloads
  - [ ] Browser closes
  - [ ] Success toast shown
  - [ ] **Acceptance**: Stakeholder sees browser automation in action

### 1.4 Playwright - Gutenberg Library (Visible Mode) ⏳
**Goal**: Second real-site demo for credibility

- [ ] **Unit Test**: Gutenberg automation
  - [ ] Test launches browser (headless: false)
  - [ ] Test navigates to gutenberg.org
  - [ ] Test selects public domain book
  - [ ] Test downloads EPUB/PDF
  - [ ] **Acceptance**: Download succeeds

- [ ] **UI Test**: Gutenberg download
  - [ ] Click "Gutenberg Library" button
  - [ ] Visible browser automation
  - [ ] Book downloaded
  - [ ] **Acceptance**: Second automation demo working

---

## STEP 2a: PRE-PARSE STORAGE (RIGHT AFTER DOWNLOAD)

### 2a.1 Configuration - 3-Tier Storage Paths ⏳
**Goal**: Configure failover storage locations

- [ ] **appsettings.json**: Add storage configuration
  ```json
  "DocumentStorage": {
    "PrimaryPath": "F:\\PrismaDocuments\\Primary",
    "SecondaryPath": "D:\\PrismaDocuments\\Secondary",
    "TertiaryPath": "\\\\NetworkShare\\PrismaDocuments",
    "CreateHourSubfolder": true,
    "HourSubfolderVolumeThreshold": 50
  }
  ```
- [ ] **Unit Test**: Configuration loads correctly
  - [ ] Test reads all 3 paths
  - [ ] Test validates paths exist (or can be created)
  - [ ] **Acceptance**: Configuration binding works

### 2a.2 Folder Hierarchy Creation ⏳
**Goal**: Auto-create {Year}/{Month}/{Day}/[{Hour}]/ structure

- [ ] **Unit Test**: Folder creation service
  - [ ] Test creates Year folder
  - [ ] Test creates Month subfolder
  - [ ] Test creates Day subfolder
  - [ ] Test creates Hour subfolder (if volume threshold met)
  - [ ] Test skips Hour if below threshold
  - [ ] **Acceptance**: Correct hierarchy created

- [ ] **Integration Test**: Download → Auto-organize
  - [ ] Download test file
  - [ ] Verify saved to: `{Root}/2025/01/25/{OriginalFileName}`
  - [ ] Download 50 files
  - [ ] Verify Hour subfolder created: `{Root}/2025/01/25/14/{OriginalFileName}`
  - [ ] **Acceptance**: Files auto-organized by date

### 2a.3 Failover Logic (Skip, Log, Continue) ⏳
**Goal**: Non-blocking storage with alerts

- [ ] **Unit Test**: Storage failover
  - [ ] Test primary path succeeds → no fallback
  - [ ] Test primary fails → tries secondary
  - [ ] Test secondary fails → tries tertiary
  - [ ] Test all fail → logs error, continues (no exception)
  - [ ] **Acceptance**: System never blocks on storage failure

- [ ] **Integration Test**: Network failure simulation
  - [ ] Disconnect network drive (tertiary)
  - [ ] Download file
  - [ ] Verify saved to secondary
  - [ ] Verify error logged
  - [ ] Verify toast notification shown
  - [ ] **Acceptance**: Graceful degradation demonstrated

---

## STEP 3: OCR + CLASSIFICATION

### 3.1 ModelEnum - Requirement Types (SmartEnum) ⏳
**Goal**: Implement extensible requirement type classification

- [ ] **Port ModelEnum from IndTraceV2025**:
  - [ ] Copy `EnumModel.cs` to Prisma.Domain
  - [ ] Copy `IEnumModel.cs`
  - [ ] Copy `EnumLookUpTable.cs` and `ILookUpTable.cs`
  - [ ] Adapt namespace to `ExxerCube.Prisma.Domain.Enum`
  - [ ] **Acceptance**: ModelEnum compiles in Prisma

- [ ] **Create RequirementType ModelEnum**:
  ```csharp
  public class RequirementType : EnumModel
  {
      public static readonly RequirementType Judicial = new(100, "Judicial", "Solicitud de Información");
      public static readonly RequirementType Aseguramiento = new(101, "Aseguramiento", "Aseguramiento/Bloqueo");
      public static readonly RequirementType Desbloqueo = new(102, "Desbloqueo", "Desbloqueo");
      public static readonly RequirementType Transferencia = new(103, "Transferencia", "Transferencia Electrónica");
      public static readonly RequirementType SituacionFondos = new(104, "SituacionFondos", "Situación de Fondos");
      public static new readonly RequirementType Unknown = new(999, "Unknown", "Desconocido");
  }
  ```
  - [ ] **Unit Test**: All 5 types + Unknown accessible
  - [ ] **Unit Test**: FromValue(100) returns Judicial
  - [ ] **Unit Test**: FromValue(888) returns Unknown
  - [ ] **Acceptance**: ModelEnum working with legal types

### 3.2 Database Dictionary for Unknown Types ⏳
**Goal**: Persist new requirement types discovered at runtime

- [ ] **Create table**: `RequirementTypeDictionary`
  ```sql
  CREATE TABLE RequirementTypeDictionary (
      Id INT PRIMARY KEY,
      Name NVARCHAR(100),
      DisplayName NVARCHAR(200),
      DiscoveredAt DATETIME2,
      DiscoveredFromDocument NVARCHAR(500)
  )
  ```
- [ ] **Unit Test**: Insert unknown type to DB
  - [ ] Test saves new type with Id=888
  - [ ] Test retrieves type from DB
  - [ ] Test ModelEnum.FromValue(888) returns custom type
  - [ ] **Acceptance**: Dynamic types work

### 3.3 Classification Engine ⏳
**Goal**: Classify documents by requirement type using legal rules

- [ ] **Unit Test**: Keyword-based classification
  - [ ] Test "solicita información" → Judicial (100)
  - [ ] Test "asegurar" → Aseguramiento (101)
  - [ ] Test "desbloquear" → Desbloqueo (102)
  - [ ] Test "transferir" + "CLABE" → Transferencia (103)
  - [ ] Test "cheque de caja" → SituacionFondos (104)
  - [ ] Test no keywords → Unknown (999)
  - [ ] **Acceptance**: Classification logic working

- [ ] **Integration Test**: Classify real PRP1 fixtures
  - [ ] Load XML from Fixtures/PRP1/
  - [ ] Extract text content
  - [ ] Run classification
  - [ ] Verify correct requirement type assigned
  - [ ] **Acceptance**: Real documents classified correctly

### 3.4 Error Handling Demo (Imperfect Fixtures) ⏳
**Goal**: Show system handles real-world problems gracefully

- [ ] **Prepare 2-3 Imperfect Fixtures**:
  - [ ] Fixture 1: Missing fields (no NumeroRequerimiento)
  - [ ] Fixture 2: Unmatching data (XML amount ≠ PDF amount)
  - [ ] Fixture 3: Mistyping errors (OCR typos, wrong RFC format)
  - [ ] **Acceptance**: Bad fixtures ready for demo

- [ ] **Unit Test**: Missing field handling
  - [ ] Test loads fixture with missing field
  - [ ] Test extracts all available fields
  - [ ] Test flags missing field in result
  - [ ] Test continues processing (no crash)
  - [ ] **Acceptance**: Graceful handling

- [ ] **UI Test**: Error display
  - [ ] Process imperfect fixture
  - [ ] UI shows warning icon for missing fields
  - [ ] Tooltip explains what's missing
  - [ ] Document still processable
  - [ ] **Acceptance**: Errors visible to user

### 3.5 OCR Confidence Display ⏳
**Goal**: Show Tesseract → GOT-OCR2 fallback mechanism

- [ ] **Unit Test**: Confidence threshold
  - [ ] Test Tesseract extraction with high confidence (>80%)
  - [ ] Test uses Tesseract result
  - [ ] Test Tesseract with low confidence (<70%)
  - [ ] Test triggers GOT-OCR2 fallback
  - [ ] **Acceptance**: Fallback logic working

- [ ] **UI Test**: Confidence visualization
  - [ ] Process document
  - [ ] UI shows confidence score per field
  - [ ] Green badge for high confidence (>80%)
  - [ ] Yellow badge for medium (70-80%)
  - [ ] Red badge for low (<70%) + "Manual Review" flag
  - [ ] **Acceptance**: Visual confidence indicators

---

## STEP 2b: POST-CLASSIFICATION STORAGE

### 2b.1 File Reorganization ⏳
**Goal**: Move files from {Date}/ to {Date}/{RequirementType}/

- [ ] **Unit Test**: File move logic
  - [ ] Test moves file after classification
  - [ ] Test old path: `2025/01/25/document.pdf`
  - [ ] Test new path: `2025/01/25/Judicial/document.pdf`
  - [ ] Test updates database record with new path
  - [ ] **Acceptance**: Files reorganized correctly

- [ ] **Integration Test**: End-to-end reorganization
  - [ ] Download document (Step 1)
  - [ ] Save to pre-parse folder (Step 2a)
  - [ ] Classify as Aseguramiento (Step 3)
  - [ ] Move to `{Date}/Aseguramiento/` (Step 2b)
  - [ ] Verify database path updated
  - [ ] **Acceptance**: Full pipeline working

---

## STEP 4: REAL-TIME REPORTING

### 4.1 Processing Status Dashboard ⏳
**Goal**: Live view of document processing

- [ ] **UI Component**: ProcessingStatusCard.razor
  - [ ] Shows current document being processed
  - [ ] Shows progress bar (OCR % complete)
  - [ ] Shows elapsed time
  - [ ] Shows estimated time remaining
  - [ ] **Acceptance**: Real-time status visible

- [ ] **Integration Test**: Live updates
  - [ ] Start processing document
  - [ ] UI updates every second
  - [ ] Progress bar animates
  - [ ] Completion triggers success toast
  - [ ] **Acceptance**: Live dashboard working

### 4.2 Confidence Interval Display ⏳
**Goal**: Show confidence per field with visual indicators

- [ ] **UI Component**: ConfidenceFieldView.razor
  ```html
  <MudChip Color="@GetConfidenceColor(field.Confidence)">
      @field.Name: @field.Value (@field.Confidence%)
  </MudChip>
  ```
  - [ ] Green: >80% confidence
  - [ ] Yellow: 70-80%
  - [ ] Red: <70%
  - [ ] **Acceptance**: Color-coded confidence

### 4.3 Manual Review Workflow ⏳
**Goal**: Queue low-confidence extractions for human validation

- [ ] **Database**: ManualReviewQueue table
  - [ ] Stores document ID
  - [ ] Stores field name
  - [ ] Stores extracted value
  - [ ] Stores confidence score
  - [ ] Stores status (Pending/Approved/Rejected)

- [ ] **UI**: ManualReviewQueue.razor
  - [ ] List of pending reviews
  - [ ] Shows field + extracted value
  - [ ] Shows confidence score
  - [ ] Approve/Reject buttons
  - [ ] **Acceptance**: Review queue functional

- [ ] **Integration Test**: Low confidence triggers review
  - [ ] Process document with low OCR confidence
  - [ ] Verify item added to review queue
  - [ ] Verify toast notification: "Manual review required"
  - [ ] **Acceptance**: Workflow end-to-end

---

## STEP 5: HISTORICAL SEARCH (STAKEHOLDER WOW FACTOR 💎)

### 5.1 Database Table ⏳
**Goal**: Single table for searchable metadata

- [ ] **Create table**: ProcessedDocuments
  ```sql
  CREATE TABLE ProcessedDocuments (
      Id INT IDENTITY PRIMARY KEY,
      ProcessedDate DATETIME2,
      RequirementType INT, -- FK to RequirementTypeDictionary
      NumeroRequerimiento NVARCHAR(50),
      AutoridadRequiriente NVARCHAR(200),
      ClienteRFC NVARCHAR(13),
      ClienteNombre NVARCHAR(300),
      FilePath NVARCHAR(500),
      PdfPath NVARCHAR(500),
      XmlPath NVARCHAR(500),
      Confidence DECIMAL(5,2),
      Status NVARCHAR(50) -- Pending/Completed/ManualReview
  )
  ```
- [ ] **Migration**: Apply to database
- [ ] **Acceptance**: Table created

### 5.2 Search UI ⏳
**Goal**: User-friendly search with multiple filters

- [ ] **UI Component**: DocumentSearchView.razor
  - [ ] Date range picker (from/to)
  - [ ] Text input: Request number
  - [ ] Dropdown: Authority type
  - [ ] Text input: Client RFC
  - [ ] Dropdown: Requirement type
  - [ ] Search button
  - [ ] **Acceptance**: Search form renders

### 5.3 Search Query Implementation ⏳
**Goal**: Fast, filterable queries

- [ ] **Service**: DocumentSearchService.cs
  ```csharp
  public Task<List<ProcessedDocumentDto>> SearchAsync(
      DateTime? fromDate,
      DateTime? toDate,
      string? requestNumber,
      string? authority,
      string? clientRfc,
      int? requirementType)
  ```
- [ ] **Unit Test**: Search filters work
  - [ ] Test date range filter
  - [ ] Test request number exact match
  - [ ] Test authority partial match
  - [ ] Test multiple filters combined
  - [ ] **Acceptance**: All filters functional

### 5.4 Results Display + PDF/XML Viewer ⏳
**Goal**: Side-by-side document viewing

- [ ] **UI Component**: SearchResultsGrid.razor
  - [ ] MudDataGrid with results
  - [ ] Columns: Date, Type, Request#, Authority, Client, Confidence, Status
  - [ ] Row click → opens viewer
  - [ ] **Acceptance**: Results grid working

- [ ] **UI Component**: DocumentViewer.razor
  - [ ] Left pane: PDF viewer (iframe or PDF.js)
  - [ ] Right pane: XML viewer (syntax-highlighted)
  - [ ] Close button
  - [ ] Download both button
  - [ ] **Acceptance**: Side-by-side view functional

### 5.5 Export Results ⏳
**Goal**: Export search results to CSV/Excel

- [ ] **Service**: ExportService.cs
  - [ ] Export to CSV
  - [ ] Export to Excel (EPPlus library)
  - [ ] Include all columns from search results

- [ ] **UI**: Export button on SearchResultsGrid
  - [ ] Click "Export to Excel"
  - [ ] File downloads
  - [ ] Open Excel → verify data
  - [ ] **Acceptance**: Export working

---

## INTEGRATION TESTING - FULL PIPELINE

### End-to-End Workflow Test ⏳
**Goal**: Prove entire pipeline works together

- [ ] **Test Scenario 1: Happy Path (SIARA)**
  1. Click "Open SIARA" button
  2. Download XML + PDF + DOCX
  3. Files saved to `{Primary}/{Year}/{Month}/{Day}/`
  4. OCR extracts text (Tesseract, high confidence)
  5. Classification identifies: Judicial (100)
  6. Files moved to `{Primary}/{Year}/{Month}/{Day}/Judicial/`
  7. Database record created
  8. Report generated (confidence >80%, no manual review)
  9. Search finds document by request number
  10. **Acceptance**: Full pipeline green

- [ ] **Test Scenario 2: Error Handling (Imperfect Fixture)**
  1. Load fixture with missing fields
  2. OCR has low confidence (<70%)
  3. Classification succeeds (partial data)
  4. Manual review queue entry created
  5. Toast notification: "Manual review required"
  6. User approves from review queue
  7. Status updated to "Completed"
  8. **Acceptance**: Error path works

- [ ] **Test Scenario 3: Failover Storage**
  1. Disable primary storage path
  2. Download document
  3. Saved to secondary path
  4. Error logged
  5. Processing continues normally
  6. **Acceptance**: Resilience demonstrated

- [ ] **Test Scenario 4: Unknown Requirement Type**
  1. Process document with unrecognized keywords
  2. Classified as Unknown (999)
  3. New entry in RequirementTypeDictionary
  4. File saved to `{Date}/Unknown/`
  5. Searchable in Step 5
  6. **Acceptance**: Extensibility working

---

## DEMO SCRIPT PREPARATION

### Demo Flow Document ⏳
- [ ] **Write demo script**: StakeholderDemo.md
  - [ ] Introduction (30 seconds): Problem statement
  - [ ] Step 1 Demo (2 mins): Multi-source downloads
  - [ ] Step 2 Demo (1 min): Auto-organization with failover
  - [ ] Step 3 Demo (3 mins): Classification + error handling
  - [ ] Step 4 Demo (2 mins): Real-time reporting + manual review
  - [ ] Step 5 Demo (2 mins): Search + PDF/XML viewer
  - [ ] Closing (1 min): ROI narrative, next steps (P1)
  - [ ] **Total**: 11 minutes + 4 minutes Q&A = 15-minute presentation

### Talking Points ⏳
- [ ] **Architecture**: Hexagonal, 600+ tests, clean patterns (beta-test stage, not yet production-ready)
- [ ] **Compliance**: Legal requirements from CNBV regulations
- [ ] **ROI**: Developer cost vs Mexican lawyer time savings
- [ ] **Resilience**: Failover storage, graceful error handling
- [ ] **Extensibility**: SmartEnum for new requirement types

---

## SUCCESS CRITERIA

### Technical ✅
- [ ] All unit tests green
- [ ] All integration tests green
- [ ] UI responsive, no crashes
- [ ] Database migrations applied
- [ ] All 5 steps demonstrable

### Business ✅
- [ ] Complete pipeline demo (15 minutes)
- [ ] Handles imperfect data gracefully
- [ ] Search feature impresses stakeholders
- [ ] Clear path to P1 articulated
- [ ] Stakeholder approval to proceed

---

## CURRENT STATUS

- **Overall Progress**: 0% (Not started)
- **Next Action**: Implement Step 1.1 - Download to Disk (Unit Test)
- **Blocker**: SQL Server logon trigger (database migrations)
- **Timeline**: 3-5 days after database issue resolved

---

## NOTES

- **ModelEnum Source**: F:\Dynamic\IndTraceV2025\Src\Code\Core\Domain\Enum
- **Legal Research**: Completed - see Prisma/Docs/Legal/
- **Serialization**: Not yet in ModelEnum, add if needed for API
- **Test Philosophy**: Test each step in isolation, then integration

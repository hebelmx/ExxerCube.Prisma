# PRESENTATION PLAN MVP - SYSTEMATIC EXECUTION STRATEGY
**ExxerCube.Prisma - CNBV RegTech Solution**

**Created**: 2025-01-25
**Status**: 🔴 Foundation Phase - Database Setup
**Target Demo**: TBA (This week or next week)

---

## EXECUTIVE SUMMARY

### **Mission**
Demonstrate a working MVP to stakeholders showing automated CNBV regulatory compliance for Mexican banks using a 5-step presentation flow.

### **Current Reality Check** (2025-01-25 Morning)
```
✅ Architecture: 600+ tests passing, hexagonal architecture validated
✅ OCR System: Tesseract + GOT-OCR2 fallback working
✅ DI Registration: Comprehensive (Program.cs lines 105-229)
✅ Legal Research: Completed - 5 requirement types documented
✅ Navigation UI: 3 sources (SIARA, Archive, Gutenberg) implemented
⚠️ Database: Migrations PENDING (blocking app startup)
❌ Download System: Not implemented
❌ Storage Organization: Not implemented
❌ Classification UI: Not implemented
❌ Reporting Dashboard: Not implemented
❌ Search Feature: Not implemented
```

**Revised Completion Estimate**: 50% → **Foundation: 70% | Features: 30%**

---

## PHASE 0: FOUNDATION - GET WEB APP RUNNING ⏳

### **Critical Blocker Resolution**
**Problem**: Web app crashes on startup due to pending database migrations

**Root Cause Analysis**:
1. ApplicationDbContext migration: `00000000000000_CreateIdentitySchema` (Pending)
2. PrismaDbContext migrations (4 pending):
   - `20251114070952_AddFileMetadataTable`
   - `20250115000000_AddSLAStatusTable`
   - `20250116000000_AddReviewCasesAndReviewDecisionsTables`
   - `20250116000000_AddAuditRecordsTable`
3. Connection String: LocalDB (good for dev)
   - `Server=(localdb)\\mssqllocaldb;Database=aspnet-ExxerCube.Prisma.Web.UI-...`

### **Foundation Tasks** (MUST COMPLETE FIRST)

#### Task 0.1: Apply ApplicationDbContext Migrations ⏳
```bash
cd "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\UI\ExxerCube.Prisma.Web.UI"
dotnet ef database update --context ApplicationDbContext
```
**Acceptance**:
- Migration applied successfully
- Identity tables created (AspNetUsers, AspNetRoles, etc.)

#### Task 0.2: Apply PrismaDbContext Migrations ⏳
```bash
cd "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\Infrastructure.Database"
dotnet ef database update --context PrismaDbContext --startup-project "../UI/ExxerCube.Prisma.Web.UI"
```
**Acceptance**:
- All 4 migrations applied
- Tables created: FileMetadata, Persona, SLAStatus, ReviewCases, ReviewDecisions, AuditRecords

#### Task 0.3: Test Web App Startup ⏳
```bash
cd "F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\UI\ExxerCube.Prisma.Web.UI"
dotnet run
```
**Acceptance**:
- App starts without exceptions
- Browser opens to https://localhost:7062
- Home page renders
- No DI errors in console

#### Task 0.4: Verify DI Registration (Sanity Check) ⏳
**Services Already Registered** (from Program.cs analysis):
- ✅ Lines 127-131: `AddPrismaPythonEnvironment()`, `AddMetricsServices()`
- ✅ Lines 174-191: `AddDatabaseServices()`, `AddBrowserAutomationServices()`, `AddFileStorageServices()`
- ✅ Line 194-195: `ISpecificationFactory` → `SpecificationFactory`
- ✅ Lines 198-201: `AddExtractionServices()`, `AddClassificationServices()`, `MetadataExtractionService`
- ✅ Lines 203-206: `FieldMatchingService`, `IFieldMatcher<T>` for DOCX and PDF
- ✅ Lines 209-228: `DecisionLogicService`, `SLATrackingService`, Export services, `AuditReportingService`, Health checks

**Manual Verification**:
- Open browser developer tools (F12)
- Check console for DI errors
- Navigate to /health endpoint
- Verify health checks respond

---

## 5-STEP PRESENTATION FLOW - IMPLEMENTATION ROADMAP

### **Pipeline Architecture** (Sequential Processing)
```
Step 1: Download
  → Step 2a: Pre-Parse Storage (date/filename)
  → Step 3: OCR + Classify
  → Step 2b: Post-Parse Storage (date/type/filename)
  → Step 4: Report Generation
  → Step 5: Search (database query)
```

---

## STEP 1: MULTI-SOURCE DOCUMENT DOWNLOAD

### **Goal**: Prove download capability from 3 sources (SIARA, Archive, Gutenberg)

**Current Status**:
- ✅ Navigation targets configured (appsettings.json lines 12-16)
- ✅ UI buttons exist (Home.razor)
- ❌ Actual download logic missing
- ❌ Playwright automation not implemented

### Task 1.1: Download Service - Unit Test First ⏳
**File**: `Tests.Infrastructure.BrowserAutomation/DownloadServiceTests.cs`

```csharp
[Fact]
public async Task DownloadFile_ShouldSaveToKnownLocation()
{
    // Arrange
    var service = new DownloadService(_httpClient, _logger);
    var url = "https://www.gutenberg.org/cache/epub/1/pg1.txt";
    var outputPath = "F:\\Temp\\TestDownload\\pg1.txt";

    // Act
    var result = await service.DownloadFileAsync(url, outputPath);

    // Assert
    result.Success.ShouldBeTrue();
    File.Exists(outputPath).ShouldBeTrue();
    new FileInfo(outputPath).Length.ShouldBeGreaterThan(0);
}
```

**Acceptance**:
- Green test
- File physically on disk
- Size > 0 bytes

### Task 1.2: Download Service - Implementation ⏳
**File**: `Infrastructure.BrowserAutomation/Services/DownloadService.cs`

```csharp
public class DownloadService : IDownloadService
{
    public async Task<DownloadResult> DownloadFileAsync(
        string url,
        string outputPath,
        CancellationToken ct = default)
    {
        // Create directory if not exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Download using HttpClient
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        // Write to file
        await using var fileStream = File.Create(outputPath);
        await response.Content.CopyToAsync(fileStream, ct);

        return new DownloadResult
        {
            Success = true,
            FilePath = outputPath,
            FileSizeBytes = new FileInfo(outputPath).Length
        };
    }
}
```

### Task 1.3: Wire Download to UI ⏳
**File**: `Web.UI/Components/Pages/Home.razor`

```razor
@page "/"
@inject IDownloadService DownloadService
@inject ISnackbar Snackbar

<MudButton OnClick="DownloadFromSiara" Color="Color.Primary">
    Download from SIARA
</MudButton>

@code {
    private async Task DownloadFromSiara()
    {
        var url = "https://localhost:5002/sample-documents/test.pdf";
        var output = "F:\\PrismaDownloads\\test.pdf";

        var result = await DownloadService.DownloadFileAsync(url, output);

        if (result.Success)
        {
            Snackbar.Add($"Downloaded: {result.FilePath}", Severity.Success);
        }
        else
        {
            Snackbar.Add($"Error: {result.ErrorMessage}", Severity.Error);
        }
    }
}
```

**Acceptance**:
- Click button → file downloads
- Toast notification shows success
- File exists at expected path

### Task 1.4: Playwright - Internet Archive (Visible Mode) ⏳
**Reference Implementation**: Use existing `GutenbergNavigationTarget.cs` pattern

```csharp
public class InternetArchiveDownloadService
{
    public async Task<string> DownloadDocumentAsync(
        string searchTerm,
        string outputFolder)
    {
        // Launch browser (headless: false for demo)
        var browser = await Playwright.Chromium.LaunchAsync(
            new() { Headless = false });

        var page = await browser.NewPageAsync();
        await page.GotoAsync("https://archive.org");

        // Search for document
        await page.FillAsync("#search-bar", searchTerm);
        await page.ClickAsync("#search-button");

        // Wait for results
        await page.WaitForSelectorAsync(".item-ia");

        // Click first result
        await page.ClickAsync(".item-ia:first-child");

        // Find PDF download link
        var downloadLink = await page.Locator("a:has-text('PDF')").GetAttributeAsync("href");

        // Download using HttpClient (faster than Playwright download)
        var fileName = Path.Combine(outputFolder, $"{searchTerm}.pdf");
        await _downloadService.DownloadFileAsync(downloadLink, fileName);

        await browser.CloseAsync();
        return fileName;
    }
}
```

**Acceptance**:
- Browser opens visibly
- Navigates to Archive.org
- Searches and downloads document
- Stakeholder sees automation in action

### Task 1.5: Playwright - Gutenberg (Visible Mode) ⏳
**Similar implementation to Task 1.4**

**Acceptance**:
- Second automation demo working
- Downloads public domain book

---

## STEP 2a: PRE-PARSE STORAGE (RIGHT AFTER DOWNLOAD)

### **Goal**: Auto-organize downloaded files by date before processing

**Current Status**:
- ⚠️ appsettings.json only has `FileStorage.StorageBasePath: "./Downloads"` (line 17-19)
- ❌ Missing 3-tier failover paths
- ❌ Missing folder hierarchy logic

### Task 2a.1: Update Configuration Schema ⏳
**File**: `Web.UI/appsettings.json`

```json
"DocumentStorage": {
  "PrimaryPath": "F:\\PrismaDocuments\\Primary",
  "SecondaryPath": "D:\\PrismaDocuments\\Secondary",
  "TertiaryPath": "\\\\NetworkShare\\PrismaDocuments",
  "CreateHourSubfolder": true,
  "HourSubfolderVolumeThreshold": 50,
  "EnableFailover": true
}
```

### Task 2a.2: Folder Hierarchy Service ⏳
**File**: `Infrastructure.FileStorage/Services/FolderHierarchyService.cs`

```csharp
public class FolderHierarchyService : IFolderHierarchyService
{
    public string BuildPreParsePath(
        string rootPath,
        DateTime timestamp,
        string originalFileName,
        int currentHourVolume)
    {
        var year = timestamp.Year.ToString();
        var month = timestamp.Month.ToString("D2");
        var day = timestamp.Day.ToString("D2");
        var hour = timestamp.Hour.ToString("D2");

        // Build path: {Root}/{Year}/{Month}/{Day}/[{Hour}]/{FileName}
        var pathBuilder = new StringBuilder(rootPath);
        pathBuilder.Append(Path.DirectorySeparatorChar).Append(year);
        pathBuilder.Append(Path.DirectorySeparatorChar).Append(month);
        pathBuilder.Append(Path.DirectorySeparatorChar).Append(day);

        // Add hour subfolder if volume exceeds threshold
        if (_options.CreateHourSubfolder && currentHourVolume >= _options.HourSubfolderVolumeThreshold)
        {
            pathBuilder.Append(Path.DirectorySeparatorChar).Append(hour);
        }

        pathBuilder.Append(Path.DirectorySeparatorChar).Append(originalFileName);

        return pathBuilder.ToString();
    }
}
```

### Task 2a.3: Failover Storage Service ⏳
**File**: `Infrastructure.FileStorage/Services/FailoverStorageService.cs`

```csharp
public async Task<StorageResult> SaveWithFailoverAsync(
    Stream fileStream,
    string relativePath,
    CancellationToken ct = default)
{
    // Try primary
    var primaryResult = await TrySaveAsync(_options.PrimaryPath, fileStream, relativePath, ct);
    if (primaryResult.Success) return primaryResult;

    _logger.LogWarning("Primary storage failed: {Error}. Trying secondary...", primaryResult.Error);

    // Try secondary
    fileStream.Position = 0; // Reset stream
    var secondaryResult = await TrySaveAsync(_options.SecondaryPath, fileStream, relativePath, ct);
    if (secondaryResult.Success) return secondaryResult;

    _logger.LogWarning("Secondary storage failed: {Error}. Trying tertiary...", secondaryResult.Error);

    // Try tertiary
    fileStream.Position = 0;
    var tertiaryResult = await TrySaveAsync(_options.TertiaryPath, fileStream, relativePath, ct);
    if (tertiaryResult.Success) return tertiaryResult;

    // All failed - log but don't throw
    _logger.LogError("ALL storage tiers failed for {RelativePath}. Processing will continue.", relativePath);
    return new StorageResult { Success = false, ErrorMessage = "All storage tiers failed" };
}
```

**Acceptance**:
- Primary saves successfully → no fallback
- Primary fails → tries secondary
- All fail → logs error, continues (non-blocking)

---

## STEP 3: OCR + CLASSIFICATION

### **Goal**: Extract text, classify document type, handle errors gracefully

**Current Status**:
- ✅ OCR services registered (Program.cs line 125)
- ✅ Classification services registered (line 199)
- ❌ ModelEnum not ported yet
- ❌ RequirementType enum not created
- ❌ Database dictionary table missing

### Task 3.1: Port ModelEnum from IndTraceV2025 ⏳
**Source**: `F:\Dynamic\IndTraceV2025\Src\Code\Core\Domain\Enum\`

**Files to Copy**:
1. `EnumModel.cs` → `ExxerCube.Prisma.Domain/Enum/EnumModel.cs`
2. `IEnumModel.cs` → `ExxerCube.Prisma.Domain/Enum/IEnumModel.cs`
3. `EnumLookUpTable.cs` → `ExxerCube.Prisma.Domain/Enum/LookUpTable/EnumLookUpTable.cs`
4. `ILookUpTable.cs` → `ExxerCube.Prisma.Domain/Enum/LookUpTable/ILookUpTable.cs`

**Changes Needed**:
- Namespace: `IndTrace.Domain.Enum` → `ExxerCube.Prisma.Domain.Enum`
- Remove dependency on `IndTrace.Domain.Interfaces.ILookupEntity`
- Add serialization support (JSON converters for API)

**Unit Tests to Port**:
- `EnumModelComprehensiveTests.cs` → Verify `FromValue()`, `FromName()`, `InvalidValue()` work correctly

**Acceptance**:
- ModelEnum compiles in Prisma.Domain
- All unit tests green
- No breaking dependencies

### Task 3.2: Create RequirementType ModelEnum ⏳
**File**: `ExxerCube.Prisma.Domain/Enum/RequirementType.cs`

**Using legal research from `Prisma/Docs/Legal/SmartEnum_RequirementTypes.md`**:

```csharp
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents CNBV requirement types based on R29-2911 and Disposiciones SIARA.
/// Values match official CNBV type codes from legal documentation.
/// </summary>
public class RequirementType : EnumModel
{
    /// <summary>
    /// Solicitud de Información (Information Request) - Art. 142 LIC
    /// Keywords: "solicita información", "estados de cuenta"
    /// </summary>
    public static readonly RequirementType Judicial = new(100, "Judicial", "Solicitud de Información");

    /// <summary>
    /// Aseguramiento/Bloqueo (Seizure/Freezing) - Art. 2(V)(b)
    /// Keywords: "asegurar", "bloquear", "embargar"
    /// </summary>
    public static readonly RequirementType Aseguramiento = new(101, "Aseguramiento", "Aseguramiento/Bloqueo");

    /// <summary>
    /// Desbloqueo (Unblocking) - R29 Type 102
    /// Keywords: "desbloquear", "liberar"
    /// </summary>
    public static readonly RequirementType Desbloqueo = new(102, "Desbloqueo", "Desbloqueo");

    /// <summary>
    /// Transferencia Electrónica (Electronic Transfer) - R29 Type 103
    /// Keywords: "transferir", "CLABE"
    /// </summary>
    public static readonly RequirementType Transferencia = new(103, "Transferencia", "Transferencia Electrónica");

    /// <summary>
    /// Situación de Fondos (Cashier's Check) - R29 Type 104
    /// Keywords: "cheque de caja", "poner a disposición"
    /// </summary>
    public static readonly RequirementType SituacionFondos = new(104, "SituacionFondos", "Situación de Fondos");

    /// <summary>
    /// Unknown type for unrecognized requirements at classification time.
    /// Triggers database dictionary lookup for dynamic types.
    /// </summary>
    public static new readonly RequirementType Unknown = new(999, "Unknown", "Desconocido");

    // Parameterless constructor required by EF Core
    public RequirementType() { }

    private RequirementType(int value, string name, string displayName = "")
        : base(value, name, displayName)
    {
    }

    // Convenience methods
    public static RequirementType FromValue(int value) => FromValue<RequirementType>(value);
    public static RequirementType FromName(string name) => FromName<RequirementType>(name);
}
```

**Unit Tests**:
```csharp
[Fact]
public void RequirementType_AllKnownTypes_ShouldBeAccessible()
{
    RequirementType.Judicial.Value.ShouldBe(100);
    RequirementType.Aseguramiento.Value.ShouldBe(101);
    RequirementType.Desbloqueo.Value.ShouldBe(102);
    RequirementType.Transferencia.Value.ShouldBe(103);
    RequirementType.SituacionFondos.Value.ShouldBe(104);
    RequirementType.Unknown.Value.ShouldBe(999);
}

[Fact]
public void RequirementType_FromValue_WithUnknownValue_ShouldReturnUnknown()
{
    var result = RequirementType.FromValue(888);
    result.ShouldBe(RequirementType.Unknown);
}
```

### Task 3.3: Database Dictionary Table ⏳
**Migration**: `AddRequirementTypeDictionary`

```sql
CREATE TABLE RequirementTypeDictionary (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    DiscoveredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DiscoveredFromDocument NVARCHAR(500) NULL,
    KeywordPattern NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedBy NVARCHAR(256) NULL,
    Notes NVARCHAR(MAX) NULL
);

-- Seed known types from legal research
INSERT INTO RequirementTypeDictionary (Id, Name, DisplayName, KeywordPattern) VALUES
(100, 'Judicial', 'Solicitud de Información', 'solicita información|estados de cuenta'),
(101, 'Aseguramiento', 'Aseguramiento/Bloqueo', 'asegurar|bloquear|embargar'),
(102, 'Desbloqueo', 'Desbloqueo', 'desbloquear|liberar'),
(103, 'Transferencia', 'Transferencia Electrónica', 'transferir|CLABE'),
(104, 'SituacionFondos', 'Situación de Fondos', 'cheque de caja|poner a disposición');
```

**Entity**:
```csharp
public class RequirementTypeDictionaryEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public DateTime DiscoveredAt { get; set; }
    public string? DiscoveredFromDocument { get; set; }
    public string? KeywordPattern { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
}
```

### Task 3.4: Classification Engine with Legal Rules ⏳
**File**: `Infrastructure.Classification/Services/RequirementClassifier.cs`

**Using decision tree from `Prisma/Docs/Legal/ClassificationRules.md`**:

```csharp
public class RequirementClassifier : IRequirementClassifier
{
    public async Task<ClassificationResult> ClassifyAsync(
        string textContent,
        CancellationToken ct = default)
    {
        var confidence = 0.0;

        // Level 4: Classify type using keyword pattern matching
        if (ContainsKeywords(textContent, "solicita información", "estados de cuenta"))
        {
            return new ClassificationResult
            {
                Type = RequirementType.Judicial,
                Confidence = 0.95,
                MatchedKeywords = new[] { "solicita información" }
            };
        }

        if (ContainsKeywords(textContent, "asegurar", "bloquear", "embargar"))
        {
            return new ClassificationResult
            {
                Type = RequirementType.Aseguramiento,
                Confidence = 0.92,
                MatchedKeywords = new[] { "asegurar" }
            };
        }

        if (ContainsKeywords(textContent, "desbloquear", "liberar"))
        {
            return new ClassificationResult
            {
                Type = RequirementType.Desbloqueo,
                Confidence = 0.90,
                MatchedKeywords = new[] { "desbloquear" }
            };
        }

        if (ContainsKeywords(textContent, "transferir") && textContent.Contains("CLABE"))
        {
            return new ClassificationResult
            {
                Type = RequirementType.Transferencia,
                Confidence = 0.88,
                MatchedKeywords = new[] { "transferir", "CLABE" }
            };
        }

        if (ContainsKeywords(textContent, "cheque de caja", "poner a disposición"))
        {
            return new ClassificationResult
            {
                Type = RequirementType.SituacionFondos,
                Confidence = 0.85,
                MatchedKeywords = new[] { "cheque de caja" }
            };
        }

        // Unknown type - check database dictionary for dynamic types
        var dynamicType = await _dictRepository.FindByKeywordMatchAsync(textContent, ct);
        if (dynamicType != null)
        {
            return new ClassificationResult
            {
                Type = RequirementType.FromValue(dynamicType.Id),
                Confidence = 0.70,
                MatchedKeywords = new[] { dynamicType.KeywordPattern },
                IsDynamicType = true
            };
        }

        // Truly unknown
        return new ClassificationResult
        {
            Type = RequirementType.Unknown,
            Confidence = 0.50,
            RequiresManualReview = true
        };
    }
}
```

### Task 3.5: Error Handling Demo - Imperfect Fixtures ⏳
**Prepare 3 test fixtures** in `Prisma/Fixtures/PRP1/Imperfect/`:

1. **MissingFields.xml**: Missing `NumeroRequerimiento`
2. **UnmatchingData.xml**: XML amount ≠ PDF amount
3. **MistypingErrors.xml**: Wrong RFC format, OCR typos

**UI Component**: `ErrorDisplayCard.razor`
```razor
@if (result.HasErrors)
{
    <MudAlert Severity="Severity.Warning">
        <MudText>⚠️ Imperfections detected:</MudText>
        <ul>
            @foreach (var error in result.Errors)
            {
                <li>@error.FieldName: @error.Description</li>
            }
        </ul>
        <MudText Typo="Typo.caption">Document still processable, manual review recommended.</MudText>
    </MudAlert>
}
```

**Acceptance**:
- Process imperfect fixture
- UI shows warnings (not errors)
- Processing continues
- Stakeholder sees resilience

### Task 3.6: OCR Confidence Display ⏳
**UI Component**: `ConfidenceIndicator.razor`

```razor
<MudChip Color="@GetConfidenceColor(Confidence)" Size="Size.Small">
    @FieldName: @Value (@Confidence.ToString("P0"))
</MudChip>

@code {
    [Parameter] public string FieldName { get; set; } = null!;
    [Parameter] public string Value { get; set; } = null!;
    [Parameter] public double Confidence { get; set; }

    private Color GetConfidenceColor(double confidence) => confidence switch
    {
        >= 0.80 => Color.Success,  // Green
        >= 0.70 => Color.Warning,  // Yellow
        _ => Color.Error           // Red
    };
}
```

**Acceptance**:
- Green badge for high confidence (>80%)
- Yellow badge for medium (70-80%)
- Red badge for low (<70%) + "Manual Review" flag

---

## STEP 2b: POST-CLASSIFICATION STORAGE

### **Goal**: Move files from {Date}/ to {Date}/{RequirementType}/ after classification

### Task 2b.1: File Reorganization Service ⏳
**File**: `Infrastructure.FileStorage/Services/FileReorganizer.cs`

```csharp
public async Task ReorganizeAfterClassificationAsync(
    string currentPath,
    RequirementType type,
    Guid fileMetadataId,
    CancellationToken ct = default)
{
    // Current: F:\PrismaDocuments\2025\01\25\document.pdf
    // New:     F:\PrismaDocuments\2025\01\25\Judicial\document.pdf

    var directory = Path.GetDirectoryName(currentPath)!;
    var fileName = Path.GetFileName(currentPath);
    var typeFolder = Path.Combine(directory, type.Name);

    Directory.CreateDirectory(typeFolder);

    var newPath = Path.Combine(typeFolder, fileName);
    File.Move(currentPath, newPath);

    // Update database record
    await _fileMetadataRepository.UpdateFilePathAsync(
        fileMetadataId,
        newPath,
        ct);

    _logger.LogInformation(
        "File reorganized: {Old} → {New} (Type: {Type})",
        currentPath,
        newPath,
        type.DisplayName);
}
```

**Integration Test**:
```csharp
[Fact]
public async Task EndToEnd_DownloadClassifyReorganize_ShouldUpdatePath()
{
    // 1. Download
    var downloadResult = await _downloadService.DownloadFileAsync(url, preParsePath);

    // 2. Save to pre-parse location
    var preParsePath = "F:\\Temp\\2025\\01\\25\\test.pdf";
    File.Exists(preParsePath).ShouldBeTrue();

    // 3. Classify
    var classification = await _classifier.ClassifyAsync(textContent);
    classification.Type.ShouldBe(RequirementType.Judicial);

    // 4. Reorganize
    await _reorganizer.ReorganizeAfterClassificationAsync(
        preParsePath,
        classification.Type,
        fileMetadataId);

    // 5. Verify new location
    var postParsePath = "F:\\Temp\\2025\\01\\25\\Judicial\\test.pdf";
    File.Exists(postParsePath).ShouldBeTrue();
    File.Exists(preParsePath).ShouldBeFalse(); // Old path gone

    // 6. Verify database updated
    var metadata = await _repo.GetByIdAsync(fileMetadataId);
    metadata.FilePath.ShouldBe(postParsePath);
}
```

---

## STEP 4: REAL-TIME REPORTING

### **Goal**: Live processing status with confidence intervals and manual review workflow

### Task 4.1: Processing Status Dashboard (SignalR) ⏳
**Hub**: Already registered at line 74 (`ProcessingHub`)

**Component**: `ProcessingStatusCard.razor`
```razor
@implements IAsyncDisposable
@inject NavigationManager Navigation

<MudCard>
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">
                Processing Status
            </MudText>
        </CardHeaderContent>
    </MudCardHeader>
    <MudCardContent>
        @if (_isProcessing)
        {
            <MudText>Document: @_currentDocument</MudText>
            <MudProgressLinear Value="@_progress" Color="Color.Primary" />
            <MudText Typo="Typo.caption">
                @_progress.ToString("P0") complete - @_elapsedTime.ToString(@"mm\:ss") elapsed
            </MudText>
        }
        else
        {
            <MudText Color="Color.Success">✓ Processing complete</MudText>
        }
    </MudCardContent>
</MudCard>

@code {
    private HubConnection? _hubConnection;
    private bool _isProcessing;
    private string _currentDocument = "";
    private double _progress;
    private TimeSpan _elapsedTime;

    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/processingHub"))
            .Build();

        _hubConnection.On<string, double, TimeSpan>("ProcessingUpdate",
            (doc, progress, elapsed) =>
            {
                _currentDocument = doc;
                _progress = progress;
                _elapsedTime = elapsed;
                _isProcessing = progress < 1.0;
                StateHasChanged();
            });

        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
```

**Service** (already exists, just use it):
```csharp
// From existing ProcessingHub
await Clients.All.SendAsync("ProcessingUpdate",
    documentName,
    progressPercent,
    elapsed);
```

### Task 4.2: Manual Review Queue ⏳
**Migration**: `AddManualReviewQueue`

```sql
CREATE TABLE ManualReviewQueue (
    Id INT IDENTITY PRIMARY KEY,
    FileMetadataId UNIQUEIDENTIFIER NOT NULL,
    FieldName NVARCHAR(100) NOT NULL,
    ExtractedValue NVARCHAR(MAX) NULL,
    ConfidenceScore DECIMAL(5,2) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending/Approved/Rejected
    ReviewedBy NVARCHAR(256) NULL,
    ReviewedAt DATETIME2 NULL,
    ReviewNotes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ManualReview_FileMetadata FOREIGN KEY (FileMetadataId)
        REFERENCES FileMetadata(Id)
);

CREATE INDEX IX_ManualReview_Status ON ManualReviewQueue(Status);
```

**Component**: `ManualReviewQueue.razor`
```razor
<MudDataGrid Items="@_pendingReviews" Filterable="true" QuickFilter="@_quickFilter">
    <Columns>
        <PropertyColumn Property="x => x.FieldName" Title="Field" />
        <PropertyColumn Property="x => x.ExtractedValue" Title="Extracted Value" />
        <TemplateColumn Title="Confidence">
            <CellTemplate>
                <ConfidenceIndicator
                    FieldName="@context.Item.FieldName"
                    Value="@context.Item.ExtractedValue"
                    Confidence="@context.Item.ConfidenceScore" />
            </CellTemplate>
        </TemplateColumn>
        <TemplateColumn Title="Actions">
            <CellTemplate>
                <MudButton Size="Size.Small" Color="Color.Success"
                           OnClick="@(() => ApproveAsync(context.Item))">
                    Approve
                </MudButton>
                <MudButton Size="Size.Small" Color="Color.Error"
                           OnClick="@(() => RejectAsync(context.Item))">
                    Reject
                </MudButton>
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

**Workflow Logic**:
```csharp
public async Task ProcessDocumentAsync(Stream documentStream, CancellationToken ct)
{
    // OCR extraction
    var ocrResult = await _ocrService.ExtractAsync(documentStream, ct);

    // Check confidence thresholds
    foreach (var field in ocrResult.Fields)
    {
        if (field.Confidence < 0.70) // Low confidence threshold
        {
            // Add to manual review queue
            await _reviewQueue.AddAsync(new ManualReviewQueueItem
            {
                FileMetadataId = fileMetadataId,
                FieldName = field.Name,
                ExtractedValue = field.Value,
                ConfidenceScore = field.Confidence,
                Status = "Pending"
            }, ct);

            // Send toast notification
            await _hub.Clients.All.SendAsync("ManualReviewRequired",
                $"Low confidence on field: {field.Name}");
        }
    }
}
```

---

## STEP 5: HISTORICAL SEARCH (STAKEHOLDER WOW FACTOR 💎)

### **Goal**: Searchable document archive with PDF/XML side-by-side viewer

### Task 5.1: Database Schema - ProcessedDocuments ⏳
**Migration**: `AddProcessedDocumentsTable`

```sql
CREATE TABLE ProcessedDocuments (
    Id INT IDENTITY PRIMARY KEY,
    ProcessedDate DATETIME2 NOT NULL,
    RequirementTypeId INT NOT NULL,
    NumeroRequerimiento NVARCHAR(50) NULL,
    AutoridadRequiriente NVARCHAR(200) NULL,
    ClienteRFC NVARCHAR(13) NULL,
    ClienteNombre NVARCHAR(300) NULL,
    FilePath NVARCHAR(500) NOT NULL,
    PdfPath NVARCHAR(500) NULL,
    XmlPath NVARCHAR(500) NULL,
    DocxPath NVARCHAR(500) NULL,
    OverallConfidence DECIMAL(5,2) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- Pending/Completed/ManualReview/Error
    FileMetadataId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProcessedDocs_RequirementType FOREIGN KEY (RequirementTypeId)
        REFERENCES RequirementTypeDictionary(Id),
    CONSTRAINT FK_ProcessedDocs_FileMetadata FOREIGN KEY (FileMetadataId)
        REFERENCES FileMetadata(Id)
);

CREATE INDEX IX_ProcessedDocs_ProcessedDate ON ProcessedDocuments(ProcessedDate DESC);
CREATE INDEX IX_ProcessedDocs_NumeroRequerimiento ON ProcessedDocuments(NumeroRequerimiento);
CREATE INDEX IX_ProcessedDocs_ClienteRFC ON ProcessedDocuments(ClienteRFC);
CREATE INDEX IX_ProcessedDocs_RequirementType ON ProcessedDocuments(RequirementTypeId);
CREATE INDEX IX_ProcessedDocs_Status ON ProcessedDocuments(Status);
```

### Task 5.2: Search Service ⏳
```csharp
public class DocumentSearchService : IDocumentSearchService
{
    public async Task<List<ProcessedDocumentDto>> SearchAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? requestNumber,
        string? authority,
        string? clientRfc,
        int? requirementType,
        CancellationToken ct = default)
    {
        var query = _context.ProcessedDocuments.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(d => d.ProcessedDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => d.ProcessedDate <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(requestNumber))
            query = query.Where(d => d.NumeroRequerimiento == requestNumber);

        if (!string.IsNullOrWhiteSpace(authority))
            query = query.Where(d => EF.Functions.Like(d.AutoridadRequiriente, $"%{authority}%"));

        if (!string.IsNullOrWhiteSpace(clientRfc))
            query = query.Where(d => d.ClienteRFC == clientRfc);

        if (requirementType.HasValue)
            query = query.Where(d => d.RequirementTypeId == requirementType.Value);

        return await query
            .OrderByDescending(d => d.ProcessedDate)
            .Select(d => new ProcessedDocumentDto
            {
                Id = d.Id,
                ProcessedDate = d.ProcessedDate,
                RequirementType = d.RequirementTypeId,
                RequestNumber = d.NumeroRequerimiento,
                Authority = d.AutoridadRequiriente,
                ClientRFC = d.ClienteRFC,
                ClientName = d.ClienteNombre,
                Confidence = d.OverallConfidence,
                Status = d.Status,
                PdfPath = d.PdfPath,
                XmlPath = d.XmlPath
            })
            .ToListAsync(ct);
    }
}
```

### Task 5.3: Search UI Component ⏳
**Component**: `DocumentSearchView.razor`

```razor
<MudCard>
    <MudCardContent>
        <MudGrid>
            <MudItem xs="12" sm="6">
                <MudDateRangePicker Label="Date Range"
                                    @bind-DateRange="_dateRange" />
            </MudItem>
            <MudItem xs="12" sm="6">
                <MudTextField Label="Request Number"
                              @bind-Value="_requestNumber" />
            </MudItem>
            <MudItem xs="12" sm="6">
                <MudTextField Label="Authority"
                              @bind-Value="_authority" />
            </MudItem>
            <MudItem xs="12" sm="6">
                <MudTextField Label="Client RFC"
                              @bind-Value="_clientRfc" />
            </MudItem>
            <MudItem xs="12" sm="6">
                <MudSelect Label="Requirement Type"
                           @bind-Value="_requirementType">
                    <MudSelectItem Value="@((int?)null)">All</MudSelectItem>
                    <MudSelectItem Value="100">Judicial</MudSelectItem>
                    <MudSelectItem Value="101">Aseguramiento</MudSelectItem>
                    <MudSelectItem Value="102">Desbloqueo</MudSelectItem>
                    <MudSelectItem Value="103">Transferencia</MudSelectItem>
                    <MudSelectItem Value="104">Situación de Fondos</MudSelectItem>
                </MudSelect>
            </MudItem>
            <MudItem xs="12">
                <MudButton Color="Color.Primary"
                           OnClick="SearchAsync"
                           Variant="Variant.Filled">
                    Search
                </MudButton>
            </MudItem>
        </MudGrid>
    </MudCardContent>
</MudCard>

<MudDataGrid Items="@_results" Filterable="true" Hideable="true">
    <Columns>
        <PropertyColumn Property="x => x.ProcessedDate" Title="Date" Format="yyyy-MM-dd HH:mm" />
        <PropertyColumn Property="x => x.RequirementType" Title="Type">
            <CellTemplate>
                @RequirementType.FromValue(context.Item.RequirementType).DisplayName
            </CellTemplate>
        </PropertyColumn>
        <PropertyColumn Property="x => x.RequestNumber" Title="Request #" />
        <PropertyColumn Property="x => x.Authority" Title="Authority" />
        <PropertyColumn Property="x => x.ClientRFC" Title="RFC" />
        <TemplateColumn Title="Confidence">
            <CellTemplate>
                <MudChip Color="@GetConfidenceColor(context.Item.Confidence)" Size="Size.Small">
                    @context.Item.Confidence.ToString("P0")
                </MudChip>
            </CellTemplate>
        </TemplateColumn>
        <TemplateColumn Title="Actions">
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Visibility"
                               Color="Color.Primary"
                               OnClick="@(() => ViewDocumentAsync(context.Item))" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

### Task 5.4: PDF/XML Side-by-Side Viewer ⏳
**Component**: `DocumentViewer.razor`

```razor
<MudDialog>
    <DialogContent>
        <MudGrid>
            <MudItem xs="6">
                <MudText Typo="Typo.h6">PDF</MudText>
                <iframe src="@_pdfUrl"
                        style="width:100%; height:600px; border:none;">
                </iframe>
            </MudItem>
            <MudItem xs="6">
                <MudText Typo="Typo.h6">XML</MudText>
                <MudTextField Value="@_xmlContent"
                              Lines="30"
                              Variant="Variant.Outlined"
                              ReadOnly="true" />
            </MudItem>
        </MudGrid>
    </DialogContent>
    <DialogActions>
        <MudButton Color="Color.Primary" OnClick="DownloadBothAsync">
            Download Both
        </MudButton>
        <MudButton OnClick="Close">Close</MudButton>
    </DialogActions>
</MudDialog>
```

### Task 5.5: Export to Excel ⏳
**Using EPPlus library**:

```csharp
public async Task<byte[]> ExportToExcelAsync(List<ProcessedDocumentDto> results)
{
    using var package = new ExcelPackage();
    var worksheet = package.Workbook.Worksheets.Add("Search Results");

    // Headers
    worksheet.Cells[1, 1].Value = "Date";
    worksheet.Cells[1, 2].Value = "Type";
    worksheet.Cells[1, 3].Value = "Request #";
    worksheet.Cells[1, 4].Value = "Authority";
    worksheet.Cells[1, 5].Value = "Client RFC";
    worksheet.Cells[1, 6].Value = "Confidence";
    worksheet.Cells[1, 7].Value = "Status";

    // Data
    for (int i = 0; i < results.Count; i++)
    {
        var row = i + 2;
        var item = results[i];
        worksheet.Cells[row, 1].Value = item.ProcessedDate.ToString("yyyy-MM-dd HH:mm");
        worksheet.Cells[row, 2].Value = RequirementType.FromValue(item.RequirementType).DisplayName;
        worksheet.Cells[row, 3].Value = item.RequestNumber;
        worksheet.Cells[row, 4].Value = item.Authority;
        worksheet.Cells[row, 5].Value = item.ClientRFC;
        worksheet.Cells[row, 6].Value = item.Confidence;
        worksheet.Cells[row, 7].Value = item.Status;
    }

    // Auto-fit columns
    worksheet.Cells.AutoFitColumns();

    return await package.GetAsByteArrayAsync();
}
```

---

## DEMO SCRIPT (15-Minute Presentation)

### **Opening** (30 seconds)
"Good [morning/afternoon]. I'm going to demonstrate a working proof-of-concept that automates CNBV regulatory compliance for Mexican banks. This system reduces the 20-day legal response window to minutes, with built-in quality assurance and full audit trails."

### **Step 1 Demo** (2 minutes) - Multi-Source Downloads
1. **SIARA**: "First, our fake SIARA simulator. In production, this connects to the real CNBV platform."
   - Click "Open SIARA" button
   - Show configurable arrival rate (Poisson distribution)
   - Download XML, PDF, DOCX
   - Toast notification: "3 files downloaded"

2. **Internet Archive**: "Now, real browser automation on a public site."
   - Click "Internet Archive" button
   - Browser opens visibly (headless: false)
   - Searches, downloads document
   - "This proves our automation works on real websites"

3. **Gutenberg**: "Second public site for credibility."
   - Click "Gutenberg Library" button
   - Downloads book
   - "Same automation pattern, different source"

### **Step 2 Demo** (1 minute) - Auto-Organization with Failover
- Show folder structure: `F:\PrismaDocuments\2025\01\25\`
- "Files organized by date automatically, before we even know what they are"
- Simulate storage failure (disconnect network drive)
- "Primary fails → Secondary saves → Toast alert → Processing continues"
- "Notice: non-blocking. We never stop processing due to storage issues."

### **Step 3 Demo** (3 minutes) - Classification + Error Handling
1. **Happy Path**: Load valid CNBV fixture
   - OCR extracts text (Tesseract, 3 seconds)
   - Classifier identifies: "Judicial - Solicitud de Información"
   - Confidence: 95% (green badge)
   - File moved to: `2025/01/25/Judicial/`

2. **Imperfect Fixture**: Load fixture with missing fields
   - "Real-world data is messy. Watch how we handle it."
   - OCR succeeds (partial data)
   - UI shows warning: "⚠️ Missing field: NumeroRequerimiento"
   - Confidence: 72% (yellow badge)
   - "Processing continues. Manual review flagged."

3. **Unknown Type**: New requirement type not in our enum
   - Classifier returns: "Unknown (999)"
   - Database dictionary saves new type
   - File moved to: `2025/01/25/Unknown/`
   - "System evolves without code changes"

### **Step 4 Demo** (2 minutes) - Real-Time Reporting
- Open Processing Dashboard
- Start processing batch of 5 documents
- Show live updates:
  - Progress bar animating
  - OCR confidence per field (green/yellow/red chips)
  - Elapsed time counter
- Document with low confidence triggers:
  - Entry in Manual Review Queue
  - Toast notification: "Manual review required"
- User clicks "Approve" on review queue
- Status changes to "Completed"

### **Step 5 Demo** (2 minutes) - Search (WOW FACTOR)
- "Now the payoff. Why do we need a database?"
- Open Search page
- **Search by date range**: Jan 1-25, 2025
  - 47 results displayed in grid
- **Filter by requirement type**: Judicial only
  - 12 results
- **Search by RFC**: HEJA850101ABC
  - 3 results for this client
- Click "View" on first result
  - PDF + XML side-by-side viewer opens
  - "Auditors love this. Everything in one place."
- Click "Export to Excel"
  - File downloads
  - Open Excel → verify data
  - "Send this to legal team, compliance, auditors"

### **Closing** (1 minute) - ROI Narrative
"Let's talk business:
- **Current state**: Lawyers manually process each request. Average 6-day response time. High error rate.
- **This system**: Minutes instead of days. Confidence scores ensure quality. Full audit trail for CNBV inspections.
- **ROI**: Developer cost vs lawyer time savings. Break-even after processing ~50 requests/month.
- **Next steps**: P1 - Production pilot with real CNBV integration. P2 - Multi-bank tenancy and advanced analytics.

Questions?"

---

## SUCCESS CRITERIA

### **Technical** ✅
- [ ] All unit tests green (600+ existing + new tests)
- [ ] All integration tests green
- [ ] All 5 steps demonstrable end-to-end
- [ ] No crashes, no DI errors
- [ ] Database migrations applied successfully
- [ ] Web app starts cleanly

### **Business** ✅
- [ ] Complete 15-minute demo without errors
- [ ] Handles imperfect data gracefully (demonstrates resilience)
- [ ] Search feature impresses stakeholders (WOW factor)
- [ ] Clear ROI narrative articulated
- [ ] Stakeholder approval to proceed with P1

### **Quality** ✅
- [ ] Hexagonal architecture preserved (17/17 tests passing)
- [ ] No shortcuts taken (production-quality code)
- [ ] Legal requirements documented and implemented
- [ ] Audit trail complete (CIS Control 6 compliant)
- [ ] Performance acceptable (<5 sec per document)

---

## RISK MITIGATION

### **Known Risks**
1. **Playwright Instability**: Browser automation can be flaky
   - Mitigation: Retry logic, visible mode for demo (user sees what happens)

2. **OCR Accuracy**: GOT-OCR2 can be slow (140 seconds)
   - Mitigation: Use Tesseract primarily (3-6s), GOT-OCR2 only on fallback

3. **Network Failures**: Storage failover, external sites down
   - Mitigation: Non-blocking error handling, local fixtures as backup

4. **Demo Timing**: 15 minutes is tight
   - Mitigation: Rehearse 3x, prepare backup recordings, skip Step 1.4/1.5 if time short

5. **Database Connection**: LocalDB may not start
   - Mitigation: Test 30 minutes before demo, fallback to in-memory provider

### **Contingency Plans**
- **If SIARA simulator fails**: Use pre-downloaded fixtures, explain "this is what would download"
- **If Playwright fails**: Show pre-recorded video of automation, explain "technical demo earlier today"
- **If database fails**: Switch to in-memory provider, lose search but show other steps
- **If entire system fails**: Slide deck with architecture diagrams + video recording

---

## PROGRESS TRACKER

### **Phase 0: Foundation** (0%)
- [ ] Task 0.1: Apply ApplicationDbContext migrations
- [ ] Task 0.2: Apply PrismaDbContext migrations
- [ ] Task 0.3: Test web app startup
- [ ] Task 0.4: Verify DI registration

### **Step 1: Download** (10%)
- [ ] Task 1.1: Download service unit test
- [ ] Task 1.2: Download service implementation
- [ ] Task 1.3: Wire download to UI
- [ ] Task 1.4: Playwright - Internet Archive
- [ ] Task 1.5: Playwright - Gutenberg

### **Step 2a: Pre-Parse Storage** (0%)
- [ ] Task 2a.1: Update configuration schema
- [ ] Task 2a.2: Folder hierarchy service
- [ ] Task 2a.3: Failover storage service

### **Step 3: OCR + Classification** (20%)
- [ ] Task 3.1: Port ModelEnum from IndTraceV2025
- [ ] Task 3.2: Create RequirementType ModelEnum
- [ ] Task 3.3: Database dictionary table
- [ ] Task 3.4: Classification engine with legal rules
- [ ] Task 3.5: Error handling demo - imperfect fixtures
- [ ] Task 3.6: OCR confidence display

### **Step 2b: Post-Parse Storage** (0%)
- [ ] Task 2b.1: File reorganization service

### **Step 4: Real-Time Reporting** (10%)
- [ ] Task 4.1: Processing status dashboard (SignalR)
- [ ] Task 4.2: Manual review queue

### **Step 5: Historical Search** (0%)
- [ ] Task 5.1: Database schema - ProcessedDocuments
- [ ] Task 5.2: Search service
- [ ] Task 5.3: Search UI component
- [ ] Task 5.4: PDF/XML side-by-side viewer
- [ ] Task 5.5: Export to Excel

### **Demo Preparation** (0%)
- [ ] Write demo script (StakeholderDemo.md)
- [ ] Rehearse demo 3x
- [ ] Prepare backup recordings
- [ ] Prepare talking points

---

## EXECUTION SCHEDULE (Post-Database Fix)

**Day 1** (4-6 hours):
- Phase 0: Foundation (all tasks)
- Step 1.1-1.3: Download basics

**Day 2** (6-8 hours):
- Step 1.4-1.5: Playwright automation
- Step 2a: Pre-parse storage (all tasks)
- Step 3.1-3.2: ModelEnum porting + RequirementType

**Day 3** (6-8 hours):
- Step 3.3-3.6: Classification engine + error handling
- Step 2b: Post-parse storage
- Step 4.1: Processing dashboard

**Day 4** (6-8 hours):
- Step 4.2: Manual review queue
- Step 5.1-5.3: Search database + service + UI

**Day 5** (4-6 hours):
- Step 5.4-5.5: PDF/XML viewer + Excel export
- Demo script writing
- Rehearsal #1

**Day 6** (Optional - Buffer):
- Integration testing
- Bug fixes
- Rehearsal #2 and #3

**Total Estimate**: 26-36 hours of focused work = **3-5 business days**

---

## RESOURCES

### **Documentation Created**
- ✅ Legal Research: `Prisma/Docs/Legal/` (4 files, 74 KB)
  - SmartEnum_RequirementTypes.md (5 types + Unknown)
  - MandatoryFields_CNBV.md (42 fields from Anexo 3)
  - ClassificationRules.md (7-level decision tree)
  - LegalRequirements_Summary.md (comprehensive compliance guide)
- ✅ Progress Tracker: `Prisma/ClosingInitiativeMvp.md`
- ✅ Presentation Checklist: `Prisma/PRESENTATION_CHECKLIST.md`
- ✅ This Plan: `Prisma/PresentationPlanMVP.md`

### **External Resources**
- ModelEnum Reference: `F:\Dynamic\IndTraceV2025\Src\Code\Core\Domain\Enum\`
- Legal PDFs: `Prisma/Fixtures/PRP1/` (4 files)
- Test Fixtures: `Prisma/Fixtures/PRP1/` (4 real + 200+ synthetic)

### **Key Files**
- Program.cs: `UI/ExxerCube.Prisma.Web.UI/Program.cs` (DI configuration)
- appsettings.json: `UI/ExxerCube.Prisma.Web.UI/appsettings.json` (configuration)
- PrismaDbContext: `Infrastructure.Database/EntityFramework/PrismaDbContext.cs`
- ApplicationDbContext: `UI/ExxerCube.Prisma.Web.UI/Data/ApplicationDbContext.cs`

---

## CONTACT & ESCALATION

**Blocker Escalation Path**:
1. Database trigger issue → Disable SQL Server trigger or use LocalDB
2. Playwright installation → `npx playwright install chromium`
3. Python environment (GOT-OCR2) → Already configured (line 127-128 in Program.cs)
4. DI errors → Check Program.cs lines 105-229 for missing registrations

**Demo Day Preparation**:
- Test database connection 30 minutes before
- Test browser automation 15 minutes before
- Have backup recordings ready
- Print demo script as fallback

---

## WISDOM FROM TODAY'S ANALYSIS

### **What We Learned**
1. **Migrations are Pending**: Both contexts have unapplied migrations (critical blocker)
2. **DI is Actually Good**: Program.cs already has comprehensive registration (lines 105-229)
3. **Legal Research Complete**: All 5 requirement types documented with keywords
4. **ModelEnum is Production-Ready**: IndTraceV2025 implementation is battle-tested
5. **Search is the WOW Factor**: User confirmed "stakeholders fall in love with it"
6. **Pipeline is Two-Phase**: Storage happens BEFORE and AFTER classification

### **What Changed from Original Plan**
- **Original MVP**: 85% complete → **Reality**: 50% complete
- **Timeline**: 1-2 days → **Revised**: 3-5 days
- **Missing Features**: Steps 2, 4, 5 not previously scoped
- **New Requirement**: Failover storage (3-tier)
- **New Requirement**: Dynamic requirement types (SmartEnum + DB dictionary)

### **What Stayed the Same**
- ✅ Architecture quality (hexagonal, 600+ tests)
- ✅ OCR system working (Tesseract + GOT-OCR2)
- ✅ Navigation UI exists
- ✅ Foundation is solid

---

## FINAL NOTE

**This is an ambitious but achievable plan.** The foundation is strong (70% complete), but feature work requires focus. The 5-step presentation flow is the right approach - it tells a compelling story and demonstrates real business value.

**Priority Order** (if time is short):
1. **Must Have**: Steps 1, 3, 5 (Download, Classify, Search) - these are the WOW factors
2. **Should Have**: Step 4 (Reporting) - shows professionalism
3. **Nice to Have**: Step 2 (Organization) - can explain verbally if not implemented

**Success Metric**: Stakeholder approval to proceed with P1. Everything else is secondary.

Let's execute systematically. Start with Phase 0 (database migrations), then work through steps 1-5 methodically. Test at every stage. Rehearse the demo 3 times before presentation day.

**We can do this.** 🚀

---

**Last Updated**: 2025-01-25 08:35 AM
**Status**: 🔴 Ready to Execute - Start Phase 0
**Next Action**: Apply database migrations (Task 0.1)

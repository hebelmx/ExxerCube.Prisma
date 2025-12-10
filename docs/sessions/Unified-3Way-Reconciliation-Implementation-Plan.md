# Unified 3-Way Reconciliation Implementation Plan
## Using Production Services: FusionExpedienteService, FieldMatchingService, and Field Extractors

---

## 📋 Executive Summary

**Goal:** Implement complete 3-way document reconciliation in DocumentProcessing.razor using production-grade services

**Key Services:**
- `IFusionExpediente` / `FusionExpedienteService` - Multi-source data fusion with confidence scoring
- `IFieldMatchingService` / `FieldMatchingService` - Field extraction and matching orchestration
- `IFieldExtractor<PdfSource>` / `PdfOcrFieldExtractor` - PDF OCR field extraction
- `IFieldExtractor<DocxSource>` / `AdaptiveDocxFieldExtractorAdapter` - DOCX adaptive extraction
- `IFieldExtractor<XmlSource>` - XML field extraction

**Start Point:** DocumentProcessing.razor line 1835 (ParseOcrToExpediente stub method)

**Status:** 🟢 READY TO IMPLEMENT

---

## 🎯 Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                   DocumentProcessing.razor                           │
│                    (Blazor UI Component)                             │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                    ┌─────────────┼─────────────┐
                    ▼             ▼             ▼
        ┌─────────────┐  ┌──────────────┐  ┌─────────────┐
        │ XML Button  │  │  PDF Button  │  │ DOCX Button │
        │   (4 each)  │  │   (4 each)   │  │  (4 each)   │
        └─────────────┘  └──────────────┘  └─────────────┘
                │              │                  │
                ▼              ▼                  ▼
        ┌─────────────────────────────────────────────────┐
        │         Field Extraction Layer                   │
        │  ┌────────────┐  ┌────────────┐  ┌────────────┐ │
        │  │ XML Field  │  │ PDF OCR    │  │ DOCX Field │ │
        │  │ Extractor  │  │ Extractor  │  │ Extractor  │ │
        │  └────────────┘  └────────────┘  └────────────┘ │
        └─────────────────────────────────────────────────┘
                                  │
                                  ▼
        ┌─────────────────────────────────────────────────┐
        │        FieldMatchingService (Optional)           │
        │  Orchestrates multi-source extraction & matching │
        │  Returns: UnifiedMetadataRecord                  │
        └─────────────────────────────────────────────────┘
                                  │
                                  ▼
        ┌─────────────────────────────────────────────────┐
        │          FusionExpedienteService                 │
        │   3-Way Reconciliation with Confidence Scoring   │
        │                                                  │
        │  Input: xmlExp, pdfExp, docxExp + Metadata      │
        │  Output: FusionResult with confidence (0-1.0)   │
        │                                                  │
        │  Algorithm:                                      │
        │  1. Calculate source reliabilities               │
        │  2. Exact match → Fuzzy match → Weighted vote   │
        │  3. Confidence scoring per field                 │
        │  4. Conflict detection & flagging                │
        │  5. Decision: AutoProcess/Review/Manual          │
        └─────────────────────────────────────────────────┘
                                  │
                                  ▼
        ┌─────────────────────────────────────────────────┐
        │              Reconciliation UI                   │
        │  • 3-Way Comparison Table                       │
        │  • Confidence Scores & Visual Indicators        │
        │  • Conflict Highlighting                        │
        │  • Field-by-field rationale                     │
        └─────────────────────────────────────────────────┘
```

---

## 📂 Service Inventory & APIs

### 1. **IFusionExpediente / FusionExpedienteService**
**Location:** `Infrastructure.Classification/FusionExpedienteService.cs`

**Primary API:**
```csharp
Task<Result<FusionResult>> FuseAsync(
    Expediente? xmlExpediente,
    Expediente? pdfExpediente,
    Expediente? docxExpediente,
    ExtractionMetadata xmlMetadata,
    ExtractionMetadata pdfMetadata,
    ExtractionMetadata docxMetadata,
    CancellationToken cancellationToken);
```

**Returns:**
```csharp
public class FusionResult
{
    public Expediente FusedExpediente { get; set; }
    public float OverallConfidence { get; set; }  // 0.0-1.0
    public FusionAction NextAction { get; set; }   // AutoProcess | ReviewRecommended | ManualReviewRequired
    public Dictionary<string, FieldFusionResult> FieldResults { get; set; }
    public List<string> ConflictingFields { get; set; }
    public List<string> MissingRequiredFields { get; set; }
    public Dictionary<SourceType, float> SourceReliabilities { get; set; }
}
```

**Key Features:**
- Dynamic source reliability weighting based on OCR confidence, image quality
- Multi-phase field fusion: Exact match → Fuzzy match (85%+) → Weighted voting
- Per-field confidence scoring with detailed rationale
- Conflict detection and manual review flagging
- Optimized using Genetic Algorithm + Polynomial Regression (like OCR filters)

---

### 2. **IFieldMatchingService / FieldMatchingService**
**Location:** `Application/Services/FieldMatchingService.cs`

**Primary API:**
```csharp
Task<Result<UnifiedMetadataRecord>> MatchFieldsAndGenerateUnifiedRecordAsync(
    DocxSource? docxSource,
    PdfSource? pdfSource,
    XmlSource? xmlSource,
    FieldDefinition[] fieldDefinitions,
    Expediente? expediente = null,
    ClassificationResult? classification = null,
    List<string>? requiredFields = null,
    CancellationToken cancellationToken = default);
```

**Returns:**
```csharp
public class UnifiedMetadataRecord
{
    public Expediente? Expediente { get; set; }
    public ExtractedFields ExtractedFields { get; set; }
    public ClassificationResult? Classification { get; set; }
    public MatchedFields MatchedFields { get; set; }
    public Dictionary<string, string> AdditionalFields { get; set; }
    public List<string> AdditionalFieldConflicts { get; set; }
    public ValidationState Validation { get; set; }
}
```

**Key Features:**
- Orchestrates field extraction from DOCX, PDF, XML sources
- Delegates to specialized `IFieldExtractor<T>` implementations
- Uses `IMatchingPolicy` to select best value from multiple candidates
- Validates required fields and calculates overall agreement
- Returns unified metadata with conflict tracking

---

### 3. **IFieldExtractor<PdfSource> / PdfOcrFieldExtractor**
**Location:** `Infrastructure.Extraction.Ocr.Teseract/PdfOcrFieldExtractor.cs`

**Primary API:**
```csharp
Task<Result<ExtractedFields>> ExtractFieldsAsync(
    PdfSource source,
    FieldDefinition[] fieldDefinitions);

Task<Result<FieldValue>> ExtractFieldAsync(
    PdfSource source,
    string fieldName);
```

**Key Features:**
- Uses existing `IOcrExecutor` and `IImagePreprocessor` pipeline
- Extracts structured fields: Expediente, Causa, AccionSolicitada, Dates, Amounts
- Pattern-based extraction using regex (e.g., `A/AS1-2505-088637-PHM`)
- Returns `ExtractedFields` with confidence scores

---

### 4. **IFieldExtractor<DocxSource> / AdaptiveDocxFieldExtractorAdapter**
**Location:** `Infrastructure.Extraction.Adaptive/AdaptiveDocxFieldExtractorAdapter.cs`

**Key Features:**
- Multi-strategy extraction (Best, Merge All, Complement)
- Adaptive pattern matching for variable DOCX formats
- Higher reliability than PDF OCR (used as primary source in conflicts)

---

### 5. **IFieldExtractor<XmlSource>**
**Key Features:**
- Parses manually-filled XML from SIARA system
- Prone to typos and catalog errors (lower reliability weight)
- Fast extraction (no OCR overhead)

---

## 🚀 Implementation Plan

### **Phase 1: Replace ParseOcrToExpediente Stub (Line 1835)**
**Priority:** 🔴 CRITICAL - Foundation for all reconciliation
**Target:** DocumentProcessing.razor lines 1835-1859

#### Current Code (Stub):
```csharp
//these need to be changed for the real exraction service
private Expediente ParseOcrToExpediente(ProcessingResult ocrResult)
{
    var expediente = new Expediente
    {
        NumeroExpediente = ocrResult.ExtractedFields.Expediente ?? "",
        NumeroOficio = "", // TODO: Extract from OCR text
        // ... more TODOs
    };
    return expediente;
}
```

#### **New Implementation Using PdfOcrFieldExtractor:**

**Step 1:** Inject `IFieldExtractor<PdfSource>` in DocumentProcessing.razor
```csharp
@inject IFieldExtractor<PdfSource> PdfFieldExtractor
@inject IFieldExtractor<XmlSource>? XmlFieldExtractor
@inject IFieldExtractor<DocxSource>? DocxFieldExtractor
@inject IFusionExpediente FusionService
@inject IFieldMatchingService? FieldMatchingService
```

**Step 2:** Replace stub with real extraction
```csharp
private async Task<Result<Expediente>> ExtractExpedienteFromOcrAsync(
    ProcessingResult ocrResult,
    byte[] pdfBytes,
    CancellationToken cancellationToken = default)
{
    try
    {
        _logger.LogDebug("Extracting Expediente from OCR result using PdfOcrFieldExtractor");

        // Create PdfSource from OCR result
        var pdfSource = new PdfSource
        {
            FileContent = pdfBytes,
            FilePath = currentPdfFixtureName,
            OcrConfidence = ocrResult.Confidence
        };

        // Define fields to extract
        var fieldDefinitions = new[]
        {
            new FieldDefinition { FieldName = "Expediente", IsRequired = true },
            new FieldDefinition { FieldName = "Causa", IsRequired = false },
            new FieldDefinition { FieldName = "AccionSolicitada", IsRequired = false },
            new FieldDefinition { FieldName = "NumeroOficio", IsRequired = true },
            new FieldDefinition { FieldName = "AutoridadNombre", IsRequired = true },
            new FieldDefinition { FieldName = "FechaPublicacion", IsRequired = false },
            new FieldDefinition { FieldName = "DiasPlazo", IsRequired = false }
        };

        // Extract fields using PdfOcrFieldExtractor
        var extractionResult = await PdfFieldExtractor.ExtractFieldsAsync(
            pdfSource,
            fieldDefinitions);

        if (extractionResult.IsFailure)
        {
            return Result<Expediente>.WithFailure(
                $"Failed to extract fields from PDF: {extractionResult.Error}");
        }

        var extractedFields = extractionResult.Value;
        if (extractedFields == null)
        {
            return Result<Expediente>.WithFailure("Extracted fields are null");
        }

        // Map ExtractedFields to Expediente entity
        var expediente = MapExtractedFieldsToExpediente(extractedFields);

        _logger.LogDebug(
            "Successfully extracted Expediente from PDF: {NumeroExpediente}",
            expediente.NumeroExpediente);

        return Result<Expediente>.Success(expediente);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error extracting Expediente from OCR result");
        return Result<Expediente>.WithFailure(
            $"Error extracting Expediente: {ex.Message}",
            default(Expediente),
            ex);
    }
}

private Expediente MapExtractedFieldsToExpediente(ExtractedFields fields)
{
    var expediente = new Expediente
    {
        NumeroExpediente = fields.Expediente ?? "",
        NumeroOficio = fields.AdditionalFields?.GetValueOrDefault("NumeroOficio") ?? "",
        SolicitudSiara = fields.AdditionalFields?.GetValueOrDefault("SolicitudSiara") ?? "",
        Folio = 0, // TODO: Extract from additional fields
        OficioYear = DateTime.Now.Year, // TODO: Parse from NumeroOficio
        AreaClave = 0,
        AreaDescripcion = fields.AdditionalFields?.GetValueOrDefault("AreaDescripcion") ?? "",
        AutoridadNombre = fields.AdditionalFields?.GetValueOrDefault("AutoridadNombre") ?? "",
        NombreSolicitante = null,
        Referencia = "",
        Referencia1 = fields.Causa ?? "",
        Referencia2 = fields.AccionSolicitada ?? "",
        TieneAseguramiento = false
    };

    // Parse dates if available
    if (fields.AdditionalFields?.TryGetValue("FechaPublicacion", out var fechaPubStr) == true &&
        DateTime.TryParse(fechaPubStr, out var fechaPub))
    {
        expediente.FechaPublicacion = fechaPub;
    }

    if (fields.AdditionalFields?.TryGetValue("DiasPlazo", out var diasStr) == true &&
        int.TryParse(diasStr, out var dias))
    {
        expediente.DiasPlazo = dias;
    }

    return expediente;
}
```

**Step 3:** Update LoadPdfFixture to use new extraction method

**Find this section in LoadPdfFixture (around line 1493-1520):**
```csharp
if (result.IsSuccess && result.Value != null)
{
    ocrProcessingResult = result.Value;
    currentPdfFixtureName = pdfFileName;
    processingStatus = "Completed";
    // ... existing code
}
```

**Replace with:**
```csharp
if (result.IsSuccess && result.Value != null)
{
    ocrProcessingResult = result.Value;

    // NEW: Extract Expediente from OCR result using real service
    var expedienteResult = await ExtractExpedienteFromOcrAsync(
        ocrProcessingResult,
        pdfBytes,
        cancellationToken);

    if (expedienteResult.IsSuccess && expedienteResult.Value != null)
    {
        pdfExpediente = expedienteResult.Value;
        _logger.LogInformation(
            "Successfully extracted Expediente from PDF: {NumeroExpediente}",
            pdfExpediente.NumeroExpediente);
    }
    else
    {
        _logger.LogWarning(
            "Failed to extract Expediente from PDF: {Error}",
            expedienteResult.Error);
        errorMessage = $"Expediente extraction warning: {expedienteResult.Error}";
    }

    currentPdfFixtureName = pdfFileName;
    processingStatus = "Completed";
    processingMessage = "PDF OCR processing and field extraction completed successfully";
    processingProgress = 100;

    Snackbar.Add($"PDF processed: {pdfFileName}", Severity.Success);
    StateHasChanged();
}
```

---

### **Phase 2: Add State Variables for Multi-Source Expediente**
**Target:** Add to DocumentProcessing.razor code section (around line 50-100)

```csharp
// Existing variables
private Expediente? xmlExpediente;
private ProcessingResult? ocrProcessingResult;

// NEW: Add separate Expediente instances for each source
private Expediente? pdfExpediente;   // Extracted from PDF via OCR
private Expediente? docxExpediente;  // Extracted from DOCX (future)

// NEW: Add metadata for fusion service
private ExtractionMetadata xmlMetadata = new();
private ExtractionMetadata pdfMetadata = new();
private ExtractionMetadata docxMetadata = new();

// NEW: Add fusion result
private FusionResult? fusionResult;
```

---

### **Phase 3: Implement 3-Way Reconciliation Method**
**Target:** Add after CompareResults() method (around line 1833)

```csharp
private async Task Reconcile3WayAsync()
{
    if (FusionService == null)
    {
        errorMessage = "FusionExpedienteService not available";
        Snackbar.Add("Fusion service not configured", Severity.Error);
        return;
    }

    isProcessing = true;
    processingStatus = "Reconciling";
    processingMessage = "Performing 3-way reconciliation...";
    processingProgress = 0;
    StateHasChanged();

    try
    {
        _logger.LogInformation("Starting 3-way reconciliation");

        // Prepare metadata for each source
        PrepareMetadata();

        // Call fusion service
        var fusionResultTask = FusionService.FuseAsync(
            xmlExpediente,
            pdfExpediente,
            docxExpediente,
            xmlMetadata,
            pdfMetadata,
            docxMetadata,
            CancellationToken.None);

        processingProgress = 50;
        StateHasChanged();

        var result = await fusionResultTask;

        if (result.IsSuccess && result.Value != null)
        {
            fusionResult = result.Value;

            processingStatus = "Completed";
            processingMessage = $"Reconciliation complete - Confidence: {fusionResult.OverallConfidence:P0}";
            processingProgress = 100;

            var actionMessage = fusionResult.NextAction switch
            {
                FusionAction.AutoProcess => "Ready for auto-processing ✓",
                FusionAction.ReviewRecommended => "Review recommended ⚠",
                FusionAction.ManualReviewRequired => "Manual review required ⚠",
                _ => ""
            };

            Snackbar.Add(
                $"3-way reconciliation completed: {fusionResult.OverallConfidence:P0} confidence. {actionMessage}",
                fusionResult.NextAction == FusionAction.AutoProcess ? Severity.Success : Severity.Warning);

            _logger.LogInformation(
                "3-way reconciliation completed - Confidence: {Confidence:F2}, Action: {Action}, Conflicts: {Conflicts}",
                fusionResult.OverallConfidence,
                fusionResult.NextAction,
                fusionResult.ConflictingFields.Count);
        }
        else
        {
            errorMessage = $"Reconciliation failed: {result.Error}";
            processingStatus = "Failed";
            Snackbar.Add($"Reconciliation failed: {result.Error}", Severity.Error);
            _logger.LogError("3-way reconciliation failed: {Error}", result.Error);
        }
    }
    catch (Exception ex)
    {
        errorMessage = $"Error during reconciliation: {ex.Message}";
        processingStatus = "Reconciliation failed";
        Snackbar.Add($"Reconciliation failed: {ex.Message}", Severity.Error);
        _logger.LogError(ex, "Reconciliation failed");
    }
    finally
    {
        isProcessing = false;
        StateHasChanged();
    }
}

private void PrepareMetadata()
{
    // XML metadata (manual input - lower reliability)
    xmlMetadata = new ExtractionMetadata
    {
        SourceType = SourceType.XML_HandFilled,
        Confidence = xmlExpediente != null ? 0.70f : 0.0f,
        ImageQualityScore = 1.0f, // N/A for XML
        PatternViolations = new List<string>(),
        CatalogValidations = new Dictionary<string, bool>(),
        ExtractionSuccess = xmlExpediente != null
    };

    // PDF metadata (OCR-based - medium reliability, variable by quality)
    pdfMetadata = new ExtractionMetadata
    {
        SourceType = SourceType.PDF_OCR_CNBV,
        Confidence = ocrProcessingResult?.Confidence ?? 0.0f,
        ImageQualityScore = ocrProcessingResult?.ImageQualityScore ?? 0.0f,
        PatternViolations = new List<string>(),
        CatalogValidations = new Dictionary<string, bool>(),
        ExtractionSuccess = pdfExpediente != null
    };

    // DOCX metadata (future - highest reliability when available)
    docxMetadata = new ExtractionMetadata
    {
        SourceType = SourceType.DOCX_OCR_Authority,
        Confidence = docxExpediente != null ? 0.85f : 0.0f,
        ImageQualityScore = 1.0f,
        PatternViolations = new List<string>(),
        CatalogValidations = new Dictionary<string, bool>(),
        ExtractionSuccess = docxExpediente != null
    };
}
```

---

### **Phase 4: Add Reconciliation UI Section**
**Target:** Add to DocumentProcessing.razor markup (after PDF results section)

```razor
@* 3-Way Reconciliation Section *@
@if (xmlExpediente != null || pdfExpediente != null || docxExpediente != null)
{
    <MudPaper Class="pa-4 mt-4">
        <MudText Typo="Typo.h5" Class="mb-4">
            3-Way Reconciliation
        </MudText>

        @* Source Status Indicators *@
        <MudGrid Class="mb-4">
            <MudItem xs="4">
                <MudChip Color="@(xmlExpediente != null ? Color.Success : Color.Default)"
                         Icon="@(xmlExpediente != null ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Cancel)">
                    XML: @(xmlExpediente != null ? "Loaded" : "Not loaded")
                </MudChip>
            </MudItem>
            <MudItem xs="4">
                <MudChip Color="@(pdfExpediente != null ? Color.Success : Color.Default)"
                         Icon="@(pdfExpediente != null ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Cancel)">
                    PDF/OCR: @(pdfExpediente != null ? "Loaded" : "Not loaded")
                </MudChip>
            </MudItem>
            <MudItem xs="4">
                <MudChip Color="@(docxExpediente != null ? Color.Success : Color.Default)"
                         Icon="@(docxExpediente != null ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Cancel)">
                    DOCX: @(docxExpediente != null ? "Loaded" : "Not loaded")
                </MudChip>
            </MudItem>
        </MudGrid>

        @* Reconcile Button *@
        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   OnClick="Reconcile3WayAsync"
                   Disabled="@(!CanReconcile() || isProcessing)"
                   StartIcon="@Icons.Material.Filled.MergeType">
            Reconcile Sources (@SourceCount sources available)
        </MudButton>

        @* Reconciliation Results *@
        @if (fusionResult != null)
        {
            <MudDivider Class="my-4" />

            @* Overall Confidence & Action *@
            <MudAlert Severity="@GetFusionSeverity(fusionResult.NextAction)" Class="mb-4">
                <strong>Overall Confidence:</strong> @fusionResult.OverallConfidence.ToString("P0")
                <br />
                <strong>Recommended Action:</strong> @fusionResult.NextAction
            </MudAlert>

            @* Source Reliabilities *@
            <MudText Typo="Typo.h6" Class="mb-2">Source Reliabilities</MudText>
            <MudSimpleTable Density="Density.Compact" Class="mb-4">
                <thead>
                    <tr>
                        <th>Source</th>
                        <th>Reliability Score</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var (source, reliability) in fusionResult.SourceReliabilities)
                    {
                        <tr>
                            <td>@source</td>
                            <td>
                                <MudProgressLinear Value="@(reliability * 100)"
                                                   Color="@GetReliabilityColor(reliability)"
                                                   Size="Size.Small" />
                                @reliability.ToString("P0")
                            </td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>

            @* Field-by-Field Reconciliation Table *@
            <MudText Typo="Typo.h6" Class="mb-2">Field-by-Field Reconciliation</MudText>
            <MudTable Items="@fusionResult.FieldResults.Values"
                      Density="Density.Compact"
                      Hover="true"
                      Class="mb-4">
                <HeaderContent>
                    <MudTh>Field</MudTh>
                    <MudTh>XML Value</MudTh>
                    <MudTh>PDF Value</MudTh>
                    <MudTh>DOCX Value</MudTh>
                    <MudTh>Reconciled Value</MudTh>
                    <MudTh>Confidence</MudTh>
                    <MudTh>Decision</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="Field">@context.FieldName</MudTd>
                    <MudTd DataLabel="XML">@GetFieldValue(xmlExpediente, context.FieldName)</MudTd>
                    <MudTd DataLabel="PDF">@GetFieldValue(pdfExpediente, context.FieldName)</MudTd>
                    <MudTd DataLabel="DOCX">@GetFieldValue(docxExpediente, context.FieldName)</MudTd>
                    <MudTd DataLabel="Reconciled">
                        <strong>@context.SelectedValue</strong>
                        @if (fusionResult.ConflictingFields.Contains(context.FieldName))
                        {
                            <MudIcon Icon="@Icons.Material.Filled.Warning"
                                     Color="Color.Warning"
                                     Size="Size.Small" />
                        }
                    </MudTd>
                    <MudTd DataLabel="Confidence">
                        <MudChip Color="@GetConfidenceColor(context.Confidence)" Size="Size.Small">
                            @context.Confidence.ToString("P0")
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="Decision">
                        <MudTooltip Text="@context.Rationale">
                            <MudChip Size="Size.Small">@context.FusionDecision</MudChip>
                        </MudTooltip>
                    </MudTd>
                </RowTemplate>
            </MudTable>

            @* Conflicts & Missing Fields *@
            @if (fusionResult.ConflictingFields.Count > 0)
            {
                <MudAlert Severity="Severity.Warning" Class="mb-4">
                    <strong>Conflicting Fields (@fusionResult.ConflictingFields.Count):</strong>
                    @string.Join(", ", fusionResult.ConflictingFields)
                </MudAlert>
            }

            @if (fusionResult.MissingRequiredFields.Count > 0)
            {
                <MudAlert Severity="Severity.Error" Class="mb-4">
                    <strong>Missing Required Fields (@fusionResult.MissingRequiredFields.Count):</strong>
                    @string.Join(", ", fusionResult.MissingRequiredFields)
                </MudAlert>
            }

            @* Export Reconciled Data *@
            <MudButton Variant="Variant.Outlined"
                       Color="Color.Primary"
                       OnClick="ExportReconciledData"
                       StartIcon="@Icons.Material.Filled.Download">
                Export Reconciled Expediente (JSON)
            </MudButton>
        }
    </MudPaper>
}

@code {
    private bool CanReconcile()
    {
        var sourceCount = 0;
        if (xmlExpediente != null) sourceCount++;
        if (pdfExpediente != null) sourceCount++;
        if (docxExpediente != null) sourceCount++;
        return sourceCount >= 2; // Need at least 2 sources for reconciliation
    }

    private int SourceCount =>
        (xmlExpediente != null ? 1 : 0) +
        (pdfExpediente != null ? 1 : 0) +
        (docxExpediente != null ? 1 : 0);

    private Severity GetFusionSeverity(FusionAction action) => action switch
    {
        FusionAction.AutoProcess => Severity.Success,
        FusionAction.ReviewRecommended => Severity.Warning,
        FusionAction.ManualReviewRequired => Severity.Error,
        _ => Severity.Info
    };

    private Color GetReliabilityColor(float reliability) => reliability switch
    {
        >= 0.85f => Color.Success,
        >= 0.70f => Color.Warning,
        _ => Color.Error
    };

    private Color GetConfidenceColor(float confidence) => confidence switch
    {
        >= 0.95f => Color.Success,
        >= 0.85f => Color.Info,
        >= 0.70f => Color.Warning,
        _ => Color.Error
    };

    private string? GetFieldValue(Expediente? expediente, string fieldName)
    {
        if (expediente == null) return "-";

        return fieldName switch
        {
            "NumeroExpediente" => expediente.NumeroExpediente,
            "NumeroOficio" => expediente.NumeroOficio,
            "AreaDescripcion" => expediente.AreaDescripcion,
            "AutoridadNombre" => expediente.AutoridadNombre,
            "SolicitudSiara" => expediente.SolicitudSiara,
            "FechaPublicacion" => expediente.FechaPublicacion != DateTime.MinValue
                ? expediente.FechaPublicacion.ToString("yyyy-MM-dd")
                : "-",
            "DiasPlazo" => expediente.DiasPlazo > 0 ? expediente.DiasPlazo.ToString() : "-",
            _ => "-"
        };
    }

    private void ExportReconciledData()
    {
        if (fusionResult?.FusedExpediente == null) return;

        var json = System.Text.Json.JsonSerializer.Serialize(
            fusionResult.FusedExpediente,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

        // Trigger download (implementation depends on JS interop)
        // JSRuntime.InvokeVoidAsync("downloadFile", "reconciled-expediente.json", json);

        Snackbar.Add("Export functionality: Use browser dev tools to copy JSON from logs", Severity.Info);
        _logger.LogInformation("Reconciled Expediente JSON:\n{Json}", json);
    }
}
```

---

### **Phase 5: Update Clear Methods**
**Target:** Update existing ClearXmlResults() and ClearPdfResults() methods

```csharp
private void ClearXmlResults()
{
    xmlExpediente = null;
    extractedFieldCount = 0;
    currentFixtureName = "";
    sourceXmlContent = "";
    showSourceXml = false;
    jsonResult = "";
    comparisonResult = null;
    fusionResult = null;  // NEW
    xmlMetadata = new();  // NEW
    errorMessage = "";
    processingStatus = "";
    processingMessage = "";
    processingProgress = 0;
    Snackbar.Add("XML results cleared", Severity.Info);
    StateHasChanged();
}

private void ClearPdfResults()
{
    ocrProcessingResult = null;
    pdfExpediente = null;  // NEW
    currentPdfFixtureName = "";
    comparisonResult = null;
    fusionResult = null;  // NEW
    pdfMetadata = new();  // NEW
    errorMessage = "";
    processingStatus = "";
    processingMessage = "";
    processingProgress = 0;
    Snackbar.Add("PDF results cleared", Severity.Info);
    StateHasChanged();
}

private void ClearAllResults()  // NEW
{
    ClearXmlResults();
    ClearPdfResults();
    docxExpediente = null;
    docxMetadata = new();
    Snackbar.Add("All results cleared", Severity.Info);
}
```

---

## 📊 Testing Checklist

### **Unit Testing:**
- [ ] Test `ExtractExpedienteFromOcrAsync` with valid OCR result
- [ ] Test `ExtractExpedienteFromOcrAsync` with invalid/null data
- [ ] Test `MapExtractedFieldsToExpediente` field mapping
- [ ] Test `PrepareMetadata` metadata creation
- [ ] Test `Reconcile3WayAsync` with 2 sources (XML + PDF)
- [ ] Test `Reconcile3WayAsync` with 3 sources (XML + PDF + DOCX)
- [ ] Test UI helper methods (GetFieldValue, CanReconcile, etc.)

### **Integration Testing:**
- [ ] Load XML fixture → Verify xmlExpediente populated
- [ ] Load PDF fixture → Verify ocrProcessingResult AND pdfExpediente populated
- [ ] Load XML + PDF → Click Reconcile → Verify fusionResult with confidence
- [ ] Verify field-by-field table displays correctly
- [ ] Verify conflict highlighting works
- [ ] Verify confidence color coding (green/yellow/red)
- [ ] Test all 4 XML fixtures (222AAA, 333BBB, 333ccc, 555CCC)
- [ ] Test all 4 PDF fixtures
- [ ] Test clear buttons reset all state correctly

### **Performance Testing:**
- [ ] Measure extraction time: < 2 seconds for PDF field extraction
- [ ] Measure fusion time: < 1 second for 3-way reconciliation
- [ ] Verify UI remains responsive during processing
- [ ] Test with concurrent loads (XML + PDF simultaneously)

---

## 🎯 Success Metrics

### **Must Have (MVP):**
- ✅ PDF OCR result → Expediente extraction working
- ✅ 2-way reconciliation (XML + PDF) displaying results
- ✅ Confidence scores showing in UI (0-100%)
- ✅ Field-by-field comparison table with all fields
- ✅ Conflict detection and highlighting

### **Nice to Have:**
- ✅ 3-way reconciliation (XML + PDF + DOCX)
- ✅ Export to JSON functionality
- ✅ Source reliability visualization
- ✅ Fusion decision rationale tooltips
- ✅ Real-time progress indicators

---

## 📝 Next Steps After Implementation

1. **Add DOCX Support:**
   - Wire `AdaptiveDocxFieldExtractorAdapter`
   - Add DOCX fixture buttons
   - Implement DOCX viewer component
   - Enable 3-way reconciliation with all sources

2. **Enhance Field Extraction:** ⚠️ **CRITICAL - LOW EXTRACTION RATE IDENTIFIED**
   - **Current State**: Only 2/26 fields extracted from PDF OCR (5% coverage)
   - **Root Cause**: AdaptiveTxtFieldExtractor has only 5 basic patterns (Expediente, Causa, AccionSolicitada, NumeroOficio, AutoridadNombre)
   - **Target State**: Extract 12-16/26 fields (46-61% coverage)
   - **See**: `docs/sessions/AdaptiveTxtFieldExtractor-Field-Catalog.md` for comprehensive field analysis

   **Phase A Enhancement Plan** (Priority 1):
   - ✅ NumeroOficio (working) - Pattern: `[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}`
   - ✅ AutoridadNombre (working) - Catalog-based matching (200+ authorities needed)
   - 🔴 NombreSolicitante (new) - Honorific + name pattern
   - 🔴 Email (new) - Standard email regex: `[a-z0-9._-]+@[a-z0-9.-]+`
   - 🔴 Telefono (new) - Mexican phone: `\(?\d{2}\)?\s*\d{4}-\d{4}`
   - 🔴 Direccion (new) - Multi-line address block extraction
   - 🔴 CodigoPostal (new) - Pattern: `C\.?P\.?\s*(\d{5})`
   - 🔴 FundamentoLegal (new) - Legal article references
   - 🔴 AutoridadEspecificaNombre (new) - Department/office patterns
   - 🔴 FechaPublicacion (new) - Labeled date pattern
   - 🔴 DiasPlazo (new) - "plazo de X días" pattern
   - 🔴 TieneAseguramiento (new) - Keyword detection (aseguramiento/embargo/bloqueo)
   - 🔴 SolicitudSiara (new) - Same as NumeroOficio

   **Verified**: Multi-page PDF processing IS working correctly (all pages converted and OCR'd)
   **Blocker**: Field extraction patterns are too rigid/limited

3. **Advanced Reconciliation:**
   - Implement fuzzy matching for name fields
   - Add catalog validation for AreaDescripcion, AutoridadNombre
   - Optimize fusion coefficients using GA + Polynomial Regression

4. **Production Readiness:**
   - Add comprehensive error handling
   - Implement retry logic for transient failures
   - Add telemetry and metrics collection
   - Create admin dashboard for monitoring

---

## 📚 Related Documentation

- **Field Catalog (NEW):** `docs/sessions/AdaptiveTxtFieldExtractor-Field-Catalog.md` - Comprehensive field extraction requirements
- **AdaptiveTxtFieldExtractor Plan:** `docs/sessions/AdaptiveTxtFieldExtractor-Implementation-Plan.md` - ITDD implementation guide
- **FusionExpedienteService API:** `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Classification/FusionExpedienteService.cs`
- **FieldMatchingService API:** `Prisma/Code/Src/CSharp/01 Core/Application/Services/FieldMatchingService.cs`
- **PdfOcrFieldExtractor:** `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Extraction/Teseract/PdfOcrFieldExtractor.cs`
- **AdaptiveTxtFieldExtractor:** `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Extraction.Txt/AdaptiveTxtFieldExtractor.cs`
- **Session Docs:** `docs/sessions/2025-12-08-DocumentProcessing-Page-Improvements.md`
- **Original Plan:** `docs/sessions/3-Way-Reconciliation-Implementation-Plan.md`

---

**Last Updated:** 2025-12-10
**Status:** 🟡 BLOCKED - Low field extraction rate (2/26 fields, 5% coverage)
**Critical Issue:** AdaptiveTxtFieldExtractor needs 10+ new field patterns (see Field Catalog)
**Next Action:** Phase A - Enhance AdaptiveTxtFieldExtractor with comprehensive patterns (target 46-61% coverage)

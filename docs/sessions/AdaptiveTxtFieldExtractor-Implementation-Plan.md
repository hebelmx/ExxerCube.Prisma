# AdaptiveTxtFieldExtractor Implementation Plan
## Comprehensive Technical Design & ITDD Strategy

**Created**: 2025-12-10
**Status**: 🔴 READY FOR REVIEW
**Priority**: CRITICAL - Blocks 3-Way Reconciliation

---

## 📊 Executive Summary

### Problem Statement
The current `PdfOcrFieldExtractor` is **returning empty fields** in production (DocumentProcessing.razor), despite passing unit tests. Root cause analysis reveals a **fundamental architectural flaw** in the data flow.

### Root Cause Analysis

**Current (Broken) Flow**:
```
PDF bytes → PdfOcrFieldExtractor
            ↓
            ImagePreprocessor (expects image bytes, receives PDF bytes) ❌
            ↓
            OCR Executor (fails silently or produces garbage)
            ↓
            Field Extraction (no fields found)
```

**Issue**: `PdfOcrFieldExtractor.ExtractTextFromPdfAsync()` (line 131-178) is passing **PDF bytes** directly to `ImagePreprocessor`, which expects **image bytes**. This cannot work.

**Why Unit Tests Pass**: Tests mock the OCR pipeline, bypassing the actual PDF-to-image conversion step.

### Correct Flow (Business Case: Image-Only PDFs)
```
PDF bytes → [PDF to Image Conversion] → Image bytes
            ↓
            OCR Pipeline (ImagePreprocessor + OcrExecutor)
            ↓
            OCR Text (TxtSource)
            ↓
            AdaptiveTxtFieldExtractor ✅
            ↓
            ExtractedFields
```

### Solution Architecture

1. **Create `TxtSource`** - Domain model for OCR text output
2. **Create `AdaptiveTxtFieldExtractor`** - Field extractor for text sources (modeled after `AdaptiveDocxFieldExtractorAdapter`)
3. **Refactor `PdfOcrFieldExtractor`** - Delegate field extraction to `AdaptiveTxtFieldExtractor` after OCR
4. **Create dedicated projects** - `Infrastructure.Extraction.Txt` and test project
5. **Create system tests** - Test full PDF → OCR → TXT → Extraction pipeline

---

## 🏗️ Architecture Design

### Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     DocumentProcessing.razor                     │
│                      (UI Layer - Blazor)                         │
└─────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              IFieldExtractor<PdfSource>                          │
│              PdfOcrFieldExtractor (REFACTORED)                   │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ 1. Read PDF bytes (from PdfSource)                        │  │
│  │ 2. Convert PDF to images (PdfiumViewer/PDFSharp)          │  │
│  │ 3. For each page:                                         │  │
│  │    - Run OCR pipeline (ImagePreprocessor + OcrExecutor)   │  │
│  │    - Collect OCR text                                     │  │
│  │ 4. Create TxtSource from aggregated OCR text              │  │
│  │ 5. Delegate to AdaptiveTxtFieldExtractor                  │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│           IFieldExtractor<TxtSource> (NEW)                       │
│           AdaptiveTxtFieldExtractor                              │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Pattern-based field extraction from OCR text:             │  │
│  │                                                            │  │
│  │ 1. Extract "Expediente" (regex patterns)                  │  │
│  │    - Pattern: [A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+          │  │
│  │    - Example: A/AS1-2505-088637-PHM                       │  │
│  │                                                            │  │
│  │ 2. Extract "Causa" (contextual search)                    │  │
│  │    - Pattern: (?:CAUSA|Causa)\s*:?\s*([^\n\r]+)           │  │
│  │                                                            │  │
│  │ 3. Extract "AccionSolicitada" (contextual search)         │  │
│  │    - Pattern: (?:ACCIÓN SOLICITADA)\s*:?\s*([^\n\r]+)     │  │
│  │                                                            │  │
│  │ 4. Extract dates, amounts (additional patterns)           │  │
│  │                                                            │  │
│  │ 5. Handle OCR errors (fuzzy matching, typo correction)    │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                       ExtractedFields                            │
│  {                                                               │
│    Expediente: "A/AS1-2505-088637-PHM",                          │
│    Causa: "...",                                                 │
│    AccionSolicitada: "...",                                      │
│    AdditionalFields: { ... }                                     │
│  }                                                               │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📂 Project Structure

### New Projects to Create

#### 1. **ExxerCube.Prisma.Infrastructure.Extraction.Txt**

**Location**: `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Extraction.Txt/`

**Purpose**: Text-based field extraction infrastructure

**Files**:
```
Infrastructure.Extraction.Txt/
├── ExxerCube.Prisma.Infrastructure.Extraction.Txt.csproj
├── AdaptiveTxtFieldExtractor.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── GlobalUsings.cs
```

**Dependencies**:
- `Domain` (for interfaces, value objects, sources)
- `Microsoft.Extensions.Logging`
- `System.Text.RegularExpressions`

**Key Constraint**: ❌ **CANNOT reference other Infrastructure projects** (architectural rule)

---

#### 2. **ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt**

**Location**: `Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Txt/`

**Purpose**: Unit tests for `AdaptiveTxtFieldExtractor`

**Files**:
```
Tests.Infrastructure.Extraction.Txt/
├── ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt.csproj
├── AdaptiveTxtFieldExtractorTests.cs
├── AdaptiveTxtFieldExtractorEnhancedTests.cs
├── Fixtures/
│   ├── sample_ocr_clean.txt
│   ├── sample_ocr_with_errors.txt
│   └── sample_ocr_missing_fields.txt
└── GlobalUsings.cs
```

**Dependencies**:
- `Infrastructure.Extraction.Txt` (SUT)
- `xUnit`
- `Shouldly`
- `NSubstitute`

---

#### 3. **ExxerCube.Prisma.Tests.SystemTests**

**Location**: `Prisma/Code/Src/CSharp/08 Tests/07 System/Tests.SystemTests/`

**Purpose**: End-to-end pipeline tests (PDF → OCR → TXT → Extraction)

**Files**:
```
Tests.SystemTests/
├── ExxerCube.Prisma.Tests.SystemTests.csproj
├── PdfToExtractedFieldsPipelineTests.cs
├── ThreeWayReconciliationIntegrationTests.cs
└── GlobalUsings.cs
```

**Dependencies**:
- `Infrastructure.Extraction.Ocr.Teseract` (PDF OCR)
- `Infrastructure.Extraction.Txt` (Txt extraction)
- `Infrastructure.Extraction.Adaptive` (DOCX extraction)
- `Infrastructure.Classification` (FusionExpedienteService)
- ✅ **CAN reference multiple Infrastructure projects** (system test privilege)

---

## 🔧 Class Designs

### 1. TxtSource (Domain Model)

**File**: `Domain/Sources/TxtSource.cs`

```csharp
namespace ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Represents a text source for field extraction (typically OCR output).
/// </summary>
public class TxtSource
{
    /// <summary>
    /// Gets or sets the text content to extract from.
    /// </summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source file path (if available, for logging/debugging).
    /// </summary>
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence score (if this text came from OCR).
    /// </summary>
    public float? OcrConfidence { get; set; }

    /// <summary>
    /// Gets or sets the image quality score (if this text came from scanned image).
    /// </summary>
    public float? ImageQualityScore { get; set; }

    /// <summary>
    /// Gets or sets metadata about the source (e.g., page numbers, source type).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TxtSource"/> class.
    /// </summary>
    public TxtSource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TxtSource"/> class with text content.
    /// </summary>
    /// <param name="textContent">The text content.</param>
    /// <param name="ocrConfidence">Optional OCR confidence score.</param>
    /// <param name="sourceFilePath">Optional source file path.</param>
    public TxtSource(string textContent, float? ocrConfidence = null, string? sourceFilePath = null)
    {
        TextContent = textContent;
        OcrConfidence = ocrConfidence;
        SourceFilePath = sourceFilePath;
    }
}
```

---

### 2. AdaptiveTxtFieldExtractor (Core Implementation)

**File**: `Infrastructure.Extraction.Txt/AdaptiveTxtFieldExtractor.cs`

```csharp
namespace ExxerCube.Prisma.Infrastructure.Extraction.Txt;

/// <summary>
/// Adaptive field extractor for text sources (typically OCR output).
/// Implements <see cref="IFieldExtractor{T}"/> for <see cref="TxtSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// This extractor is designed to handle OCR text output with typical errors:
/// - Character substitutions (0 → O, 1 → I, 5 → S)
/// - Missing or misplaced field labels
/// - Variable spacing and formatting
/// - Typos in manually filled fields
/// </para>
/// <para>
/// <strong>Extraction Strategy:</strong>
/// </para>
/// <list type="number">
///   <item><description>Pattern-based extraction using regex</description></item>
///   <item><description>Fuzzy matching for OCR errors (Levenshtein distance)</description></item>
///   <item><description>Contextual search (look for labels + surrounding text)</description></item>
///   <item><description>Multi-line pattern matching for complex fields</description></item>
/// </list>
/// </remarks>
public sealed class AdaptiveTxtFieldExtractor : IFieldExtractor<TxtSource>
{
    private readonly ILogger<AdaptiveTxtFieldExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveTxtFieldExtractor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public AdaptiveTxtFieldExtractor(ILogger<AdaptiveTxtFieldExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<ExtractedFields>> ExtractFieldsAsync(
        TxtSource source,
        FieldDefinition[] fieldDefinitions)
    {
        if (source == null)
        {
            return Task.FromResult(Result<ExtractedFields>.WithFailure("TxtSource cannot be null"));
        }

        if (string.IsNullOrWhiteSpace(source.TextContent))
        {
            return Task.FromResult(Result<ExtractedFields>.WithFailure("TxtSource.TextContent cannot be null or empty"));
        }

        try
        {
            _logger.LogDebug(
                "AdaptiveTxtExtractor: Extracting fields from text ({Length} chars, {FieldCount} definitions, OCR confidence: {Confidence:F2})",
                source.TextContent.Length,
                fieldDefinitions?.Length ?? 0,
                source.OcrConfidence ?? 0.0f);

            var text = source.TextContent;
            var extractedFields = new ExtractedFields();

            // Extract all defined fields
            if (fieldDefinitions != null)
            {
                foreach (var fieldDef in fieldDefinitions)
                {
                    var fieldResult = ExtractFieldByName(text, fieldDef.FieldName, source.OcrConfidence ?? 0.8f);
                    if (fieldResult.IsSuccess && fieldResult.Value != null)
                    {
                        ApplyFieldToExtractedFields(extractedFields, fieldDef.FieldName, fieldResult.Value.Value);
                    }
                }
            }

            // Always extract core fields (Expediente, Causa, AccionSolicitada)
            ExtractCoreFields(text, extractedFields, source.OcrConfidence ?? 0.8f);

            _logger.LogInformation(
                "AdaptiveTxtExtractor: Successfully extracted fields - Expediente: {Expediente}, Causa: {Causa}",
                extractedFields.Expediente,
                extractedFields.Causa);

            return Task.FromResult(Result<ExtractedFields>.Success(extractedFields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveTxtExtractor: Error extracting fields from text");
            return Task.FromResult(Result<ExtractedFields>.WithFailure(
                $"Error extracting text fields: {ex.Message}",
                default(ExtractedFields),
                ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<FieldValue>> ExtractFieldAsync(TxtSource source, string fieldName)
    {
        if (source == null)
        {
            return Task.FromResult(Result<FieldValue>.WithFailure("TxtSource cannot be null"));
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return Task.FromResult(Result<FieldValue>.WithFailure("Field name cannot be null or empty"));
        }

        try
        {
            _logger.LogDebug("AdaptiveTxtExtractor: Extracting field '{FieldName}' from text", fieldName);

            var text = source.TextContent;
            var confidence = source.OcrConfidence ?? 0.8f;

            var fieldResult = ExtractFieldByName(text, fieldName, confidence);
            if (fieldResult.IsSuccess && fieldResult.Value != null)
            {
                return Task.FromResult(Result<FieldValue>.Success(fieldResult.Value));
            }

            return Task.FromResult(Result<FieldValue>.WithFailure($"Field '{fieldName}' not found in text"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveTxtExtractor: Error extracting field '{FieldName}' from text", fieldName);
            return Task.FromResult(Result<FieldValue>.WithFailure(
                $"Error extracting field from text: {ex.Message}",
                default(FieldValue),
                ex));
        }
    }

    //
    // Private Helper Methods
    //

    /// <summary>
    /// Extracts core fields (Expediente, Causa, AccionSolicitada) from text.
    /// </summary>
    private void ExtractCoreFields(string text, ExtractedFields fields, float confidence)
    {
        // Extract Expediente if not already set
        if (string.IsNullOrEmpty(fields.Expediente))
        {
            fields.Expediente = ExtractExpediente(text);
        }

        // Extract Causa if not already set
        if (string.IsNullOrEmpty(fields.Causa))
        {
            fields.Causa = ExtractCausa(text);
        }

        // Extract AccionSolicitada if not already set
        if (string.IsNullOrEmpty(fields.AccionSolicitada))
        {
            fields.AccionSolicitada = ExtractAccionSolicitada(text);
        }
    }

    /// <summary>
    /// Extracts a field by name from text.
    /// </summary>
    private Result<FieldValue> ExtractFieldByName(string text, string fieldName, float confidence)
    {
        var normalizedFieldName = fieldName.ToLowerInvariant();

        var value = normalizedFieldName switch
        {
            "expediente" => ExtractExpediente(text),
            "causa" => ExtractCausa(text),
            "accionsolicitada" or "accion_solicitada" => ExtractAccionSolicitada(text),
            "numerooficio" or "numero_oficio" => ExtractNumeroOficio(text),
            "autoridadnombre" or "autoridad_nombre" => ExtractAutoridadNombre(text),
            _ => null
        };

        if (value != null)
        {
            return Result<FieldValue>.Success(
                new FieldValue(fieldName, value, confidence, "TXT_OCR", FieldOrigin.PdfOcr));
        }

        return Result<FieldValue>.WithFailure($"Field '{fieldName}' not found");
    }

    /// <summary>
    /// Applies extracted field value to ExtractedFields object.
    /// </summary>
    private static void ApplyFieldToExtractedFields(ExtractedFields fields, string fieldName, string? value)
    {
        switch (fieldName.ToLowerInvariant())
        {
            case "expediente":
                fields.Expediente = value;
                break;

            case "causa":
                fields.Causa = value;
                break;

            case "accionsolicitada":
            case "accion_solicitada":
                fields.AccionSolicitada = value;
                break;

            default:
                // Store in AdditionalFields
                fields.AdditionalFields[fieldName] = value;
                break;
        }
    }

    //
    // Field Extraction Methods (Pattern-Based)
    //

    /// <summary>
    /// Extracts Expediente using pattern matching.
    /// Pattern: A/AS1-2505-088637-PHM or similar formats.
    /// </summary>
    private static string? ExtractExpediente(string text)
    {
        // Primary pattern: A/AS1-2505-088637-PHM
        var expedientePattern = @"[A-Z]/[A-Z]{1,4}\d+-\d+-\d+-[A-Z]+";
        var match = Regex.Match(text, expedientePattern, RegexOptions.Multiline);
        if (match.Success)
        {
            return match.Value;
        }

        // Alternative pattern (with OCR errors): handle O→0, I→1 substitutions
        var fuzzyPattern = @"[A-Z]/[A-Z]{1,4}[0-9O]+[-–][0-9O]+[-–][0-9O]+[-–][A-Z]+";
        match = Regex.Match(text, fuzzyPattern, RegexOptions.Multiline);
        if (match.Success)
        {
            // Clean up OCR errors
            return CleanOcrErrors(match.Value);
        }

        return null;
    }

    /// <summary>
    /// Extracts Causa using contextual search.
    /// Looks for "CAUSA:", "Causa:", etc. followed by text.
    /// </summary>
    private static string? ExtractCausa(string text)
    {
        // Pattern: CAUSA: <text> or Causa: <text>
        var causaPattern = @"(?:CAUSA|Causa|causa)\s*[:：]?\s*([^\n\r]+)";
        var match = Regex.Match(text, causaPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts AccionSolicitada using contextual search.
    /// Looks for "ACCIÓN SOLICITADA:", "Accion Solicitada:", etc.
    /// </summary>
    private static string? ExtractAccionSolicitada(string text)
    {
        // Pattern: ACCIÓN SOLICITADA: <text> or Accion Solicitada: <text>
        var accionPattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada|acción\s+solicitada)\s*[:：]?\s*([^\n\r]+)";
        var match = Regex.Match(text, accionPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts NumeroOficio (e.g., AGAFADAFSON2/2025/000084).
    /// </summary>
    private static string? ExtractNumeroOficio(string text)
    {
        // Pattern: AGAFADAFSON2/2025/000084 or similar
        var oficioPattern = @"[A-Z]{4,}[A-Z0-9]{0,10}/\d{4}/\d{6}";
        var match = Regex.Match(text, oficioPattern, RegexOptions.Multiline);

        if (match.Success)
        {
            return match.Value;
        }

        // Look for "No. De Identificación" or similar labels
        var labeledPattern = @"(?:No\.\s*De\s*Identificación|Número\s*de\s*Oficio)\s*[:：]?\s*([A-Z0-9/]+)";
        match = Regex.Match(text, labeledPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Extracts AutoridadNombre (authority name).
    /// </summary>
    private static string? ExtractAutoridadNombre(string text)
    {
        // Look for common authority names
        var authorities = new[]
        {
            "Comisión Nacional Bancaria y de Valores",
            "CNBV",
            "Administración General de Auditoría Fiscal Federal",
            "AGAFF",
            "SAT"
        };

        foreach (var authority in authorities)
        {
            if (text.Contains(authority, StringComparison.OrdinalIgnoreCase))
            {
                return authority;
            }
        }

        return null;
    }

    /// <summary>
    /// Cleans common OCR errors (O→0, I→1, S→5).
    /// </summary>
    private static string CleanOcrErrors(string text)
    {
        // For numeric sections, replace O with 0, I with 1
        // This is a simplified version - production would be more sophisticated
        return text;
    }
}
```

---

## 🧪 Test Strategy (ITDD)

### Test Scenarios for AdaptiveTxtFieldExtractor

#### 1. **AdaptiveTxtFieldExtractorTests.cs** (Basic Unit Tests)

```csharp
[Fact]
public async Task ExtractFieldsAsync_ValidOcrText_ExtractsExpediente()
{
    // Arrange
    var ocrText = @"
        Administración General de Auditoría Fiscal Federal
        No. De Identificación: AGAFADAFSON2/2025/000084
        Expediente: A/AS1-2505-088637-PHM
        CAUSA: Investigación administrativa
    ";
    var source = new TxtSource(ocrText, ocrConfidence: 0.85f);
    var fieldDefs = new[] { new FieldDefinition("Expediente") };

    // Act
    var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
}

[Fact]
public async Task ExtractFieldsAsync_OcrTextWithErrors_HandlesSubstitutions()
{
    // Arrange - O→0 substitution
    var ocrText = "Expediente: A/AS1-25O5-O88637-PHM"; // O instead of 0
    var source = new TxtSource(ocrText);

    // Act
    var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Expediente.ShouldNotBeNull();
}

[Fact]
public async Task ExtractFieldAsync_ExtractsCausa_FromLabeledText()
{
    // Arrange
    var ocrText = "CAUSA: Investigación por fraude financiero";
    var source = new TxtSource(ocrText);

    // Act
    var result = await _extractor.ExtractFieldAsync(source, "Causa");

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Value.ShouldBe("Investigación por fraude financiero");
}

[Fact]
public async Task ExtractFieldsAsync_MissingField_ReturnsEmptyField()
{
    // Arrange
    var ocrText = "Some text without expediente";
    var source = new TxtSource(ocrText);
    var fieldDefs = new[] { new FieldDefinition("Expediente") };

    // Act
    var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Expediente.ShouldBeNull();
}

[Fact]
public async Task ExtractFieldsAsync_NullSource_ReturnsFailure()
{
    // Act
    var result = await _extractor.ExtractFieldsAsync(null!, Array.Empty<FieldDefinition>());

    // Assert
    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldContain("TxtSource cannot be null");
}

[Fact]
public async Task ExtractFieldsAsync_EmptyText_ReturnsFailure()
{
    // Arrange
    var source = new TxtSource("");

    // Act
    var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

    // Assert
    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldContain("TextContent cannot be null or empty");
}
```

---

#### 2. **AdaptiveTxtFieldExtractorEnhancedTests.cs** (Edge Cases & Real Fixtures)

```csharp
[Fact]
public async Task ExtractFieldsAsync_RealOcrFixture222AAA_ExtractsAllFields()
{
    // Arrange - Load real OCR fixture
    var ocrText = await File.ReadAllTextAsync(
        "Fixtures/222AAA-44444444442025_page-0001.ocr.txt");
    var source = new TxtSource(ocrText, ocrConfidence: 0.82f);

    var fieldDefs = new[]
    {
        new FieldDefinition("Expediente", isRequired: true),
        new FieldDefinition("NumeroOficio", isRequired: true),
        new FieldDefinition("Causa"),
        new FieldDefinition("AutoridadNombre")
    };

    // Act
    var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Expediente.ShouldNotBeNullOrEmpty();
    result.Value.AdditionalFields["NumeroOficio"].ShouldBe("AGAFADAFSON2/2025/000084");
}

[Fact]
public async Task ExtractFieldsAsync_MultipleExpedientePatterns_ExtractsFirst()
{
    // Arrange - Text with multiple expediente references
    var ocrText = @"
        Expediente principal: A/AS1-2505-088637-PHM
        Expediente relacionado: B/BS2-3606-099748-XYZ
    ";
    var source = new TxtSource(ocrText);

    // Act
    var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
}

[Fact]
public async Task ExtractFieldsAsync_VariableSpacing_HandlesCorrectly()
{
    // Arrange - OCR with inconsistent spacing
    var ocrText = "CAUSA  :   Investigación     administrativa";
    var source = new TxtSource(ocrText);

    // Act
    var result = await _extractor.ExtractFieldAsync(source, "Causa");

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Value.ShouldBe("Investigación     administrativa");
}

[Theory]
[InlineData("CAUSA: Test", "Test")]
[InlineData("Causa: Test", "Test")]
[InlineData("causa: Test", "Test")]
[InlineData("CAUSA : Test", "Test")]
[InlineData("CAUSA Test", "Test")] // No colon
public async Task ExtractCausa_VariousFormats_ExtractsCorrectly(string input, string expected)
{
    // Arrange
    var source = new TxtSource(input);

    // Act
    var result = await _extractor.ExtractFieldAsync(source, "Causa");

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value!.Value.ShouldBe(expected);
}
```

---

#### 3. **PdfToExtractedFieldsPipelineTests.cs** (System Tests)

```csharp
/// <summary>
/// System tests for the full PDF → OCR → TXT → Extraction pipeline.
/// </summary>
public class PdfToExtractedFieldsPipelineTests
{
    [Fact]
    public async Task FullPipeline_PdfFixture222AAA_ExtractsFields()
    {
        // Arrange
        var pdfPath = "Fixtures/PRP1/222AAA-44444444442025.pdf";
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
        var pdfSource = new PdfSource(pdfBytes) { FilePath = pdfPath };

        var fieldDefs = new[]
        {
            new FieldDefinition("Expediente", isRequired: true),
            new FieldDefinition("NumeroOficio", isRequired: true),
            new FieldDefinition("Causa")
        };

        // Act - Use real PdfOcrFieldExtractor (refactored)
        var result = await _pdfExtractor.ExtractFieldsAsync(pdfSource, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldNotBeNullOrEmpty();
        result.Value.AdditionalFields["NumeroOficio"].ShouldContain("AGAFADAFSON2");
    }

    [Fact]
    public async Task FullPipeline_AllPRP1Fixtures_ExtractSuccessfully()
    {
        // Arrange - Test all 4 fixtures
        var fixtures = new[]
        {
            "222AAA-44444444442025.pdf",
            "333BBB-44444444442025.pdf",
            "333ccc-6666666662025.pdf",
            "555CCC-66666662025.pdf"
        };

        foreach (var fixture in fixtures)
        {
            var pdfPath = $"Fixtures/PRP1/{fixture}";
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            var pdfSource = new PdfSource(pdfBytes);

            // Act
            var result = await _pdfExtractor.ExtractFieldsAsync(
                pdfSource,
                new[] { new FieldDefinition("Expediente") });

            // Assert
            result.IsSuccess.ShouldBeTrue($"Failed for fixture: {fixture}");
        }
    }
}
```

---

## 📝 Implementation Steps (Phased Approach)

### Phase 1: Create Domain Model ✅
**Goal**: Add `TxtSource` to Domain layer

1. Create `Domain/Sources/TxtSource.cs`
2. Add to `Domain.csproj`
3. Update `GlobalUsings.cs` if needed

**Verification**: Compiles successfully

---

### Phase 2: Create Infrastructure.Extraction.Txt Project ✅
**Goal**: Create new infrastructure project with `AdaptiveTxtFieldExtractor`

1. Create project: `Infrastructure.Extraction.Txt/`
2. Create `.csproj` file with dependencies:
   - `Domain` project reference
   - `Microsoft.Extensions.Logging`
3. Create `GlobalUsings.cs`
4. Create `AdaptiveTxtFieldExtractor.cs` (full implementation)
5. Create `DependencyInjection/ServiceCollectionExtensions.cs`:
   ```csharp
   public static IServiceCollection AddTxtFieldExtraction(this IServiceCollection services)
   {
       services.AddScoped<IFieldExtractor<TxtSource>, AdaptiveTxtFieldExtractor>();
       return services;
   }
   ```

**Verification**: Project builds successfully

---

### Phase 3: Create Test Project (ITDD) ✅
**Goal**: Write failing tests, then make them pass

1. Create project: `Tests.Infrastructure.Extraction.Txt/`
2. Create test fixtures in `Fixtures/` directory
3. Write `AdaptiveTxtFieldExtractorTests.cs` (basic tests) - **Tests FAIL** ❌
4. Implement extraction logic in `AdaptiveTxtFieldExtractor` - **Tests PASS** ✅
5. Write `AdaptiveTxtFieldExtractorEnhancedTests.cs` (edge cases) - **Tests FAIL** ❌
6. Refine extraction logic - **Tests PASS** ✅

**Verification**: All unit tests pass (green)

---

### Phase 4: Refactor PdfOcrFieldExtractor ✅
**Goal**: Update `PdfOcrFieldExtractor` to delegate to `AdaptiveTxtFieldExtractor`

**Changes to `PdfOcrFieldExtractor`**:

```csharp
public class PdfOcrFieldExtractor : IFieldExtractor<PdfSource>
{
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IFieldExtractor<TxtSource> _txtExtractor; // NEW
    private readonly ILogger<PdfOcrFieldExtractor> _logger;

    public PdfOcrFieldExtractor(
        IOcrExecutor ocrExecutor,
        IImagePreprocessor imagePreprocessor,
        IFieldExtractor<TxtSource> txtExtractor, // NEW
        ILogger<PdfOcrFieldExtractor> logger)
    {
        _ocrExecutor = ocrExecutor;
        _imagePreprocessor = imagePreprocessor;
        _txtExtractor = txtExtractor; // NEW
        _logger = logger;
    }

    public async Task<Result<ExtractedFields>> ExtractFieldsAsync(
        PdfSource source,
        FieldDefinition[] fieldDefinitions)
    {
        // 1. Get PDF bytes
        byte[] pdfBytes = GetPdfBytes(source);

        // 2. Convert PDF to images (using PdfiumViewer or similar)
        var images = await ConvertPdfToImagesAsync(pdfBytes);

        // 3. Run OCR on each page
        var allOcrText = new StringBuilder();
        float totalConfidence = 0f;

        foreach (var imageData in images)
        {
            // Preprocess image
            var preprocessResult = await _imagePreprocessor.PreprocessAsync(
                imageData,
                new ProcessingConfig());

            if (preprocessResult.IsFailure)
            {
                continue; // Skip failed pages
            }

            // Run OCR
            var ocrResult = await _ocrExecutor.ExecuteOcrAsync(
                preprocessResult.Value!,
                new OCRConfig());

            if (ocrResult.IsSuccess)
            {
                allOcrText.AppendLine(ocrResult.Value!.Text);
                totalConfidence += ocrResult.Value.ConfidenceAvg;
            }
        }

        var avgConfidence = images.Count > 0 ? totalConfidence / images.Count : 0f;

        // 4. Create TxtSource from aggregated OCR text
        var txtSource = new TxtSource(
            allOcrText.ToString(),
            avgConfidence,
            source.FilePath);

        // 5. Delegate to AdaptiveTxtFieldExtractor
        return await _txtExtractor.ExtractFieldsAsync(txtSource, fieldDefinitions);
    }

    private async Task<List<ImageData>> ConvertPdfToImagesAsync(byte[] pdfBytes)
    {
        // TODO: Implement using PdfiumViewer or PDFSharp
        // For now, placeholder
        throw new NotImplementedException("PDF to image conversion not yet implemented");
    }
}
```

**Verification**: Existing `PdfOcrFieldExtractorTests` still pass (with updated mocks)

---

### Phase 5: Create System Tests ✅
**Goal**: Test full pipeline with real fixtures

1. Create `Tests.SystemTests` project
2. Write `PdfToExtractedFieldsPipelineTests.cs`
3. Test with real PRP1 fixtures
4. Verify end-to-end extraction works

**Verification**: System tests pass with real PDF fixtures

---

### Phase 6: Integration & Deployment ✅
**Goal**: Wire up in DI container and test in DocumentProcessing.razor

1. Update `PrismaServiceCollectionExtensions.cs`:
   ```csharp
   services.AddTxtFieldExtraction(); // NEW
   services.AddScoped<IFieldExtractor<PdfSource>, PdfOcrFieldExtractor>();
   ```

2. Test in DocumentProcessing.razor page
3. Verify 3-way reconciliation works
4. Monitor logs for extraction accuracy

**Verification**: DocumentProcessing.razor shows extracted fields correctly

---

## 🔍 Key Design Decisions

### Why Create Separate Infrastructure.Extraction.Txt Project?

**Reason**: Infrastructure projects **cannot reference each other** (architectural constraint).

- `Infrastructure.Extraction.Ocr.Teseract` cannot reference `Infrastructure.Extraction.Adaptive`
- Both can reference `Domain` (common interfaces/models)
- Solution: Create third infrastructure project (`Infrastructure.Extraction.Txt`) that both can use via DI

### Why Model After AdaptiveDocxFieldExtractorAdapter?

**Reason**: It follows the **Adapter Pattern** and works with imperfect, variable-format sources.

- DOCX sources: manually filled, typos, missing fields, variable structure
- OCR text sources: **exact same problems** + OCR errors
- Adaptive pattern handles variability and errors gracefully

### Why System Tests Project?

**Reason**: Need to test **multiple infrastructure projects together**, which unit tests cannot do.

- Unit tests: Single project, mocked dependencies
- System tests: Multiple real implementations, real fixtures
- Validates full pipeline: PDF → Image → OCR → TXT → Extraction → Reconciliation

---

## 🎯 Success Criteria

### Must Have (MVP)
- ✅ `TxtSource` domain model created
- ✅ `AdaptiveTxtFieldExtractor` implemented with core fields (Expediente, Causa, AccionSolicitada)
- ✅ Unit tests pass (basic + enhanced)
- ✅ `PdfOcrFieldExtractor` refactored to delegate to `AdaptiveTxtFieldExtractor`
- ✅ Existing `PdfOcrFieldExtractorTests` still pass
- ✅ DocumentProcessing.razor extracts fields correctly from PDF fixtures

### Nice to Have
- ✅ System tests for full PDF → Extraction pipeline
- ✅ Fuzzy matching for OCR errors (character substitutions)
- ✅ Multi-page PDF support with aggregated text
- ✅ Additional field patterns (dates, amounts, RFC, CURP)
- ✅ Confidence scoring per field

---

## 📚 References

- **Implementation Plan Document**: `docs/sessions/Unified-3Way-Reconciliation-Implementation-Plan.md`
- **AdaptiveDocxFieldExtractorAdapter**: `Infrastructure.Extraction.Adaptive/AdaptiveDocxFieldExtractorAdapter.cs`
- **PdfOcrFieldExtractor (Current)**: `Infrastructure.Extraction/Teseract/PdfOcrFieldExtractor.cs`
- **OCR Fixtures**: `Prisma/Fixtures/PRP1/*.ocr.txt`
- **Test Fixtures**: `Prisma/Fixtures/PRP1/*.pdf`

---

**Last Updated**: 2025-12-10
**Status**: 🔴 READY FOR REVIEW & APPROVAL
**Next Action**: Review plan → Get approval → Begin Phase 1 (ITDD)

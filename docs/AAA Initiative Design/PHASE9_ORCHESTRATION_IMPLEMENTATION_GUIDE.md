# Phase 9 Orchestration Implementation Guide — Consolidated

**Last Updated:** December 2, 2024
**Status:** Stages 1-2 Complete (28/28 tests passing), Stage 3 Ready
**Current Commit:** `ecf7de3` - Clean build, 0 errors, 0 warnings

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Current State & Progress](#current-state--progress)
3. [**CRITICAL: What to Use vs What to Avoid**](#critical-what-to-use-vs-what-to-avoid)
4. [**Lessons Learned from Stage 3 Attempt**](#lessons-learned-from-stage-3-attempt)
5. [Architecture & Design Principles](#architecture--design-principles)
6. [Existing Infrastructure (MUST USE)](#existing-infrastructure-must-use)
7. [Implementation Stages](#implementation-stages)
8. [Stage 3: Athena Processing (Current Focus)](#stage-3-athena-processing-current-focus)
9. [Dependencies & Project References](#dependencies--project-references)
10. [Testing Standards](#testing-standards)
11. [Appendix: Stash Review & Valuable Content](#appendix-stash-review--valuable-content)

---

## Executive Summary

### What Works (DO NOT BREAK)

**Phase 2 Complete:**
- ✅ 39/39 R29 fields (100% fusion coverage)
- ✅ All domain interfaces and infrastructure implementations complete

**Phase 9 Stages 1-2 Complete:**
- ✅ **Stage 1:** Infrastructure.Events with InMemoryEventBus (10/10 tests)
- ✅ **Stage 2:** Orion.Ingestion with IngestionOrchestrator (5/5 tests)
- ✅ **Total:** 28/28 tests passing (7 serialization + 10 event bus + 6 composition + 5 ingestion)
- ✅ **Build:** Clean (0 errors, 0 warnings)

### What's Next

**Stage 3: Athena Processing Orchestrator**
- **Goal:** Wire existing domain services together (NOT create new ones)
- **Approach:** Pure plumbing/coordination using existing interfaces
- **Status:** Ready to implement (clean slate after failed attempt)

---

## Current State & Progress

### Completed Work

| Stage | Component | Tests | Status |
|-------|-----------|-------|--------|
| **Stage 1** | Infrastructure.Events.InMemoryEventBus | 10/10 ✅ | COMPLETE |
| **Stage 1** | Prisma.Shared.Contracts | 7/7 ✅ | COMPLETE |
| **Stage 1** | Prisma.Composition.Tests | 6/6 ✅ | COMPLETE |
| **Stage 2** | Prisma.Orion.Ingestion.IngestionOrchestrator | 5/5 ✅ | COMPLETE |
| **Stage 2** | Prisma.Orion.Ingestion.FileIngestionJournal | Validated ✅ | COMPLETE |
| **Stage 3** | Prisma.Athena.Processing.ProcessingOrchestrator | 0/7 ❌ | **PENDING** |

### Git Status

```bash
Current Branch: Kt2
Last Clean Commit: ecf7de3 (Stages 1-2 complete)
Stash: stash@{0} - Stage 3 attempt (has 188 errors - DO NOT USE AS-IS)
Working Tree: CLEAN ✅
Build Status: SUCCESS ✅ (0 errors, 0 warnings)
```

---

## CRITICAL: What to Use vs What to Avoid

### ✅ MUST USE - Existing Domain Interfaces

**ALL orchestration MUST use these existing interfaces. DO NOT create new ones.**

#### Quality Analysis
```csharp
// Use existing interface from Domain/Interfaces
IImageQualityAnalyzer
  Task<Result<ImageQualityAssessment>> AnalyzeAsync(ImageData imageData);
  Task<Result<ImageQualityLevel>> GetQualityLevelAsync(ImageData imageData);

IFilterSelectionStrategy
```

#### OCR Execution
```csharp
// Use existing interfaces from Domain/Interfaces
IOcrExecutor
  Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config);

IOcrProcessingService
IOcrSessionRepository
```

#### XML/Metadata Extraction
```csharp
// Use existing interfaces from Domain/Interfaces
IMetadataExtractor
IFieldExtractor<T>
IXmlNullableParser<T>
```

#### Fusion & Reconciliation
```csharp
// Use existing interface from Domain/Interfaces - THIS IS 100% COMPLETE!
IFusionExpediente
  Task<Result<FusionResult>> FuseAsync(
    Expediente? xmlExpediente,
    Expediente? pdfExpediente,
    Expediente? docxExpediente,
    ExtractionMetadata xmlMetadata,
    ExtractionMetadata pdfMetadata,
    ExtractionMetadata docxMetadata,
    CancellationToken cancellationToken);

IFieldMatcher
```

#### Classification
```csharp
// Use existing interfaces from Domain/Interfaces
IFileClassifier
ILegalDirectiveClassifier
```

#### Export
```csharp
// Use existing interfaces from Domain/Interfaces
IResponseExporter
IAdaptiveExporter
  Task<Result<byte[]>> ExportAsync(object sourceObject, string templateType, CancellationToken cancellationToken);
```

#### Audit & Events
```csharp
// Use existing interfaces from Domain/Interfaces
IAuditLogger
IEventPublisher  // From Infrastructure.Events (already implemented!)
```

#### Ingestion Helpers
```csharp
// Use existing interfaces from Domain/Interfaces
IBrowserAutomationAgent
IDownloadStorage
IDownloadTracker
```

### ❌ DO NOT CREATE - Forbidden Duplicates

**These were mistakes from Stage 3 attempt - DO NOT RECREATE:**

```csharp
// ❌ WRONG - Creating new coordinator interfaces
IQualityAnalysisCoordinator  // Duplicate! Use IImageQualityAnalyzer
IOcrCoordinator              // Duplicate! Use IOcrExecutor
IFusionCoordinator           // Duplicate! Use IFusionExpediente
IClassificationCoordinator   // Duplicate! Use IFileClassifier
IExportCoordinator           // Duplicate! Use IAdaptiveExporter

// ❌ WRONG - Creating new result types
QualityAnalysisResult   // Duplicate! Use ImageQualityAssessment from Domain
OcrExtractionResult     // Duplicate! Use OCRResult from Domain
ClassificationResult    // Duplicate! Use existing ClassificationResult from Domain.ValueObjects

// ❌ WRONG - Creating new event types unnecessarily
// Only create events if they don't exist in Domain/Events
```

### ✅ CORRECT Approach for Stage 3

**ProcessingOrchestrator should:**

1. **Inject existing services** via constructor DI
2. **Call existing methods** on existing interfaces
3. **Emit existing events** using IEventPublisher
4. **Coordinate** - not reimplment logic

**Example (Correct):**
```csharp
public sealed class ProcessingOrchestrator
{
    private readonly IImageQualityAnalyzer _qualityAnalyzer;      // ✅ Existing interface
    private readonly IOcrExecutor _ocrExecutor;                   // ✅ Existing interface
    private readonly IFusionExpediente _fusionService;            // ✅ Existing interface
    private readonly IFileClassifier _classifier;                 // ✅ Existing interface
    private readonly IAdaptiveExporter _exporter;                 // ✅ Existing interface
    private readonly IEventPublisher _eventPublisher;             // ✅ From Infrastructure.Events
    private readonly ILogger<ProcessingOrchestrator> _logger;

    public async Task ProcessDocumentAsync(
        DocumentDownloadedEvent downloadEvent,
        CancellationToken cancellationToken)
    {
        // ✅ CORRECT: Just plumbing existing services
        var fileId = downloadEvent.FileId;
        var correlationId = downloadEvent.CorrelationId;

        // Step 1: Quality analysis (use existing service)
        var imageData = await LoadImageData(fileId);  // Helper method
        var qualityResult = await _qualityAnalyzer.AnalyzeAsync(imageData);

        if (!qualityResult.IsSuccess || qualityResult.Value.QualityLevel == ImageQualityLevel.Poor)
        {
            _eventPublisher.Publish(new QualityRejectedEvent { ... });
            return; // Stop pipeline
        }

        // Step 2: OCR (use existing service)
        var ocrConfig = new OCRConfig { ... };
        var ocrResult = await _ocrExecutor.ExecuteOcrAsync(imageData, ocrConfig);

        // Step 3: Fusion (use existing 100% complete service!)
        var fusionResult = await _fusionService.FuseAsync(...);

        // Step 4: Classification (use existing service)
        var classificationResult = await _classifier.ClassifyAsync(...);

        // Step 5: Export (use existing service)
        var exportResult = await _exporter.ExportAsync(...);

        // Emit completion event
        _eventPublisher.Publish(new DocumentProcessingCompletedEvent { ... });
    }
}
```

---

## Lessons Learned from Stage 3 Attempt

### ❌ What Went Wrong

**Problem:** Agent created 188 compilation errors by:
1. Creating duplicate coordinator interfaces (IQualityAnalysisCoordinator, etc.)
2. Creating duplicate result types (QualityAnalysisResult, OcrResult)
3. Breaking existing Domain classes with naming collisions
4. Ignoring explicit guidance to use existing interfaces

**Impact:**
- Broke a clean build
- Created architectural violations
- Wasted time on cleanup
- Confused the implementation scope

### ✅ Root Cause Analysis

**Why it happened:**
1. **Misunderstood task scope** - Agent thought "orchestration" meant "create new abstraction layer"
2. **Ignored existing infrastructure** - Did not check what interfaces already existed
3. **Created before reading** - Did not read existing Domain interfaces before implementing
4. **Over-engineered** - Created "coordinator" wrappers instead of direct usage

### ✅ How to Prevent This

**For Future Implementation:**

1. **READ FIRST, CODE SECOND**
   ```bash
   # Step 1: Find all existing interfaces
   grep -r "interface I" Prisma/Code/Src/CSharp/01-Core/Domain/Interfaces/

   # Step 2: Read each interface to understand its contract
   # Step 3: ONLY THEN start coding orchestrator
   ```

2. **Orchestration = Plumbing**
   - ✅ Orchestrate existing services
   - ❌ Do NOT create new abstractions
   - ❌ Do NOT create new result types
   - ❌ Do NOT create new interfaces

3. **When in Doubt, ASK**
   - If you think you need a new interface, ASK FIRST
   - If you think existing interfaces don't match, ASK FIRST
   - If you're creating new classes in Domain, ASK FIRST

4. **Verify No Duplicates**
   ```bash
   # Before creating ANY new class, check if it exists:
   grep -r "class QualityAnalysisResult" Prisma/Code/Src/CSharp/
   grep -r "interface IQualityAnalyzer" Prisma/Code/Src/CSharp/
   ```

5. **Keep Build Clean**
   ```bash
   # Build after EVERY change
   dotnet build ExxerCube.Prisma.sln
   # If errors appear, STOP and fix immediately
   ```

---

## Architecture & Design Principles

### Hexagonal Architecture (ENFORCE STRICTLY)

```
┌─────────────────────────────────────────────────────────┐
│ Hosts (Workers) - ONLY WIRE DI                          │
│ - Prisma.Orion.Worker                                   │
│ - Prisma.Athena.Worker                                  │
│ - Prisma.Sentinel.Monitor                               │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│ Orchestrators (Coordination Logic) - PLUMBING ONLY      │
│ - Prisma.Orion.Ingestion.IngestionOrchestrator         │
│ - Prisma.Athena.Processing.ProcessingOrchestrator  ← STAGE 3
│ └─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│ Domain Interfaces - ALREADY 100% COMPLETE               │
│ - IImageQualityAnalyzer, IOcrExecutor, IFusionExpediente │
│ - IFileClassifier, IAdaptiveExporter, IEventPublisher   │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│ Infrastructure Implementations - ALREADY COMPLETE        │
│ - Infrastructure.Quality, Infrastructure.Ocr            │
│ - Infrastructure.Classification (100% fusion!)          │
│ - Infrastructure.Export, Infrastructure.Events          │
└─────────────────────────────────────────────────────────┘
```

### SOLID Principles (VERIFY COMPLIANCE)

**Single Responsibility:**
- ProcessingOrchestrator = coordinate pipeline (NOT implement stages)
- Each service does ONE thing

**Open/Closed:**
- Orchestrator depends on interfaces (open for extension)
- Domain interfaces are closed for modification

**Liskov Substitution:**
- Any implementation of IImageQualityAnalyzer works identically
- Tests prove substitutability

**Interface Segregation:**
- Small, focused interfaces (IImageQualityAnalyzer has 2 methods)
- No fat interfaces

**Dependency Inversion:**
- Orchestrator depends on abstractions (IEventPublisher)
- Infrastructure depends on Domain (not vice versa)

### Defensive Programming ("NEVER CRASH")

```csharp
// ✅ CORRECT - Defensive error handling
try
{
    var result = await _ocrExecutor.ExecuteOcrAsync(imageData, config, cancellationToken);
    if (!result.IsSuccess)
    {
        _logger.LogWarning("OCR failed: {Error}", result.Error);
        _eventPublisher.Publish(new ProcessingErrorEvent
        {
            FileId = fileId,
            Component = "OCR",
            ErrorMessage = result.Error,
            CorrelationId = correlationId
        });
        return; // Stop pipeline gracefully
    }
}
catch (Exception ex)
{
    // NEVER CRASH - log and emit error event
    _logger.LogError(ex, "OCR execution threw exception");
    _eventPublisher.Publish(new ProcessingErrorEvent { ... });
    return; // Stop pipeline gracefully
}
```

---

## Existing Infrastructure (MUST USE)

### Complete Domain Interfaces

| Interface | Location | Purpose | Status |
|-----------|----------|---------|--------|
| IImageQualityAnalyzer | Domain/Interfaces | Quality analysis | ✅ COMPLETE |
| IOcrExecutor | Domain/Interfaces | OCR execution | ✅ COMPLETE |
| IFusionExpediente | Domain/Interfaces | **39/39 fields fusion** | ✅ 100% COMPLETE! |
| IFileClassifier | Domain/Interfaces | Classification | ✅ COMPLETE |
| IAdaptiveExporter | Domain/Interfaces | Export generation | ✅ COMPLETE |
| IEventPublisher | Domain/Interfaces | Event publishing | ✅ IMPLEMENTED (Stage 1) |
| IAuditLogger | Domain/Interfaces | Audit logging | ✅ COMPLETE |

### Complete Infrastructure Implementations

| Implementation | Location | Status |
|----------------|----------|--------|
| EmguCVImageQualityAnalyzer | Infrastructure.Quality | ✅ COMPLETE |
| TesseractOcrExecutor | Infrastructure.Ocr | ✅ COMPLETE |
| FusionExpedienteService | Infrastructure.Classification | ✅ 100% COMPLETE (39/39 fields!) |
| FileClassificationService | Infrastructure.Classification | ✅ COMPLETE |
| AdaptiveExportService | Infrastructure.Export | ✅ COMPLETE |
| InMemoryEventBus | Infrastructure.Events | ✅ IMPLEMENTED (Stage 1, 10/10 tests) |

### Events Already Defined

**File:** `Domain/Events/ProcessingEvents.cs`

```csharp
// ✅ Already exist - USE THESE
DocumentDownloadedEvent
QualityAnalysisCompletedEvent
OcrCompletedEvent
ClassificationCompletedEvent
ConflictDetectedEvent
DocumentFlaggedForReviewEvent
DocumentProcessingCompletedEvent
ProcessingErrorEvent
```

**Need to Add (from stash review):**
```csharp
// ✅ These are valuable from stash - ADD THESE
QualityRejectedEvent      // When quality is unacceptable
FusionCompletedEvent      // When fusion completes
ExportCompletedEvent      // When export completes
```

---

## Implementation Stages

### Stage 1: Infrastructure Foundation ✅ COMPLETE

**Delivered:**
- InMemoryEventBus (10/10 tests passing)
- Event serialization (7/7 tests passing)
- DI composition (6/6 tests passing)
- IEventPublisher, IEventSubscriber, IIngestionJournal interfaces

**Status:** ✅ COMPLETE (28/28 total tests passing)

### Stage 2: Orion Ingestion ✅ COMPLETE

**Delivered:**
- IngestionOrchestrator with hash-based idempotency (5/5 tests)
- FileIngestionJournal for duplicate detection
- Partitioned storage (YYYY/MM/DD/{docId}.pdf)
- DocumentDownloadedEvent emission

**Status:** ✅ COMPLETE (all tests passing, clean build)

### Stage 3: Athena Processing ⚠️ CURRENT FOCUS

**Goal:** Orchestrate Quality → OCR → Fusion → Classification → Export pipeline

**What to Build:**
1. ProcessingOrchestrator that coordinates existing services
2. Tests for pipeline execution and event emission
3. Defensive error handling throughout

**What NOT to Build:**
- ❌ New coordinator interfaces
- ❌ New result types
- ❌ Duplicate implementations

**Status:** Ready to implement (clean slate after failed attempt)

---

## Stage 3: Athena Processing (Current Focus)

### Project Structure

```
Prisma.Athena.Processing/
├── ProcessingOrchestrator.cs  ← Main orchestrator (PLUMBING ONLY)
└── Prisma.Athena.Processing.csproj

Prisma.Athena.Processing.Tests/  ← Create this
├── ProcessingOrchestratorTests.cs
└── Prisma.Athena.Processing.Tests.csproj
```

### Required Dependencies

**Prisma.Athena.Processing.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>

  <ItemGroup>
    <!-- Domain has ALL interfaces -->
    <ProjectReference Include="..\..\01-Core\Domain\ExxerCube.Prisma.Domain.csproj" />

    <!-- Events -->
    <ProjectReference Include="..\..\06-Shared\Prisma.Shared.Contracts\Prisma.Shared.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**Prisma.Athena.Processing.Tests.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup Label="Core Properties">
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <OutputType>Exe</OutputType>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
  </PropertyGroup>

  <PropertyGroup Label="Testing Platform">
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <TestingPlatformServer>true</TestingPlatformServer>
  </PropertyGroup>

  <ItemGroup Label="Testing Utilities">
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
  </ItemGroup>

  <ItemGroup Label="Microsoft Testing Platform">
    <PackageReference Include="Microsoft.Testing.Platform" />
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
    <PackageReference Include="Microsoft.Testing.Extensions.VSTestBridge" />
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" />
  </ItemGroup>

  <ItemGroup Label="xUnit v3">
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="xunit.v3.runner.inproc.console" />
    <PackageReference Include="xunit.v3.runner.msbuild">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\01-Core\Domain\ExxerCube.Prisma.Domain.csproj" />
    <ProjectReference Include="..\..\06-Athena\Prisma.Athena.Processing\Prisma.Athena.Processing.csproj" />
  </ItemGroup>

  <ItemGroup Label="Global Usings">
    <Using Include="Xunit" />
    <Using Include="NSubstitute" />
    <Using Include="Shouldly" />
    <Using Include="Microsoft.Extensions.Logging" />
    <Using Include="System" />
    <Using Include="System.Collections.Generic" />
    <Using Include="System.Linq" />
    <Using Include="System.Threading" />
    <Using Include="System.Threading.Tasks" />
  </ItemGroup>
</Project>
```

### Test Structure (TDD - Write Tests First!)

**File:** `Prisma.Athena.Processing.Tests/ProcessingOrchestratorTests.cs`

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Events;
using Prisma.Athena.Processing;

namespace Prisma.Athena.Processing.Tests;

public sealed class ProcessingOrchestratorTests
{
    [Fact]
    public async Task ProcessDocument_ExecutesFullPipeline()
    {
        // Arrange - mock EXISTING interfaces
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();
        var classifier = Substitute.For<IFileClassifier>();
        var exporter = Substitute.For<IAdaptiveExporter>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        // Setup mocks to return success
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ImageQualityLevel.Q4_Good,
                Confidence = 0.95f
            }));

        // ... setup other mocks ...

        var orchestrator = new ProcessingOrchestrator(
            qualityAnalyzer,
            ocrExecutor,
            fusionService,
            classifier,
            exporter,
            eventPublisher,
            logger);

        var downloadEvent = new DocumentDownloadedEvent { /* ... */ };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - verify pipeline executed
        await qualityAnalyzer.Received(1).AnalyzeAsync(Arg.Any<ImageData>());
        await ocrExecutor.Received(1).ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
        // ... verify other stages called ...
    }

    [Fact]
    public async Task ProcessDocument_PoorQuality_SkipsDownstreamPipeline()
    {
        // Test quality gate logic
    }

    [Fact]
    public async Task ProcessDocument_EmitsEventsAtEachStage()
    {
        // Test event emission
    }

    [Fact]
    public async Task ProcessDocument_PreservesCorrelationId()
    {
        // Test correlation ID propagation
    }

    [Fact]
    public async Task ProcessDocument_ErrorInOcr_EmitsErrorEvent_DoesNotCrash()
    {
        // Test defensive error handling (NEVER CRASH)
    }
}
```

### Implementation Structure (After Tests Pass)

**File:** `Prisma.Athena.Processing/ProcessingOrchestrator.cs`

```csharp
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing;

/// <summary>
/// Orchestrates document processing pipeline: Quality → OCR → Fusion → Classification → Export.
/// </summary>
/// <remarks>
/// Pure coordination logic - delegates to existing Domain services.
/// Defensive "NEVER CRASH" philosophy - logs errors and emits events instead of throwing.
/// </remarks>
public sealed class ProcessingOrchestrator
{
    private readonly IImageQualityAnalyzer _qualityAnalyzer;
    private readonly IOcrExecutor _ocrExecutor;
    private readonly IFusionExpediente _fusionService;
    private readonly IFileClassifier _classifier;
    private readonly IAdaptiveExporter _exporter;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ProcessingOrchestrator> _logger;

    public ProcessingOrchestrator(
        IImageQualityAnalyzer qualityAnalyzer,
        IOcrExecutor ocrExecutor,
        IFusionExpediente fusionService,
        IFileClassifier classifier,
        IAdaptiveExporter exporter,
        IEventPublisher eventPublisher,
        ILogger<ProcessingOrchestrator> logger)
    {
        _qualityAnalyzer = qualityAnalyzer ?? throw new ArgumentNullException(nameof(qualityAnalyzer));
        _ocrExecutor = ocrExecutor ?? throw new ArgumentNullException(nameof(ocrExecutor));
        _fusionService = fusionService ?? throw new ArgumentNullException(nameof(fusionService));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a document through the full pipeline.
    /// </summary>
    public async Task ProcessDocumentAsync(
        DocumentDownloadedEvent downloadEvent,
        CancellationToken cancellationToken = default)
    {
        var fileId = downloadEvent.FileId;
        var correlationId = downloadEvent.CorrelationId;

        _logger.LogInformation(
            "Starting document processing. FileId: {FileId}, CorrelationId: {CorrelationId}",
            fileId, correlationId);

        try
        {
            // Step 1: Quality Analysis (use existing service!)
            var imageData = await LoadImageDataAsync(downloadEvent.Path, cancellationToken);
            var qualityResult = await _qualityAnalyzer.AnalyzeAsync(imageData);

            if (!qualityResult.IsSuccess)
            {
                _logger.LogWarning("Quality analysis failed: {Error}", qualityResult.Error);
                _eventPublisher.Publish(new ProcessingErrorEvent
                {
                    FileId = fileId,
                    Component = "Quality",
                    ErrorMessage = qualityResult.Error,
                    CorrelationId = correlationId
                });
                return; // Stop pipeline gracefully
            }

            // Check quality threshold
            if (qualityResult.Value.QualityLevel == ImageQualityLevel.Poor)
            {
                _logger.LogInformation("Quality rejected for FileId: {FileId}", fileId);
                _eventPublisher.Publish(new QualityRejectedEvent
                {
                    FileId = fileId,
                    Score = qualityResult.Value.Confidence,
                    Reason = "Image quality below acceptable threshold",
                    CorrelationId = correlationId
                });
                return; // Stop pipeline
            }

            _eventPublisher.Publish(new QualityAnalysisCompletedEvent { /* ... */ });

            // Step 2: OCR (use existing service!)
            var ocrConfig = CreateOcrConfig(qualityResult.Value);
            var ocrResult = await _ocrExecutor.ExecuteOcrAsync(imageData, ocrConfig);

            if (!ocrResult.IsSuccess)
            {
                // Defensive - emit error, don't crash
                _eventPublisher.Publish(new ProcessingErrorEvent { Component = "OCR", ... });
                return;
            }

            _eventPublisher.Publish(new OcrCompletedEvent { /* ... */ });

            // Step 3: Fusion (use existing 100% complete service!)
            var fusionResult = await _fusionService.FuseAsync(/* ... */);
            _eventPublisher.Publish(new FusionCompletedEvent { /* ... */ });

            // Step 4: Classification (use existing service!)
            var classificationResult = await _classifier.ClassifyAsync(/* ... */);
            _eventPublisher.Publish(new ClassificationCompletedEvent { /* ... */ });

            // Step 5: Export (use existing service!)
            var exportResult = await _exporter.ExportAsync(/* ... */);
            _eventPublisher.Publish(new ExportCompletedEvent { /* ... */ });

            // Pipeline complete!
            _eventPublisher.Publish(new DocumentProcessingCompletedEvent
            {
                FileId = fileId,
                CorrelationId = correlationId,
                AutoProcessed = true
            });
        }
        catch (Exception ex)
        {
            // DEFENSIVE - NEVER CRASH
            _logger.LogError(ex, "Pipeline failed for FileId: {FileId}", fileId);
            _eventPublisher.Publish(new ProcessingErrorEvent
            {
                FileId = fileId,
                Component = "Pipeline",
                ErrorMessage = ex.Message,
                CorrelationId = correlationId
            });
        }
    }

    private async Task<ImageData> LoadImageDataAsync(string filePath, CancellationToken cancellationToken)
    {
        // Helper to load image from file
        // Implementation details...
    }

    private OCRConfig CreateOcrConfig(ImageQualityAssessment quality)
    {
        // Helper to create OCR config based on quality
        // Implementation details...
    }
}
```

---

## Dependencies & Project References

### Orchestration Project Dependencies

**DO NOT add Infrastructure references to orchestration projects!**

```xml
<!-- ✅ CORRECT - Prisma.Athena.Processing dependencies -->
<ItemGroup>
  <!-- Domain has ALL interfaces -->
  <ProjectReference Include="..\..\01-Core\Domain\ExxerCube.Prisma.Domain.csproj" />

  <!-- Events -->
  <ProjectReference Include="..\..\06-Shared\Prisma.Shared.Contracts\Prisma.Shared.Contracts.csproj" />

  <!-- Hosting abstractions -->
  <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
</ItemGroup>

<!-- ❌ WRONG - DO NOT REFERENCE INFRASTRUCTURE -->
<!-- <ProjectReference Include="..\..\02-Infrastructure\Infrastructure.Quality\..." /> -->
<!-- Infrastructure references belong in HOSTS, not orchestrators -->
```

### Worker Host Dependencies

**Hosts wire everything together:**

```xml
<!-- ✅ CORRECT - Prisma.Athena.Worker dependencies -->
<ItemGroup>
  <!-- Domain -->
  <ProjectReference Include="..\..\01-Core\Domain\ExxerCube.Prisma.Domain.csproj" />

  <!-- Orchestrator -->
  <ProjectReference Include="..\..\06-Athena\Prisma.Athena.Processing\Prisma.Athena.Processing.csproj" />

  <!-- ALL Infrastructure implementations -->
  <ProjectReference Include="..\..\02-Infrastructure\Infrastructure.Quality\..." />
  <ProjectReference Include="..\..\02-Infrastructure\Infrastructure.Ocr\..." />
  <ProjectReference Include="..\..\02-Infrastructure\Infrastructure.Classification\..." />
  <ProjectReference Include="..\..\02-Infrastructure\Infrastructure.Export\..." />
  <ProjectReference Include="..\..\02-Infrastructure\Infrastructure.Events\..." />

  <!-- Events -->
  <ProjectReference Include="..\..\06-Shared\Prisma.Shared.Contracts\..." />
</ItemGroup>
```

---

## Testing Standards

### Test Project Standards (MUST FOLLOW)

**Copy these settings from existing test projects:**

1. **Central Package Management:** `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
2. **Microsoft Testing Platform:** Full setup (see project template above)
3. **xUnit v3:** Latest version with proper runners
4. **Shouldly + NSubstitute:** For assertions and mocking
5. **Global Usings:** Include common namespaces
6. **TestContext.Current.CancellationToken:** Use for all async tests (xUnit v3 requirement)

### Test Naming Conventions

```csharp
// ✅ CORRECT - Descriptive test names
[Fact]
public async Task ProcessDocument_ExecutesFullPipeline()

[Fact]
public async Task ProcessDocument_PoorQuality_SkipsDownstreamPipeline()

[Fact]
public async Task ProcessDocument_ErrorInOcr_EmitsErrorEvent_DoesNotCrash()
```

### Test Structure (AAA Pattern)

```csharp
[Fact]
public async Task TestName()
{
    // Arrange - setup mocks and dependencies
    var service = Substitute.For<IService>();
    service.Method().Returns(expectedResult);

    var sut = new SystemUnderTest(service);

    // Act - execute the operation
    var result = await sut.DoSomethingAsync();

    // Assert - verify behavior
    result.ShouldNotBeNull();
    await service.Received(1).Method();
}
```

---

## Appendix: Stash Review & Valuable Content

### Stash Status

```bash
Stash: stash@{0} - "Stage 3 ITDD attempt - orchestration work (has errors, need cleanup)"
Status: 188 compilation errors (DO NOT APPLY AS-IS)
Valuable Content: Yes (events, event registrations, test structure ideas)
```

### Valuable Content from Stash

**1. New Events (ADD THESE):**

From `Domain/Events/ProcessingEvents.cs`:
```csharp
// ✅ These events are valuable - ADD to ProcessingEvents.cs
public record QualityRejectedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public decimal Score { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record FusionCompletedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public Guid ExpedienteId { get; init; }
}

public record ExportCompletedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public string Destination { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
}
```

**2. DomainEvent Registrations (ADD THESE):**

From `Domain/Events/DomainEvent.cs`:
```csharp
// ✅ Add these JSON serialization registrations
[JsonDerivedType(typeof(QualityRejectedEvent), "QualityRejectedEvent")]
[JsonDerivedType(typeof(FusionCompletedEvent), "FusionCompletedEvent")]
[JsonDerivedType(typeof(ExportCompletedEvent), "ExportCompletedEvent")]
```

**3. Test Structure Ideas:**

The test project setup and structure ideas are good (test names, AAA pattern), but:
- ❌ DO NOT use the duplicate coordinator interfaces
- ✅ DO use the test method signatures and structure
- ✅ DO use the xUnit v3 setup

### What to Cherry-Pick from Stash

**Recommended approach:**

1. **Manually add the 3 new events** (QualityRejectedEvent, FusionCompletedEvent, ExportCompletedEvent)
2. **Manually add JSON registrations** to DomainEvent.cs
3. **DO NOT apply the stash** directly (has too many errors)
4. **Reference test names** from stash for inspiration
5. **Rewrite ProcessingOrchestrator** from scratch using existing interfaces

**Command to view stash without applying:**
```bash
git stash show stash@{0} -p | less
```

---

## Quick Start Checklist for Stage 3

- [ ] 1. **Read this entire document** (especially "What to Use vs Avoid")
- [ ] 2. **Verify clean build** (`dotnet build ExxerCube.Prisma.sln`)
- [ ] 3. **Add 3 new events** from stash (QualityRejectedEvent, FusionCompletedEvent, ExportCompletedEvent)
- [ ] 4. **Add JSON registrations** to DomainEvent.cs
- [ ] 5. **Create test project** (Prisma.Athena.Processing.Tests)
- [ ] 6. **Write failing tests** (7 tests covering pipeline stages)
- [ ] 7. **Implement ProcessingOrchestrator** using ONLY existing interfaces
- [ ] 8. **Verify tests pass** (`dotnet test`)
- [ ] 9. **Verify build clean** (0 errors, 0 warnings)
- [ ] 10. **Commit** with message "feat: ITDD Stage 3 - Athena ProcessingOrchestrator (X/X tests passing)"

---

## Summary

**Current Status:**
- ✅ Stages 1-2 Complete (28/28 tests passing)
- ✅ Clean build (0 errors, 0 warnings)
- ⚠️ Stage 3 Ready (clean slate after failed attempt)

**Key Lessons:**
1. **Use existing Domain interfaces** - do NOT create coordinator wrappers
2. **Orchestration = Plumbing** - coordinate, don't reimplement
3. **Read before coding** - understand what exists first
4. **Keep build clean** - fix errors immediately

**Next Step:**
Implement Stage 3 (Athena ProcessingOrchestrator) following TDD with existing interfaces.

---

**Document Version:** 1.0
**Last Updated:** December 2, 2024
**Status:** Consolidated Guide (Replaces 3 separate documents)
**Git Commit:** `ecf7de3` (last clean state)

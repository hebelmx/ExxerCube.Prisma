# ADR-002 Remediation Guide: Detailed Step-by-Step Instructions

**Status:** 📋 **Ready for Implementation**  
**Date:** 2025-01-15  
**Related ADRs:**
- [ADR-002: Test Project Split - Clean Architecture Violations](./adr-002-test-project-split-clean-architecture-violations.md)
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)

---

## Purpose

This document provides **detailed, executable remediation instructions** for fixing the 9 clean architecture violations identified in ADR-002. Each violation includes:

1. **Current State Analysis** - What's wrong and why
2. **Target State** - What the correct implementation should look like
3. **Step-by-Step Refactoring Instructions** - Exact code changes needed
4. **Code Examples** - Before/after comparisons
5. **Validation Steps** - How to verify the fix is correct

---

## Violation Summary

| # | Test Class | Location | Violation Type | Priority | Estimated Effort |
|---|------------|----------|----------------|----------|------------------|
| 1 | `DecisionLogicIntegrationTests` | Tests.Application | Instantiates Infrastructure.Classification | High | 2 hours |
| 2 | `MetadataExtractionIntegrationTests` | Tests.Application | Instantiates Infrastructure.Extraction | High | 4 hours |
| 3 | `MetadataExtractionPerformanceTests` | Tests.Application | Instantiates Infrastructure.Extraction | High | 4 hours |
| 4 | `DocumentIngestionIntegrationTests` | Tests.Application | Instantiates Infrastructure.Database + FileStorage | High | 3 hours |
| 5 | `FieldMatchingServiceTests` | Tests.Application | Instantiates Infrastructure.Classification | High | 2 hours |
| 6 | `FieldMatchingPerformanceTests` | Tests.Application | Instantiates Infrastructure.Classification | High | 2 hours |
| 7 | `FieldMatchingIntegrationTests` | Tests.Application | Instantiates Infrastructure.Classification | High | 2 hours |
| 8 | `AuditLoggerIntegrationTests` | Tests.Infrastructure.Database | Instantiates Application services | Medium | 2 hours |
| 9 | `ExportIntegrationTests` | Tests.Infrastructure.Export | Instantiates Infrastructure.Extraction | Medium | 2 hours |

**Total Estimated Effort:** ~23 hours

---

## Phase 1: Application Test Refactoring (Priority: High)

### Violation 1: DecisionLogicIntegrationTests

**File:** `Prisma/Code/Src/CSharp/Tests.Application/Services/DecisionLogicIntegrationTests.cs`

#### Current State Analysis

**Problem:**
- Constructor instantiates `PersonIdentityResolverService` and `LegalDirectiveClassifierService` (Infrastructure.Classification)
- These are concrete Infrastructure implementations, violating dependency inversion principle
- Test project `Tests.Application` cannot reference `Infrastructure.Classification` project

**Current Code (Lines 25-26):**
```csharp
_identityResolver = new PersonIdentityResolverService(identityLogger);
_classifier = new LegalDirectiveClassifierService(classifierLogger);
```

#### Target State

**Solution:**
- Use NSubstitute mocks of Domain interfaces: `IPersonIdentityResolver` and `ILegalDirectiveClassifier`
- Configure mock behavior to return appropriate test data
- Remove all Infrastructure.Classification project references

#### Step-by-Step Refactoring Instructions

1. **Remove Infrastructure using statements:**
   ```csharp
   // REMOVE these lines if present:
   // using ExxerCube.Prisma.Infrastructure.Classification;
   ```

2. **Update constructor to use mocks:**
   ```csharp
   public DecisionLogicIntegrationTests()
   {
       // Create mocks of Domain interfaces
       _identityResolver = Substitute.For<IPersonIdentityResolver>();
       _classifier = Substitute.For<ILegalDirectiveClassifier>();
       var serviceLogger = Substitute.For<ILogger<DecisionLogicService>>();
       var auditLogger = Substitute.For<IAuditLogger>();
       var manualReviewerPanel = Substitute.For<IManualReviewerPanel>();

       _service = new DecisionLogicService(_identityResolver, _classifier, manualReviewerPanel, auditLogger, serviceLogger);
   }
   ```

3. **Configure mock behavior in test methods:**
   
   For `ProcessDecisionLogicAsync_EndToEndWorkflow_CompletesSuccessfully`:
   ```csharp
   [Fact]
   public async Task ProcessDecisionLogicAsync_EndToEndWorkflow_CompletesSuccessfully()
   {
       // Arrange
       var persons = new List<Persona> { /* ... existing test data ... */ };
       
       // Configure mock to return resolved persons
       _identityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
           .Returns(callInfo => 
           {
               var person = callInfo.Arg<Persona>();
               // Return person with RFC variants added
               person.RfcVariants = new List<string> { person.Rfc, person.Rfc.Replace("-", "") };
               return Result<Persona>.Success(person);
           });
       
       _identityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
           .Returns(callInfo => 
           {
               var persons = callInfo.Arg<List<Persona>>();
               return Result<List<Persona>>.Success(persons);
           });
       
       // Configure classifier mock
       _classifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
           .Returns(Result<List<ComplianceAction>>.Success(new List<ComplianceAction>
           {
               new ComplianceAction 
               { 
                   ActionType = ComplianceActionType.Block,
                   Confidence = 95,
                   AccountNumber = "1234567890",
                   Amount = 1000000.00m
               },
               new ComplianceAction 
               { 
                   ActionType = ComplianceActionType.Document,
                   Confidence = 90
               }
           }));
       
       _classifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<List<string>>.Success(new List<string> { "Acuerdo 105/2021" }));
       
       // Act
       var result = await _service.ProcessDecisionLogicAsync(persons, documentText, expediente, cancellationToken: TestContext.Current.CancellationToken);
       
       // Assert
       result.IsSuccess.ShouldBeTrue();
       // ... rest of assertions ...
   }
   ```

4. **Update all test methods** to configure mocks appropriately:
   - `ResolvePersonIdentitiesAsync_DoesNotModifyExistingStructures_IV1` - Mock `ResolveIdentityAsync` and `DeduplicatePersonsAsync`
   - `ClassifyLegalDirectivesAsync_UsesExtractedMetadata_IV2` - Mock `ClassifyDirectivesAsync`
   - `ClassifyLegalDirectivesAsync_PerformanceWithinTarget_IV3` - Mock `ClassifyDirectivesAsync` with fast response
   - `ResolvePersonIdentitiesAsync_WithRfcVariants_HandlesVariants` - Mock `ResolveIdentityAsync` to return RFC variants
   - `ClassifyLegalDirectivesAsync_WithLegalInstruments_DetectsInstruments` - Mock `DetectLegalInstrumentsAsync` and `ClassifyDirectivesAsync`
   - `ClassifyLegalDirectivesAsync_MapsToComplianceActions_WithConfidenceScores` - Mock `ClassifyDirectivesAsync` with confidence scores
   - `ProcessDecisionLogicAsync_LogsAllDecisions_AC6` - Mock all methods

5. **Remove the throw statement** from constructor (lines 27-31)

6. **Verify project dependencies:**
   - Open `Tests.Application/ExxerCube.Prisma.Tests.Application.csproj`
   - Ensure there is NO reference to `Infrastructure.Classification` project
   - If present, remove it

#### Validation Steps

1. **Compile the test project:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj
   ```
   - Should compile without errors
   - No references to Infrastructure.Classification types

2. **Run the tests:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj --filter "FullyQualifiedName~DecisionLogicIntegrationTests"
   ```
   - All tests should pass
   - No InvalidOperationException thrown

3. **Verify no Infrastructure references:**
   ```bash
   grep -r "PersonIdentityResolverService\|LegalDirectiveClassifierService" Prisma/Code/Src/CSharp/Tests.Application/
   ```
   - Should return no results (except in comments/documentation)

---

### Violation 2: MetadataExtractionIntegrationTests

**File:** `Prisma/Code/Src/CSharp/Tests.Application/Services/MetadataExtractionIntegrationTests.cs`

#### Current State Analysis

**Problem:**
- Constructor instantiates multiple Infrastructure.Extraction types:
  - `FileTypeIdentifierService`
  - `XmlMetadataExtractor`
  - `DocxMetadataExtractor`
  - `PdfMetadataExtractor`
  - `CompositeMetadataExtractor`
- Also instantiates Infrastructure.Classification and Infrastructure.FileStorage:
  - `FileClassifierService`
  - `SafeFileNamerService`
  - `FileMoverService`
- Test project `Tests.Application` cannot reference Infrastructure projects

#### Target State

**Solution:**
- Mock all Domain interfaces: `IFileTypeIdentifier`, `IMetadataExtractor`, `IFileClassifier`, `ISafeFileNamer`, `IFileMover`
- Configure mocks to return appropriate test data based on test scenarios
- Remove all Infrastructure project references

#### Step-by-Step Refactoring Instructions

1. **Remove Infrastructure using statements:**
   ```csharp
   // REMOVE these lines if present:
   // using ExxerCube.Prisma.Infrastructure.Extraction;
   // using ExxerCube.Prisma.Infrastructure.Classification;
   // using ExxerCube.Prisma.Infrastructure.FileStorage;
   ```

2. **Update field declarations** (lines 20-25):
   ```csharp
   private readonly IFileTypeIdentifier _fileTypeIdentifier;
   private readonly IMetadataExtractor _metadataExtractor;
   private readonly IFileClassifier _fileClassifier;
   private readonly ISafeFileNamer _safeFileNamer;
   private readonly IFileMover _fileMover;
   ```

3. **Update constructor** (lines 41-100):
   ```csharp
   public MetadataExtractionIntegrationTests(ITestOutputHelper output)
   {
       _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
       Directory.CreateDirectory(_tempDirectory);

       // Create mocks of Domain interfaces
       _fileTypeIdentifier = Substitute.For<IFileTypeIdentifier>();
       _metadataExtractor = Substitute.For<IMetadataExtractor>();
       _fileClassifier = Substitute.For<IFileClassifier>();
       _safeFileNamer = Substitute.For<ISafeFileNamer>();
       _fileMover = Substitute.For<IFileMover>();
       
       var serviceLogger = XUnitLogger.CreateLogger<MetadataExtractionService>(output);
       var auditLogger = Substitute.For<IAuditLogger>();

       _service = new MetadataExtractionService(
           _fileTypeIdentifier,
           _metadataExtractor,
           _fileClassifier,
           _safeFileNamer,
           _fileMover,
           auditLogger,
           serviceLogger);
   }
   ```

4. **Update test method `ProcessFileAsync_XmlDocument_CompletesWorkflow`** (lines 106-126):
   ```csharp
   [Fact]
   public async Task ProcessFileAsync_XmlDocument_CompletesWorkflow()
   {
       // Arrange
       var xmlContent = @"<?xml version=""1.0""?>
       <Expediente>
           <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
           <NumeroOficio>214-1-18714972/2025</NumeroOficio>
           <AreaDescripcion>ASEGURAMIENTO</AreaDescripcion>
           <SolicitudPartes>
               <Parte>
                   <ParteId>1</ParteId>
                   <Caracter>Contribuyente</Caracter>
                   <PersonaTipo>Fisica</PersonaTipo>
                   <Nombre>Juan</Nombre>
                   <Paterno>Perez</Paterno>
                   <Rfc>PERJ800101ABC</Rfc>
               </Parte>
           </SolicitudPartes>
       </Expediente>";
       var testFile = Path.Combine(_tempDirectory, "test_expediente.xml");
       await File.WriteAllTextAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

       // Configure mocks for XML processing workflow
       _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<FileType>.Success(FileType.Xml));
       
       _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<ExtractedMetadata>.Success(new ExtractedMetadata
           {
               Expediente = new Expediente
               {
                   NumeroExpediente = "A/AS1-2505-088637-PHM",
                   NumeroOficio = "214-1-18714972/2025",
                   AreaDescripcion = "ASEGURAMIENTO"
               },
               Persons = new List<Persona>
               {
                   new Persona
                   {
                       ParteId = 1,
                       Nombre = "Juan",
                       Paterno = "Perez",
                       Rfc = "PERJ800101ABC"
                   }
               }
           }));
       
       _fileClassifier.ClassifyLevel1Async(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
           .Returns(Result<ClassificationLevel1>.Success(ClassificationLevel1.Aseguramiento));
       
       _fileClassifier.ClassifyLevel2Async(Arg.Any<ExtractedMetadata>(), Arg.Any<ClassificationLevel1>(), Arg.Any<CancellationToken>())
           .Returns(Result<ClassificationLevel2>.Success(ClassificationLevel2.Especial));
       
       _safeFileNamer.GenerateSafeFileNameAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<string>.Success("A-AS1-2505-088637-PHM.xml"));
       
       _fileMover.MoveFileAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<string>.Success(Path.Combine(_tempDirectory, "A-AS1-2505-088637-PHM.xml")));

       // Act
       var result = await _service.ProcessFileAsync(testFile, "test_expediente.xml", cancellationToken: TestContext.Current.CancellationToken);

       // Assert
       result.IsSuccess.ShouldBeTrue();
       result.Value.ShouldNotBeNull();
       result.Value.Expediente.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
       // ... rest of assertions ...
   }
   ```

5. **Update all other test methods** similarly:
   - Configure mocks based on test scenario
   - Remove real Infrastructure instantiation
   - Verify mock interactions if needed

6. **Remove the throw statement** from constructor (lines 43-47)

#### Validation Steps

1. **Compile the test project:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj
   ```

2. **Run the tests:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj --filter "FullyQualifiedName~MetadataExtractionIntegrationTests"
   ```

3. **Verify no Infrastructure references:**
   ```bash
   grep -r "FileTypeIdentifierService\|XmlMetadataExtractor\|DocxMetadataExtractor\|PdfMetadataExtractor\|CompositeMetadataExtractor\|FileClassifierService\|SafeFileNamerService\|FileMoverService" Prisma/Code/Src/CSharp/Tests.Application/
   ```

---

### Violation 3: MetadataExtractionPerformanceTests

**File:** `Prisma/Code/Src/CSharp/Tests.Application/Services/MetadataExtractionPerformanceTests.cs`

#### Current State Analysis

**Problem:**
- Same as Violation 2 - instantiates Infrastructure.Extraction, Classification, and FileStorage types
- Performance tests should still use mocks, but configure them to return quickly

#### Target State

**Solution:**
- Use mocks like Violation 2
- Configure mocks to return immediately (no delays) for performance testing
- Performance tests verify the Application service logic, not Infrastructure performance

#### Step-by-Step Refactoring Instructions

1. **Follow the same steps as Violation 2** (remove Infrastructure references, use mocks)

2. **Update constructor** similar to Violation 2:
   ```csharp
   public MetadataExtractionPerformanceTests(ITestOutputHelper output)
   {
       _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
       Directory.CreateDirectory(_tempDirectory);

       // Create mocks - performance tests verify Application layer, not Infrastructure
       var fileTypeIdentifier = Substitute.For<IFileTypeIdentifier>();
       var metadataExtractor = Substitute.For<IMetadataExtractor>();
       var fileClassifier = Substitute.For<IFileClassifier>();
       var safeFileNamer = Substitute.For<ISafeFileNamer>();
       var fileMover = Substitute.For<IFileMover>();
       
       var serviceLogger = XUnitLogger.CreateLogger<MetadataExtractionService>(output);
       var auditLogger = Substitute.For<IAuditLogger>();

       _service = new MetadataExtractionService(
           fileTypeIdentifier,
           metadataExtractor,
           fileClassifier,
           safeFileNamer,
           fileMover,
           auditLogger,
           serviceLogger);
   }
   ```

3. **Configure mocks in test methods** to return immediately:
   ```csharp
   [Fact]
   [Trait("Category", "Performance")]
   public async Task ProcessFileAsync_XmlFile_CompletesWithin2Seconds()
   {
       // Arrange
       var xmlContent = @"<?xml version=""1.0""?><Expediente><NumeroExpediente>TEST-001</NumeroExpediente></Expediente>";
       var testFile = Path.Combine(_tempDirectory, "test.xml");
       await File.WriteAllTextAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

       // Configure mocks to return immediately (no delays)
       _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<FileType>.Success(FileType.Xml));
       
       _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<ExtractedMetadata>.Success(new ExtractedMetadata { /* ... */ }));
       
       // ... configure other mocks ...

       // Act
       var stopwatch = Stopwatch.StartNew();
       var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);
       stopwatch.Stop();

       // Assert
       result.IsSuccess.ShouldBeTrue();
       stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);
   }
   ```

**Note:** Performance tests verify Application service orchestration performance, not Infrastructure performance. Infrastructure performance should be tested in `Tests.Infrastructure.*` projects.

#### Validation Steps

Same as Violation 2.

---

### Violation 4: DocumentIngestionIntegrationTests

**File:** `Prisma/Code/Src/CSharp/Tests.Application/Services/DocumentIngestionIntegrationTests.cs`

#### Current State Analysis

**Problem:**
- Constructor instantiates `PrismaDbContext`, `DownloadTrackerService`, `FileMetadataLoggerService`, `FileSystemDownloadStorageAdapter`
- These are Infrastructure.Database and Infrastructure.FileStorage types
- Test project `Tests.Application` cannot reference Infrastructure projects

#### Target State

**Solution:**
- Mock Domain interfaces: `IDownloadTracker`, `IFileMetadataLogger`, `IDownloadStorage`
- Remove `PrismaDbContext` usage (it's Infrastructure)
- Configure mocks to simulate database and file storage behavior

#### Step-by-Step Refactoring Instructions

1. **Remove Infrastructure using statements:**
   ```csharp
   // REMOVE these lines if present:
   // using ExxerCube.Prisma.Infrastructure.Database;
   // using ExxerCube.Prisma.Infrastructure.FileStorage;
   // using Microsoft.EntityFrameworkCore;
   ```

2. **Update field declarations:**
   ```csharp
   private readonly string _tempDirectory;
   private readonly IDownloadTracker _downloadTracker;
   private readonly IFileMetadataLogger _fileMetadataLogger;
   private readonly IDownloadStorage _downloadStorage;
   private readonly IBrowserAutomationAgent _browserAutomationAgent;
   private readonly DocumentIngestionService _service;
   ```

3. **Update constructor** (lines 31-70):
   ```csharp
   public DocumentIngestionIntegrationTests(ITestOutputHelper output)
   {
       _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
       Directory.CreateDirectory(_tempDirectory);

       var serviceLogger = XUnitLogger.CreateLogger<DocumentIngestionService>(output);

       // Create mocks of Domain interfaces
       _downloadTracker = Substitute.For<IDownloadTracker>();
       _fileMetadataLogger = Substitute.For<IFileMetadataLogger>();
       _downloadStorage = Substitute.For<IDownloadStorage>();
       _browserAutomationAgent = Substitute.For<IBrowserAutomationAgent>();
       var auditLogger = Substitute.For<IAuditLogger>();

       _service = new DocumentIngestionService(
           _browserAutomationAgent,
           _downloadTracker,
           _downloadStorage,
           _fileMetadataLogger,
           auditLogger,
           serviceLogger);
   }
   ```

4. **Update test method `IngestDocumentsAsync_NewDocument_CompletesSuccessfully`**:
   ```csharp
   [Fact]
   public async Task IngestDocumentsAsync_NewDocument_CompletesSuccessfully()
   {
       // Arrange
       var websiteUrl = "https://example.com";
       var downloadableFile = new DownloadableFile
       {
           FileName = "test.pdf",
           Url = "https://example.com/test.pdf",
           Size = 1024
       };

       // Configure browser automation mock
       _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<string>(), Arg.Any<BrowserOptions>(), Arg.Any<CancellationToken>())
           .Returns(Result<string>.Success("session-123"));
       
       _browserAutomationAgent.IdentifyDownloadableFilesAsync(Arg.Any<string>(), Arg.Any<FilePattern[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
       
       _browserAutomationAgent.DownloadFileAsync(Arg.Any<string>(), Arg.Any<DownloadableFile>(), Arg.Any<CancellationToken>())
           .Returns(Result<DownloadedFile>.Success(new DownloadedFile
           {
               FileName = "test.pdf",
               Content = new byte[] { 1, 2, 3, 4 },
               Checksum = "abc123"
           }));

       // Configure download tracker mock
       _downloadTracker.IsDuplicateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<bool>.Success(false)); // File not duplicate
       
       _downloadTracker.GetFileMetadataByChecksumAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<FileMetadata?>.Success(null)); // File not found

       // Configure download storage mock
       _downloadStorage.SaveFileAsync(Arg.Any<DownloadedFile>(), Arg.Any<string>(), Arg.Any<FileNamingStrategy>(), Arg.Any<CancellationToken>())
           .Returns(Result<string>.Success(Path.Combine(_tempDirectory, "test.pdf")));

       // Configure file metadata logger mock
       _fileMetadataLogger.LogMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<CancellationToken>())
           .Returns(Result.Success());

       // Act
       var result = await _service.IngestDocumentsAsync(websiteUrl, cancellationToken: TestContext.Current.CancellationToken);

       // Assert
       result.IsSuccess.ShouldBeTrue();
       // ... rest of assertions ...
   }
   ```

5. **Update all other test methods** similarly

6. **Remove the throw statement** from constructor (lines 33-37)

7. **Remove `_dbContext` field** and all `PrismaDbContext` usage

#### Validation Steps

1. **Compile the test project:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj
   ```

2. **Run the tests:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj --filter "FullyQualifiedName~DocumentIngestionIntegrationTests"
   ```

3. **Verify no Infrastructure references:**
   ```bash
   grep -r "PrismaDbContext\|DownloadTrackerService\|FileMetadataLoggerService\|FileSystemDownloadStorageAdapter" Prisma/Code/Src/CSharp/Tests.Application/
   ```

---

### Violations 5-7: FieldMatchingServiceTests, FieldMatchingPerformanceTests, FieldMatchingIntegrationTests

**Files:**
- `Prisma/Code/Src/CSharp/Tests.Application/Services/FieldMatchingServiceTests.cs`
- `Prisma/Code/Src/CSharp/Tests.Application/Services/FieldMatchingPerformanceTests.cs`
- `Prisma/Code/Src/CSharp/Tests.Application/Services/FieldMatchingIntegrationTests.cs`

#### Current State Analysis

**Problem:**
- All three test classes instantiate `MatchingPolicyService` (Infrastructure.Classification)
- Line 35 in each: `_matchingPolicy = new MatchingPolicyService(options, Substitute.For<ILogger<MatchingPolicyService>>());`

#### Target State

**Solution:**
- Mock `IMatchingPolicy` interface instead of instantiating `MatchingPolicyService`
- Configure mock behavior based on test requirements

#### Step-by-Step Refactoring Instructions

1. **Remove Infrastructure using statements:**
   ```csharp
   // REMOVE these lines if present:
   // using ExxerCube.Prisma.Infrastructure.Classification;
   // using Microsoft.Extensions.Options;
   ```

2. **Update constructor** in all three files:
   ```csharp
   public FieldMatchingServiceTests() // or FieldMatchingPerformanceTests, FieldMatchingIntegrationTests
   {
       _docxFieldExtractor = Substitute.For<IFieldExtractor<DocxSource>>();
       _pdfFieldExtractor = Substitute.For<IFieldExtractor<PdfSource>>();
       _logger = Substitute.For<ILogger<FieldMatchingService>>();
       
       // Use mock instead of concrete implementation
       _matchingPolicy = Substitute.For<IMatchingPolicy>();
       
       _service = new FieldMatchingService(
           _docxFieldExtractor,
           _pdfFieldExtractor,
           null, // XML extractor optional
           _matchingPolicy,
           _logger);
   }
   ```

3. **Configure mock behavior in test methods** as needed:
   ```csharp
   [Fact]
   public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithMultipleSources_ReturnsUnifiedRecord()
   {
       // Arrange
       var fieldDefinitions = new[] { new FieldDefinition("Expediente") };
       
       // Configure field extractors
       _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP-001" }));
       
       _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>(), Arg.Any<CancellationToken>())
           .Returns(Result<ExtractedFields>.Success(new ExtractedFields { Expediente = "EXP-001" }));
       
       // Configure matching policy mock
       _matchingPolicy.SelectBestValueAsync(Arg.Any<string>(), Arg.Any<List<FieldValue>>())
           .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("Expediente", "EXP-001", 1.0f, "CONSENSUS")));
       
       _matchingPolicy.CalculateAgreementLevelAsync(Arg.Any<List<FieldValue>>())
           .Returns(Result<float>.Success(1.0f));
       
       _matchingPolicy.HasConflictAsync(Arg.Any<List<FieldValue>>(), Arg.Any<float>())
           .Returns(Result<bool>.Success(false));

       // Act
       var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
           new DocxSource("test.docx"),
           new PdfSource("test.pdf"),
           null,
           fieldDefinitions,
           expediente: null,
           classification: null,
           requiredFields: null,
           TestContext.Current.CancellationToken);

       // Assert
       result.IsSuccess.ShouldBeTrue();
       // ... rest of assertions ...
   }
   ```

4. **Remove the throw statement** from constructors

5. **Remove `MatchingPolicyOptions` usage** - not needed with mocks

#### Validation Steps

1. **Compile the test project:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj
   ```

2. **Run the tests:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj --filter "FullyQualifiedName~FieldMatching"
   ```

3. **Verify no Infrastructure references:**
   ```bash
   grep -r "MatchingPolicyService" Prisma/Code/Src/CSharp/Tests.Application/
   ```

---

## Phase 2: Infrastructure Test Refactoring (Priority: Medium)

### Violation 8: AuditLoggerIntegrationTests

**File:** `Prisma/Code/Src/CSharp/Tests.Infrastructure.Database/AuditLoggerIntegrationTests.cs`

#### Current State Analysis

**Problem:**
- Test instantiates Application services: `DocumentIngestionService`, `MetadataExtractionService`, `DecisionLogicService`, `ExportService`
- Infrastructure tests should not depend on Application layer
- Test is verifying audit logging integration, but should test Infrastructure in isolation

#### Target State

**Solution:**
- **Option A (Recommended):** Move test to `Tests.Application` since it tests Application service integration with audit logging
- **Option B:** Mock Application service interfaces (if they exist) or refactor to test `AuditLoggerService` directly

#### Step-by-Step Refactoring Instructions (Option A - Recommended)

1. **Move file to Application test project:**
   ```bash
   # Move the file
   mv Prisma/Code/Src/CSharp/Tests.Infrastructure.Database/AuditLoggerIntegrationTests.cs \
      Prisma/Code/Src/CSharp/Tests.Application/Services/AuditLoggerIntegrationTests.cs
   ```

2. **Update namespace:**
   ```csharp
   namespace ExxerCube.Prisma.Tests.Application.Services;
   ```

3. **Update project file:**
   - Remove from `Tests.Infrastructure.Database.csproj`
   - Ensure it's included in `Tests.Application.csproj` (should be automatic)

4. **Update test class** - Application tests can instantiate Application services:
   ```csharp
   public class AuditLoggerIntegrationTests : IDisposable
   {
       private readonly PrismaDbContext _dbContext;
       private readonly ILogger<AuditLoggerService> _logger;
       private readonly AuditLoggerService _auditLogger;
       private readonly DocumentIngestionService _documentIngestionService;
       private readonly MetadataExtractionService _metadataExtractionService;
       private readonly DecisionLogicService _decisionLogicService;
       private readonly ExportService _exportService;

       public AuditLoggerIntegrationTests(ITestOutputHelper output)
       {
           // Database context is OK - it's Infrastructure, but we're testing Application integration
           var options = new DbContextOptionsBuilder<PrismaDbContext>()
               .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
               .Options;

           _dbContext = new PrismaDbContext(options);
           _dbContext.Database.EnsureCreated();
           _logger = XUnitLogger.CreateLogger<AuditLoggerService>(output);
           _auditLogger = new AuditLoggerService(_dbContext, _logger);

           // Create Application services with mocked dependencies
           var ingestionLogger = XUnitLogger.CreateLogger<DocumentIngestionService>(output);
           var extractionLogger = XUnitLogger.CreateLogger<MetadataExtractionService>(output);
           var decisionLogger = XUnitLogger.CreateLogger<DecisionLogicService>(output);
           var exportLogger = XUnitLogger.CreateLogger<ExportService>(output);

           // Mock all Infrastructure dependencies
           var browserAgent = Substitute.For<IBrowserAutomationAgent>();
           var downloadTracker = Substitute.For<IDownloadTracker>();
           var downloadStorage = Substitute.For<IDownloadStorage>();
           var fileMetadataLogger = Substitute.For<IFileMetadataLogger>();
           var fileTypeIdentifier = Substitute.For<IFileTypeIdentifier>();
           var metadataExtractor = Substitute.For<IMetadataExtractor>();
           var fileClassifier = Substitute.For<IFileClassifier>();
           var safeFileNamer = Substitute.For<ISafeFileNamer>();
           var fileMover = Substitute.For<IFileMover>();
           var personIdentityResolver = Substitute.For<IPersonIdentityResolver>();
           var legalDirectiveClassifier = Substitute.For<ILegalDirectiveClassifier>();
           var manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
           var responseExporter = Substitute.For<IResponseExporter>();
           var layoutGenerator = Substitute.For<ILayoutGenerator>();
           var criterionMapper = Substitute.For<ICriterionMapper>();
           var pdfRequirementSummarizer = Substitute.For<IPdfRequirementSummarizer>();

           _documentIngestionService = new DocumentIngestionService(
               browserAgent,
               downloadTracker,
               downloadStorage,
               fileMetadataLogger,
               _auditLogger,
               ingestionLogger);

           _metadataExtractionService = new MetadataExtractionService(
               fileTypeIdentifier,
               metadataExtractor,
               fileClassifier,
               safeFileNamer,
               fileMover,
               _auditLogger,
               extractionLogger);

           _decisionLogicService = new DecisionLogicService(
               personIdentityResolver,
               legalDirectiveClassifier,
               manualReviewerPanel,
               _auditLogger,
               decisionLogger);

           _exportService = new ExportService(
               responseExporter,
               layoutGenerator,
               criterionMapper,
               pdfRequirementSummarizer,
               _auditLogger,
               exportLogger);
       }
       
       // ... rest of test methods remain the same ...
   }
   ```

5. **Remove the throw statement** from constructor (lines 32-36)

#### Validation Steps

1. **Verify file moved:**
   ```bash
   ls Prisma/Code/Src/CSharp/Tests.Application/Services/AuditLoggerIntegrationTests.cs
   ```

2. **Compile both projects:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj
   dotnet build Prisma/Code/Src/CSharp/Tests.Infrastructure.Database/ExxerCube.Prisma.Tests.Infrastructure.Database.csproj
   ```

3. **Run the tests:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj --filter "FullyQualifiedName~AuditLoggerIntegrationTests"
   ```

---

### Violation 9: ExportIntegrationTests

**File:** `Prisma/Code/Src/CSharp/Tests.Infrastructure.Export/ExportIntegrationTests.cs`

#### Current State Analysis

**Problem:**
- Test instantiates `CompositeMetadataExtractor`, `XmlMetadataExtractor`, `DocxMetadataExtractor`, `PdfMetadataExtractor` (Infrastructure.Extraction)
- Infrastructure.Export tests should not depend on Infrastructure.Extraction
- Violates Infrastructure independence principle

#### Target State

**Solution:**
- **Option A (Recommended):** Move test to `Tests.Infrastructure.Extraction` since it tests extraction integration
- **Option B:** Mock `IMetadataExtractor` interface

#### Step-by-Step Refactoring Instructions (Option A - Recommended)

1. **Move file to Extraction test project:**
   ```bash
   # Move the file
   mv Prisma/Code/Src/CSharp/Tests.Infrastructure.Export/ExportIntegrationTests.cs \
      Prisma/Code/Src/CSharp/Tests.Infrastructure.Extraction/ExportIntegrationTests.cs
   ```

2. **Update namespace:**
   ```csharp
   namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;
   ```

3. **Update project file:**
   - Remove from `Tests.Infrastructure.Export.csproj`
   - Add to `Tests.Infrastructure.Extraction.csproj`

4. **Rename test class** to reflect it's testing extraction, not export:
   ```csharp
   /// <summary>
   /// Integration tests for PDF summarization workflow using metadata extraction.
   /// Tests the integration between PdfRequirementSummarizerService and metadata extractors.
   /// </summary>
   public class PdfSummarizationIntegrationTests
   {
       // ... test implementation ...
   }
   ```

5. **Remove the throw statement** from constructor (lines 20-24)

#### Alternative: Option B - Mock IMetadataExtractor

If keeping the test in Export project:

1. **Update constructor:**
   ```csharp
   public ExportIntegrationTests()
   {
       // Mock IMetadataExtractor instead of instantiating concrete types
       var metadataExtractor = Substitute.For<IMetadataExtractor>();
       
       var logger = XUnitLogger.CreateLogger<PdfRequirementSummarizerService>();
       var summarizer = new PdfRequirementSummarizerService(metadataExtractor, logger);
       
       // Configure mock in test methods as needed
   }
   ```

#### Validation Steps

1. **Verify file moved (if Option A):**
   ```bash
   ls Prisma/Code/Src/CSharp/Tests.Infrastructure.Extraction/ExportIntegrationTests.cs
   ```

2. **Compile projects:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/Tests.Infrastructure.Export/ExxerCube.Prisma.Tests.Infrastructure.Export.csproj
   dotnet build Prisma/Code/Src/CSharp/Tests.Infrastructure.Extraction/ExxerCube.Prisma.Tests.Infrastructure.Extraction.csproj
   ```

3. **Run the tests:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Infrastructure.Extraction/ExxerCube.Prisma.Tests.Infrastructure.Extraction.csproj --filter "FullyQualifiedName~ExportIntegrationTests"
   ```

---

## Phase 3: Cleanup (Priority: Low)

### Delete Monolithic Tests Project

**Prerequisite:** All violations resolved, all tests migrated

#### Step-by-Step Instructions

1. **Verify all tests pass:**
   ```bash
   dotnet test Prisma/Code/Src/CSharp/Tests.Application/
   dotnet test Prisma/Code/Src/CSharp/Tests.Infrastructure.*/
   dotnet test Prisma/Code/Src/CSharp/Tests.EndToEnd/
   ```

2. **Verify no references to monolithic project:**
   ```bash
   grep -r "ExxerCube.Prisma.Tests" Prisma/Code/Src/CSharp/*.csproj
   # Should not find references to old Tests project
   ```

3. **Remove project from solution:**
   ```bash
   dotnet sln remove Prisma/Code/Src/CSharp/Tests/ExxerCube.Prisma.Tests.csproj
   ```

4. **Delete project directory:**
   ```bash
   rm -rf Prisma/Code/Src/CSharp/Tests/
   ```

5. **Verify solution builds:**
   ```bash
   dotnet build Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln
   ```

---

## General Refactoring Principles

### 1. Always Mock Domain Interfaces, Never Infrastructure Implementations

**❌ Wrong:**
```csharp
var resolver = new PersonIdentityResolverService(logger);
```

**✅ Correct:**
```csharp
var resolver = Substitute.For<IPersonIdentityResolver>();
```

### 2. Configure Mocks Based on Test Scenarios

**Example:**
```csharp
// Configure mock to return success
_mockService.ProcessAsync(Arg.Any<Input>(), Arg.Any<CancellationToken>())
    .Returns(Result<Output>.Success(new Output { /* ... */ }));

// Configure mock to return failure
_mockService.ProcessAsync(Arg.Any<Input>(), Arg.Any<CancellationToken>())
    .Returns(Result<Output>.WithFailure("Error message"));
```

### 3. Verify Mock Interactions When Needed

**Example:**
```csharp
// Verify mock was called
await _mockService.Received(1).ProcessAsync(Arg.Any<Input>(), Arg.Any<CancellationToken>());

// Verify mock was called with specific arguments
await _mockService.Received(1).ProcessAsync(
    Arg.Is<Input>(x => x.Id == expectedId),
    Arg.Any<CancellationToken>());
```

### 4. Remove All Infrastructure Project References

After refactoring, verify no Infrastructure project references exist in `.csproj` files:

```bash
# Check for Infrastructure references in Application tests
grep -r "Infrastructure\." Prisma/Code/Src/CSharp/Tests.Application/*.csproj
# Should return no results
```

### 5. Keep Test Logic Focused on Application Layer

- **Application tests** should test Application service orchestration logic
- **Infrastructure tests** should test Infrastructure adapter implementations
- **E2E tests** can use real implementations (they're in `Tests.EndToEnd`)

---

## Validation Checklist

After completing all refactoring:

- [ ] All 9 violating test classes refactored
- [ ] All tests compile without errors
- [ ] All tests pass
- [ ] No Infrastructure project references in `Tests.Application` project file
- [ ] No Application project references in `Tests.Infrastructure.*` project files (except where allowed)
- [ ] All mock configurations are appropriate for test scenarios
- [ ] No `InvalidOperationException` thrown from constructors
- [ ] All `throw new InvalidOperationException(...)` statements removed
- [ ] Monolithic `Tests` project deleted (Phase 3)

---

## References

- [ADR-002: Test Project Split - Clean Architecture Violations](./adr-002-test-project-split-clean-architecture-violations.md)
- [ADR-003: Test Suite Split Decision](./adr-003-test-suite-split-decision.md)
- [Clean Architecture Patterns](../../.cursor/rules/1008_CleanArchitecturePatterns.mdc)
- [Domain-Driven Design Patterns](../../.cursor/rules/1007_DomainDrivenDesignPatterns.mdc)
- [C# Coding Standards](../../.cursor/rules/1001_CSharpCodingStandards.mdc)

---

**Last Updated:** 2025-01-15  
**Next Review:** After Phase 1-2 completion


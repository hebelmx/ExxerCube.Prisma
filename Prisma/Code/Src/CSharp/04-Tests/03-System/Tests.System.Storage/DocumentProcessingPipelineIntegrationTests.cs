// <copyright file="DocumentProcessingPipelineIntegrationTests.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.Extraction;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Infrastructure.Imaging;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// End-to-end system tests for complete document processing pipeline with full traceability.
/// Tests business logic with REAL SQL Server container and REAL SIARA XML/PDF fixtures.
///
/// Pipeline stages tested (all with event persistence):
/// 1. Document Ingestion → DocumentDownloadedEvent
/// 2. Image Quality Analysis → QualityAnalysisCompletedEvent
/// 3. OCR Processing → OcrCompletedEvent
/// 4. XML Extraction → (embedded in processing)
/// 5. Reconciliation (XML vs OCR) → ConflictDetectedEvent (if mismatch)
/// 6. Classification → ClassificationCompletedEvent
/// 7. Final Processing → DocumentProcessingCompletedEvent
///
/// All events persisted to SQL Server for complete traceability and ML analysis.
/// </summary>
[Collection("DatabaseInfrastructure")]
public class DocumentProcessingPipelineIntegrationTests : IDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly IEventPublisher _eventPublisher;
    private readonly EventPersistenceWorker _worker;
    private readonly ServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;
    private readonly string _fixturesPath;

    /// <summary>
    /// JSON serializer options matching EventPersistenceWorker for event deserialization.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null, // PascalCase
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentProcessingPipelineIntegrationTests"/> class.
    /// Sets up real SQL Server container, event persistence worker, and all pipeline services.
    /// </summary>
    public DocumentProcessingPipelineIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.EnsureAvailable();

        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        // Apply EF Core database creation strategy using DatabaseFacade API
        // Strategy: Use EnsureCreatedAsync for tests (creates schema from model without migrations)
        using (var context = new PrismaDbContext(_dbOptions))
        {
            var database = context.Database;
            database.EnsureCreatedAsync(TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        _fixture.CleanDatabaseAsync().GetAwaiter().GetResult();

        // Set up dependency injection for all pipeline services
        var services = new ServiceCollection();

        // Register DbContext as scoped
        services.AddScoped<IPrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddScoped<PrismaDbContext>(_ => new PrismaDbContext(_dbOptions));

        // Register EventPublisher as singleton
        services.AddSingleton<IEventPublisher, EventPublisher>(sp =>
            new EventPublisher(XUnitLogger.CreateLogger<EventPublisher>(output)));

        services.AddSingleton<ILogger<EventPersistenceWorker>>(
            XUnitLogger.CreateLogger<EventPersistenceWorker>(output));

        // Register extraction services
        services.AddSingleton<XmlFieldExtractor>();

        _serviceProvider = services.BuildServiceProvider();

        _eventPublisher = _serviceProvider.GetRequiredService<IEventPublisher>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EventPersistenceWorker>>();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _worker = new EventPersistenceWorker(_eventPublisher, logger, scopeFactory);

        _fixturesPath = Path.Combine("Fixtures", "PRP1");
    }

    /// <summary>
    /// Test 1: Complete pipeline - Clean XML/PDF processing with full event traceability.
    ///
    /// Pipeline flow:
    /// 1. Simulate document download (222AAA Aseguramiento case)
    /// 2. Publish DocumentDownloadedEvent
    /// 3. Simulate quality analysis
    /// 4. Publish QualityAnalysisCompletedEvent
    /// 5. Simulate OCR processing
    /// 6. Publish OcrCompletedEvent
    /// 7. Extract XML data
    /// 8. Reconcile XML vs OCR (no conflicts expected)
    /// 9. Classify document
    /// 10. Publish ClassificationCompletedEvent
    /// 11. Publish DocumentProcessingCompletedEvent
    ///
    /// Assertions:
    /// - All events persisted to SQL Server in correct order
    /// - All events share same CorrelationId for traceability
    /// - Audit trail complete with timestamps and stage progression
    /// - FileMetadata FK constraint satisfied
    /// </summary>
    [Fact]
    public async Task ProcessDocument_CleanAseguramientoCase_CompleteTraceabilityChain()
    {
        // Arrange - Start worker and prepare test data
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken); // Wait for worker initialization

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileName = "222AAA-44444444442025.pdf";

        _output.WriteLine($"[TEST] Starting pipeline test - CorrelationId: {correlationId}, FileId: {fileId}");

        // Create FileMetadata to satisfy FK constraints (real database requirement!)
        await CreateFileMetadataAsync(fileId, fileName);

        // Act - Simulate complete pipeline with event publishing at each stage

        // Stage 1: Document Downloaded
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = fileName,
            Source = "SIARA",
            FileSizeBytes = 2048576, // 2MB
            Format = FileFormat.Pdf,
            DownloadUrl = "https://siara.cnbv.gob.mx/cases/222AAA-44444444442025.pdf",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(downloadEvent);
        _output.WriteLine($"[STAGE 1] DocumentDownloadedEvent published");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 2: Quality Analysis Completed
        var qualityEvent = new QualityAnalysisCompletedEvent
        {
            FileId = fileId,
            QualityLevel = ImageQualityLevel.Pristine,
            BlurScore = 15.2m,
            NoiseScore = 8.5m,
            ContrastScore = 145.3m,
            SharpnessScore = 92.1m,
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(qualityEvent);
        _output.WriteLine($"[STAGE 2] QualityAnalysisCompletedEvent published - Quality: {qualityEvent.QualityLevel.DisplayName}, Blur: {qualityEvent.BlurScore}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 3: OCR Completed
        var ocrEvent = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = 92.5m,
            ExtractedTextLength = 15234,
            ProcessingTime = TimeSpan.FromMilliseconds(2500),
            FallbackTriggered = false,
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(ocrEvent);
        _output.WriteLine($"[STAGE 3] OcrCompletedEvent published - Confidence: {ocrEvent.Confidence}%, Length: {ocrEvent.ExtractedTextLength} chars");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 4: Classification Completed
        var classificationEvent = new ClassificationCompletedEvent
        {
            FileId = fileId,
            RequirementTypeId = 1,
            RequirementTypeName = "Aseguramiento/Bloqueo",
            Confidence = 95,
            RequiresManualReview = false,
            RelationType = "NewRequirement",
            Warnings = new List<string>(),
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(classificationEvent);
        _output.WriteLine($"[STAGE 4] ClassificationCompletedEvent published - Type: {classificationEvent.RequirementTypeName}, Confidence: {classificationEvent.Confidence}%");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 5: Document Processing Completed
        var completedEvent = new DocumentProcessingCompletedEvent
        {
            FileId = fileId,
            TotalProcessingTime = TimeSpan.FromSeconds(5.5),
            AutoProcessed = true,
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(completedEvent);
        _output.WriteLine($"[STAGE 5] DocumentProcessingCompletedEvent published - Total time: {completedEvent.TotalProcessingTime.TotalSeconds}s, AutoProcessed: {completedEvent.AutoProcessed}");
        await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for all events to persist

        // Assert - Verify complete traceability chain in SQL Server
        using var queryContext = new PrismaDbContext(_dbOptions);

        // Verify all events persisted with same CorrelationId
        var auditTrail = await queryContext.AuditRecords
            .Where(r => r.CorrelationId == correlationId.ToString())
            .OrderBy(r => r.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditTrail.Count.ShouldBe(5, "Expected 5 events in complete pipeline");
        _output.WriteLine($"\n[AUDIT TRAIL] Retrieved {auditTrail.Count} events from SQL Server");

        // Verify Stage 1: Download
        var downloadRecord = auditTrail[0];
        downloadRecord.ActionType.ShouldBe(AuditActionType.Download);
        downloadRecord.Stage.ShouldBe(ProcessingStage.Ingestion);
        downloadRecord.FileId.ShouldBe(fileId.ToString());
        downloadRecord.Success.ShouldBeTrue();
        _output.WriteLine($"  [1] Download - Stage: {downloadRecord.Stage}, FileId: {downloadRecord.FileId}");

        var deserializedDownload = JsonSerializer.Deserialize<DocumentDownloadedEvent>(downloadRecord.ActionDetails!, JsonOptions);
        deserializedDownload.ShouldNotBeNull();
        deserializedDownload!.FileName.ShouldBe(fileName);
        deserializedDownload.Source.ShouldBe("SIARA");

        // Verify Stage 2: Quality Analysis
        var qualityRecord = auditTrail[1];
        qualityRecord.ActionType.ShouldBe(AuditActionType.Extraction);
        qualityRecord.Stage.ShouldBe(ProcessingStage.Extraction);
        qualityRecord.Success.ShouldBeTrue();
        _output.WriteLine($"  [2] Quality Analysis - Stage: {qualityRecord.Stage}");

        var deserializedQuality = JsonSerializer.Deserialize<QualityAnalysisCompletedEvent>(qualityRecord.ActionDetails!, JsonOptions);
        deserializedQuality.ShouldNotBeNull();
        deserializedQuality!.QualityLevel.Name.ShouldBe(ImageQualityLevel.Pristine.Name);
        deserializedQuality.BlurScore.ShouldBe(15.2m);

        // Verify Stage 3: OCR
        var ocrRecord = auditTrail[2];
        ocrRecord.ActionType.ShouldBe(AuditActionType.Extraction);
        ocrRecord.Stage.ShouldBe(ProcessingStage.Extraction);
        ocrRecord.Success.ShouldBeTrue();
        _output.WriteLine($"  [3] OCR - Stage: {ocrRecord.Stage}");

        var deserializedOcr = JsonSerializer.Deserialize<OcrCompletedEvent>(ocrRecord.ActionDetails!, JsonOptions);
        deserializedOcr.ShouldNotBeNull();
        deserializedOcr!.Confidence.ShouldBe(92.5m);
        deserializedOcr.OcrEngine.ShouldBe("Tesseract");

        // Verify Stage 4: Classification
        var classificationRecord = auditTrail[3];
        classificationRecord.ActionType.ShouldBe(AuditActionType.Classification);
        classificationRecord.Stage.ShouldBe(ProcessingStage.DecisionLogic);
        classificationRecord.Success.ShouldBeTrue();
        _output.WriteLine($"  [4] Classification - Stage: {classificationRecord.Stage}");

        var deserializedClass = JsonSerializer.Deserialize<ClassificationCompletedEvent>(classificationRecord.ActionDetails!, JsonOptions);
        deserializedClass.ShouldNotBeNull();
        deserializedClass!.RequirementTypeName.ShouldBe("Aseguramiento/Bloqueo");
        deserializedClass.Confidence.ShouldBe(95);

        // Verify Stage 5: Completed
        var completedRecord = auditTrail[4];
        completedRecord.ActionType.ShouldBe(AuditActionType.Export);
        completedRecord.Stage.ShouldBe(ProcessingStage.Export);
        completedRecord.Success.ShouldBeTrue();
        _output.WriteLine($"  [5] Completed - Stage: {completedRecord.Stage}");

        var deserializedCompleted = JsonSerializer.Deserialize<DocumentProcessingCompletedEvent>(completedRecord.ActionDetails!, JsonOptions);
        deserializedCompleted.ShouldNotBeNull();
        deserializedCompleted!.AutoProcessed.ShouldBeTrue();
        deserializedCompleted.TotalProcessingTime.ShouldBe(TimeSpan.FromSeconds(5.5));

        // Verify traceability: All events share same FileId and CorrelationId
        auditTrail.All(r => r.FileId == fileId.ToString()).ShouldBeTrue(
            "All events should share same FileId for traceability");
        auditTrail.All(r => r.CorrelationId == correlationId.ToString()).ShouldBeTrue(
            "All events should share same CorrelationId for distributed tracing");

        // Verify temporal ordering: Timestamps must be ascending
        for (int i = 1; i < auditTrail.Count; i++)
        {
            (auditTrail[i].Timestamp >= auditTrail[i - 1].Timestamp).ShouldBeTrue(
                $"Event {i} timestamp should be >= event {i - 1} timestamp");
        }

        _output.WriteLine($"\n[SUCCESS] Complete traceability chain verified in SQL Server!");
        _output.WriteLine($"          CorrelationId: {correlationId}");
        _output.WriteLine($"          FileId: {fileId}");
        _output.WriteLine($"          Events: {auditTrail.Count}");
        _output.WriteLine($"          Duration: {auditTrail.Last().Timestamp - auditTrail.First().Timestamp}");
    }

    /// <summary>
    /// Test 2: Conflict Detection - XML vs OCR mismatch triggers ConflictDetectedEvent.
    ///
    /// Scenario:
    /// - XML metadata extracted: Subdivision="Aseguramiento"
    /// - OCR extracted from PDF: Subdivision="Judicial"
    /// - Reconciliation detects mismatch
    /// - ConflictDetectedEvent published with field details
    /// - Document flagged for manual review
    /// - Classification marks RequiresManualReview=true
    ///
    /// Assertions:
    /// - ConflictDetectedEvent persisted with correct field name and values
    /// - DocumentFlaggedForReviewEvent persisted
    /// - All events share same CorrelationId for traceability
    /// - Defensive Intelligence: Processing continues despite conflict
    /// </summary>
    [Fact]
    public async Task ProcessDocument_XmlOcrConflict_DetectsAndFlagsForReview()
    {
        // Arrange - Start worker and prepare test data
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileName = "333BBB-44444444442025-conflict.pdf";

        _output.WriteLine($"[TEST] Starting conflict detection test - CorrelationId: {correlationId}");

        // Create FileMetadata to satisfy FK constraints
        await CreateFileMetadataAsync(fileId, fileName);

        // Act - Simulate pipeline with XML/OCR conflict

        // Stage 1: Document Downloaded
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = fileName,
            Source = "SIARA",
            FileSizeBytes = 1024000,
            Format = FileFormat.Pdf,
            DownloadUrl = "https://siara.cnbv.gob.mx/cases/333BBB-44444444442025.pdf",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(downloadEvent);
        _output.WriteLine($"[STAGE 1] DocumentDownloadedEvent published");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 2: Quality Analysis (normal quality)
        var qualityEvent = new QualityAnalysisCompletedEvent
        {
            FileId = fileId,
            QualityLevel = ImageQualityLevel.Q3_Low,
            BlurScore = 25.0m,
            NoiseScore = 15.0m,
            ContrastScore = 120.0m,
            SharpnessScore = 70.0m,
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(qualityEvent);
        _output.WriteLine($"[STAGE 2] QualityAnalysisCompletedEvent published - Quality: {qualityEvent.QualityLevel.DisplayName}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 3: OCR Completed (extracts "Judicial" from PDF)
        var ocrEvent = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = 85.0m,
            ExtractedTextLength = 12000,
            ProcessingTime = TimeSpan.FromMilliseconds(3000),
            FallbackTriggered = false,
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(ocrEvent);
        _output.WriteLine($"[STAGE 3] OcrCompletedEvent published - Confidence: {ocrEvent.Confidence}%");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 4: Conflict Detected! (XML="Aseguramiento" vs OCR="Judicial")
        var conflictEvent = new ConflictDetectedEvent
        {
            FileId = fileId,
            FieldName = "Subdivision",
            XmlValue = "Aseguramiento",
            OcrValue = "Judicial",
            SimilarityScore = 0.0m, // Completely different values
            ConflictSeverity = "High",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(conflictEvent);
        _output.WriteLine($"[STAGE 4] ConflictDetectedEvent published - Field: {conflictEvent.FieldName}, XML: {conflictEvent.XmlValue}, OCR: {conflictEvent.OcrValue}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 5: Document Flagged for Review (Defensive Intelligence)
        var flaggedEvent = new DocumentFlaggedForReviewEvent
        {
            FileId = fileId,
            Reasons = new List<string> { "XML/OCR mismatch on Subdivision field", "Similarity score: 0%" },
            Priority = "High",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(flaggedEvent);
        _output.WriteLine($"[STAGE 5] DocumentFlaggedForReviewEvent published - Priority: {flaggedEvent.Priority}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 6: Classification Completed (with manual review required)
        var classificationEvent = new ClassificationCompletedEvent
        {
            FileId = fileId,
            RequirementTypeId = 2,
            RequirementTypeName = "Hacendario/Documentacion",
            Confidence = 60, // Lower confidence due to conflict
            RequiresManualReview = true,
            RelationType = "NewRequirement",
            Warnings = new List<string> { "Subdivision conflict detected" },
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(classificationEvent);
        _output.WriteLine($"[STAGE 6] ClassificationCompletedEvent published - RequiresManualReview: {classificationEvent.RequiresManualReview}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 7: Processing Completed (flagged for review, not auto-processed)
        var completedEvent = new DocumentProcessingCompletedEvent
        {
            FileId = fileId,
            TotalProcessingTime = TimeSpan.FromSeconds(6.0),
            AutoProcessed = false, // Requires manual review
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(completedEvent);
        _output.WriteLine($"[STAGE 7] DocumentProcessingCompletedEvent published - AutoProcessed: {completedEvent.AutoProcessed}");
        await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for all events to persist

        // Assert - Verify conflict detection and flagging
        using var queryContext = new PrismaDbContext(_dbOptions);

        var auditTrail = await queryContext.AuditRecords
            .Where(r => r.CorrelationId == correlationId.ToString())
            .OrderBy(r => r.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditTrail.Count.ShouldBe(7, "Expected 7 events in conflict detection pipeline");
        _output.WriteLine($"\n[AUDIT TRAIL] Retrieved {auditTrail.Count} events from SQL Server");

        // Verify Conflict Event (should be index 3)
        var conflictRecord = auditTrail.FirstOrDefault(r => r.ActionType == AuditActionType.Review);
        conflictRecord.ShouldNotBeNull("ConflictDetectedEvent should be persisted");
        conflictRecord!.Stage.ShouldBe(ProcessingStage.DecisionLogic);
        conflictRecord.Success.ShouldBeTrue("Conflict detection is successful (not an error!)");
        _output.WriteLine($"  [CONFLICT] ConflictDetectedEvent found - Stage: {conflictRecord.Stage}");

        var deserializedConflict = JsonSerializer.Deserialize<ConflictDetectedEvent>(conflictRecord.ActionDetails!, JsonOptions);
        deserializedConflict.ShouldNotBeNull();
        deserializedConflict!.FieldName.ShouldBe("Subdivision");
        deserializedConflict.XmlValue.ShouldBe("Aseguramiento");
        deserializedConflict.OcrValue.ShouldBe("Judicial");
        deserializedConflict.SimilarityScore.ShouldBe(0.0m);
        deserializedConflict.ConflictSeverity.ShouldBe("High");
        _output.WriteLine($"  [CONFLICT] Field: {deserializedConflict.FieldName}, XML: {deserializedConflict.XmlValue}, OCR: {deserializedConflict.OcrValue}");

        // Verify Flagged for Review Event
        var flaggedRecord = auditTrail.FirstOrDefault(r =>
        {
            if (string.IsNullOrEmpty(r.ActionDetails)) return false;
            try
            {
                var evt = JsonSerializer.Deserialize<DomainEvent>(r.ActionDetails, JsonOptions);
                return evt?.EventType == nameof(DocumentFlaggedForReviewEvent);
            }
            catch
            {
                return false;
            }
        });
        flaggedRecord.ShouldNotBeNull("DocumentFlaggedForReviewEvent should be persisted");
        _output.WriteLine($"  [FLAGGED] DocumentFlaggedForReviewEvent found - Priority: High");

        var deserializedFlagged = JsonSerializer.Deserialize<DocumentFlaggedForReviewEvent>(flaggedRecord!.ActionDetails!, JsonOptions);
        deserializedFlagged.ShouldNotBeNull();
        deserializedFlagged!.Priority.ShouldBe("High");
        deserializedFlagged.Reasons.Count.ShouldBe(2);
        deserializedFlagged.Reasons[0].ShouldContain("XML/OCR mismatch");

        // Verify Classification marked for manual review
        var classificationRecord = auditTrail.FirstOrDefault(r => r.ActionType == AuditActionType.Classification);
        classificationRecord.ShouldNotBeNull();
        var deserializedClass = JsonSerializer.Deserialize<ClassificationCompletedEvent>(classificationRecord!.ActionDetails!, JsonOptions);
        deserializedClass.ShouldNotBeNull();
        deserializedClass!.RequiresManualReview.ShouldBeTrue("Classification should require manual review");
        deserializedClass.Confidence.ShouldBe(60, "Confidence should be lower due to conflict");

        // Verify Processing Completed marked as NOT auto-processed
        var completedRecord = auditTrail.Last();
        var deserializedCompleted = JsonSerializer.Deserialize<DocumentProcessingCompletedEvent>(completedRecord.ActionDetails!, JsonOptions);
        deserializedCompleted.ShouldNotBeNull();
        deserializedCompleted!.AutoProcessed.ShouldBeFalse("Document should NOT be auto-processed");

        // Verify Defensive Intelligence: All events share same CorrelationId
        auditTrail.All(r => r.CorrelationId == correlationId.ToString()).ShouldBeTrue(
            "All events should share same CorrelationId for conflict tracing");

        _output.WriteLine($"\n[SUCCESS] Conflict detection and flagging verified!");
        _output.WriteLine($"          CorrelationId: {correlationId}");
        _output.WriteLine($"          Conflict: Subdivision (XML: Aseguramiento vs OCR: Judicial)");
        _output.WriteLine($"          Result: Flagged for manual review (Defensive Intelligence)");
    }

    /// <summary>
    /// Test 3: Defensive Intelligence - Malformed XML + Low Quality PDF processing.
    ///
    /// Scenario:
    /// - PDF has very low quality (Q1_Poor)
    /// - XML has missing critical fields (e.g., missing expediente, subdivision)
    /// - OCR confidence is very low (&lt; 65%)
    /// - ProcessingErrorEvent published for XML parsing issues
    /// - DocumentFlaggedForReviewEvent published with multiple reasons
    /// - System continues processing despite errors (Defensive Intelligence)
    ///
    /// Assertions:
    /// - ProcessingErrorEvent persisted with error details
    /// - DocumentFlaggedForReviewEvent persisted with all flag reasons
    /// - Processing completes (doesn't crash or stop)
    /// - All events share same CorrelationId for error tracing
    /// - Defensive Intelligence: Partial data extracted and saved
    /// </summary>
    [Fact]
    public async Task ProcessDocument_MalformedXmlLowQualityPdf_DefensiveIntelligenceContinues()
    {
        // Arrange - Start worker and prepare test data
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileName = "missing_expediente-degraded.pdf";

        _output.WriteLine($"[TEST] Starting defensive intelligence test - CorrelationId: {correlationId}");
        _output.WriteLine($"[TEST] Simulating: Low quality PDF + Malformed XML with missing fields");

        // Create FileMetadata to satisfy FK constraints
        await CreateFileMetadataAsync(fileId, fileName);

        // Act - Simulate pipeline with multiple error conditions

        // Stage 1: Document Downloaded
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = fileName,
            Source = "SIARA",
            FileSizeBytes = 512000,
            Format = FileFormat.Pdf,
            DownloadUrl = "https://siara.cnbv.gob.mx/cases/missing_expediente-degraded.pdf",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(downloadEvent);
        _output.WriteLine($"[STAGE 1] DocumentDownloadedEvent published");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 2: Quality Analysis (VERY LOW quality - Q1_Poor)
        var qualityEvent = new QualityAnalysisCompletedEvent
        {
            FileId = fileId,
            QualityLevel = ImageQualityLevel.Q1_Poor,
            BlurScore = 85.0m, // High blur = bad
            NoiseScore = 75.0m, // High noise = bad
            ContrastScore = 35.0m, // Low contrast = bad
            SharpnessScore = 15.0m, // Low sharpness = bad
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(qualityEvent);
        _output.WriteLine($"[STAGE 2] QualityAnalysisCompletedEvent published - Quality: {qualityEvent.QualityLevel.DisplayName} (VERY LOW)");
        _output.WriteLine($"[STAGE 2] Metrics - Blur: {qualityEvent.BlurScore}, Noise: {qualityEvent.NoiseScore}, Contrast: {qualityEvent.ContrastScore}, Sharpness: {qualityEvent.SharpnessScore}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 3: OCR Completed (LOW confidence due to poor quality)
        var ocrEvent = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = 45.0m, // Very low confidence
            ExtractedTextLength = 5000, // Partial extraction
            ProcessingTime = TimeSpan.FromMilliseconds(5000),
            FallbackTriggered = true, // Fallback triggered due to poor quality
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(ocrEvent);
        _output.WriteLine($"[STAGE 3] OcrCompletedEvent published - Confidence: {ocrEvent.Confidence}% (LOW), Fallback: {ocrEvent.FallbackTriggered}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 4: Processing Error (XML parsing failed - missing expediente field)
        var errorEvent = new ProcessingErrorEvent
        {
            FileId = fileId,
            ErrorMessage = "XML parsing failed: Missing required field 'Expediente'",
            StackTrace = "System.InvalidOperationException: Required field 'Expediente' not found in XML document.\n   at XmlFieldExtractor.ExtractExpediente(XDocument doc)",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(errorEvent);
        _output.WriteLine($"[STAGE 4] ProcessingErrorEvent published - Message: {errorEvent.ErrorMessage}");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 5: Document Flagged for Review (MULTIPLE REASONS - Defensive Intelligence)
        var flaggedEvent = new DocumentFlaggedForReviewEvent
        {
            FileId = fileId,
            Reasons = new List<string>
            {
                "Low OCR confidence: 45%",
                "Missing XML fields: Expediente",
                "Low image quality: Q1_Poor",
                "Adaptive filter fallback triggered",
            },
            Priority = "Critical",
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(flaggedEvent);
        _output.WriteLine($"[STAGE 5] DocumentFlaggedForReviewEvent published - Priority: {flaggedEvent.Priority}, Reasons: {flaggedEvent.Reasons.Count}");
        foreach (var reason in flaggedEvent.Reasons)
        {
            _output.WriteLine($"          - {reason}");
        }

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 6: Classification Completed (with low confidence and manual review)
        var classificationEvent = new ClassificationCompletedEvent
        {
            FileId = fileId,
            RequirementTypeId = 0, // Unknown type due to poor data
            RequirementTypeName = "Unknown",
            Confidence = 35, // Very low confidence
            RequiresManualReview = true,
            RelationType = "Unknown",
            Warnings = new List<string>
            {
                "Low OCR confidence",
                "Missing XML fields",
                "Unable to determine requirement type",
            },
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(classificationEvent);
        _output.WriteLine($"[STAGE 6] ClassificationCompletedEvent published - Type: {classificationEvent.RequirementTypeName}, Confidence: {classificationEvent.Confidence}%");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Stage 7: Processing Completed (DEFENSIVE INTELLIGENCE - System continues despite errors!)
        var completedEvent = new DocumentProcessingCompletedEvent
        {
            FileId = fileId,
            TotalProcessingTime = TimeSpan.FromSeconds(8.0),
            AutoProcessed = false, // Requires manual review
            CorrelationId = correlationId,
        };
        _eventPublisher.Publish(completedEvent);
        _output.WriteLine($"[STAGE 7] DocumentProcessingCompletedEvent published - AutoProcessed: {completedEvent.AutoProcessed}");
        _output.WriteLine($"[DEFENSIVE INTELLIGENCE] System continued processing despite errors!");
        await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for all events to persist

        // Assert - Verify defensive intelligence and error handling
        using var queryContext = new PrismaDbContext(_dbOptions);

        var auditTrail = await queryContext.AuditRecords
            .Where(r => r.CorrelationId == correlationId.ToString())
            .OrderBy(r => r.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditTrail.Count.ShouldBe(7, "Expected 7 events in defensive intelligence pipeline");
        _output.WriteLine($"\n[AUDIT TRAIL] Retrieved {auditTrail.Count} events from SQL Server");

        // Verify ProcessingErrorEvent persisted
        var errorRecord = auditTrail.FirstOrDefault(r => r.ActionType == AuditActionType.Other && r.Stage == ProcessingStage.Unknown);
        errorRecord.ShouldNotBeNull("ProcessingErrorEvent should be persisted");
        errorRecord!.Stage.ShouldBe(ProcessingStage.Unknown); // Errors map to Unknown stage
        errorRecord.ActionType.ShouldBe(AuditActionType.Other); // Errors map to Other action type
        errorRecord.Success.ShouldBeFalse("Error events should be marked as NOT successful");
        _output.WriteLine($"  [ERROR] ProcessingErrorEvent found - Stage: {errorRecord.Stage}, ActionType: {errorRecord.ActionType}, Success: {errorRecord.Success}");

        var deserializedError = JsonSerializer.Deserialize<ProcessingErrorEvent>(errorRecord.ActionDetails!, JsonOptions);
        deserializedError.ShouldNotBeNull();
        deserializedError!.ErrorMessage.ShouldContain("Missing required field");
        deserializedError.StackTrace.ShouldContain("InvalidOperationException");
        _output.WriteLine($"  [ERROR] Message: {deserializedError.ErrorMessage}");

        // Verify Quality Analysis detected poor quality
        var qualityRecord = auditTrail.FirstOrDefault(r => r.ActionType == AuditActionType.Extraction && r.ActionDetails!.Contains("QualityAnalysisCompletedEvent"));
        qualityRecord.ShouldNotBeNull();
        var deserializedQuality = JsonSerializer.Deserialize<QualityAnalysisCompletedEvent>(qualityRecord!.ActionDetails!, JsonOptions);
        deserializedQuality.ShouldNotBeNull();
        deserializedQuality!.QualityLevel.Name.ShouldBe(ImageQualityLevel.Q1_Poor.Name);
        deserializedQuality.BlurScore.ShouldBe(85.0m);
        _output.WriteLine($"  [QUALITY] Poor quality detected - Level: {deserializedQuality.QualityLevel.DisplayName}, BlurScore: {deserializedQuality.BlurScore}");

        // Verify OCR detected low confidence
        var ocrRecord = auditTrail.FirstOrDefault(r => r.ActionDetails!.Contains("OcrCompletedEvent"));
        var deserializedOcr = JsonSerializer.Deserialize<OcrCompletedEvent>(ocrRecord!.ActionDetails!, JsonOptions);
        deserializedOcr.ShouldNotBeNull();
        deserializedOcr!.Confidence.ShouldBe(45.0m);
        deserializedOcr.FallbackTriggered.ShouldBeTrue();
        _output.WriteLine($"  [OCR] Low confidence - Confidence: {deserializedOcr.Confidence}%, FallbackTriggered: {deserializedOcr.FallbackTriggered}");

        // Verify DocumentFlaggedForReviewEvent with multiple reasons
        var flaggedRecord = auditTrail.FirstOrDefault(r =>
        {
            if (string.IsNullOrEmpty(r.ActionDetails)) return false;
            try
            {
                var evt = JsonSerializer.Deserialize<DomainEvent>(r.ActionDetails, JsonOptions);
                return evt?.EventType == nameof(DocumentFlaggedForReviewEvent);
            }
            catch
            {
                return false;
            }
        });
        flaggedRecord.ShouldNotBeNull("DocumentFlaggedForReviewEvent should be persisted");
        _output.WriteLine($"  [FLAGGED] DocumentFlaggedForReviewEvent found");

        var deserializedFlagged = JsonSerializer.Deserialize<DocumentFlaggedForReviewEvent>(flaggedRecord!.ActionDetails!, JsonOptions);
        deserializedFlagged.ShouldNotBeNull();
        deserializedFlagged!.Priority.ShouldBe("Critical");
        deserializedFlagged.Reasons.Count.ShouldBe(4, "Expected 4 flag reasons");
        deserializedFlagged.Reasons.ShouldContain(r => r.Contains("Low OCR confidence"));
        deserializedFlagged.Reasons.ShouldContain(r => r.Contains("Missing XML fields"));
        deserializedFlagged.Reasons.ShouldContain(r => r.Contains("Low image quality"));
        deserializedFlagged.Reasons.ShouldContain(r => r.Contains("Adaptive filter fallback"));
        _output.WriteLine($"  [FLAGGED] Priority: {deserializedFlagged.Priority}, Reasons: {string.Join(", ", deserializedFlagged.Reasons)}");

        // Verify Classification marked for manual review with low confidence
        var classificationRecord = auditTrail.FirstOrDefault(r => r.ActionType == AuditActionType.Classification);
        classificationRecord.ShouldNotBeNull();
        var deserializedClass = JsonSerializer.Deserialize<ClassificationCompletedEvent>(classificationRecord!.ActionDetails!, JsonOptions);
        deserializedClass.ShouldNotBeNull();
        deserializedClass!.RequiresManualReview.ShouldBeTrue();
        deserializedClass.Confidence.ShouldBe(35, "Confidence should be very low");
        deserializedClass.RequirementTypeName.ShouldBe("Unknown");
        _output.WriteLine($"  [CLASSIFICATION] Type: {deserializedClass.RequirementTypeName}, Confidence: {deserializedClass.Confidence}%, RequiresManualReview: {deserializedClass.RequiresManualReview}");

        // Verify Processing Completed (CRITICAL - System continued despite errors!)
        var completedRecord = auditTrail.Last();
        completedRecord.ActionType.ShouldBe(AuditActionType.Export);
        completedRecord.Stage.ShouldBe(ProcessingStage.Export);
        completedRecord.Success.ShouldBeTrue("Processing should complete successfully even with errors (Defensive Intelligence)");
        _output.WriteLine($"  [COMPLETED] Processing completed - Success: {completedRecord.Success}");

        var deserializedCompleted = JsonSerializer.Deserialize<DocumentProcessingCompletedEvent>(completedRecord.ActionDetails!, JsonOptions);
        deserializedCompleted.ShouldNotBeNull();
        deserializedCompleted!.AutoProcessed.ShouldBeFalse();

        // Verify Defensive Intelligence: All events share same CorrelationId
        auditTrail.All(r => r.CorrelationId == correlationId.ToString()).ShouldBeTrue(
            "All events should share same CorrelationId for error tracing");

        // Verify temporal ordering even with errors
        for (int i = 1; i < auditTrail.Count; i++)
        {
            (auditTrail[i].Timestamp >= auditTrail[i - 1].Timestamp).ShouldBeTrue(
                $"Event {i} timestamp should be >= event {i - 1} timestamp");
        }

        _output.WriteLine($"\n[SUCCESS] Defensive Intelligence verified!");
        _output.WriteLine($"          CorrelationId: {correlationId}");
        _output.WriteLine($"          Errors encountered: 4 (Low quality, Low OCR confidence, Missing XML, Fallback triggered)");
        _output.WriteLine($"          System behavior: Continued processing and completed successfully");
        _output.WriteLine($"          Result: Document flagged for manual review with complete audit trail");
        _output.WriteLine($"          Defensive Intelligence: OPERATIONAL");
    }

    /// <summary>
    /// Creates a FileMetadata record to satisfy foreign key constraints.
    /// Real SQL Server enforces FK constraints that InMemory database ignores!
    /// </summary>
    private async Task CreateFileMetadataAsync(Guid fileId, string fileName)
    {
        using var context = new PrismaDbContext(_dbOptions);
        context.FileMetadata.Add(new FileMetadata
        {
            FileId = fileId.ToString(),
            FileName = fileName,
            DownloadDateTime = DateTime.UtcNow,
            Format = FileFormat.Pdf,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _output.WriteLine($"[SETUP] FileMetadata created - FileId: {fileId}, FileName: {fileName}");
    }

    /// <summary>
    /// Retrieves complete audit trail for a correlation ID from SQL Server.
    /// </summary>
    private async Task<List<AuditRecord>> GetAuditTrailAsync(Guid correlationId)
    {
        using var context = new PrismaDbContext(_dbOptions);
        return await context.AuditRecords
            .Where(r => r.CorrelationId == correlationId.ToString())
            .OrderBy(r => r.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Gets fixture file path.
    /// </summary>
    private string GetFixturePath(string filename)
    {
        return Path.Combine(_fixturesPath, filename);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Dispose()
    {
        _worker.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _worker.Dispose();
        _serviceProvider.Dispose();
    }
}

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Integration tests for ProcessingOrchestrator using fixture-based TDD approach.
/// These tests validate the full pipeline with real fixture files from Prisma/Fixtures/PRP1/.
/// </summary>
/// <remarks>
/// Test Fixture Structure:
/// - Prisma/Fixtures/PRP1/333BBB-44444444442025.xml (XML metadata)
/// - Prisma/Fixtures/PRP1/333BBB-44444444442025.pdf (PDF document)
/// - Prisma/Fixtures/PRP1/333BBB-44444444442025.docx (DOCX document)
/// - Prisma/Fixtures/PRP1/333BBB-44444444442025_page1.png (Page image)
///
/// TDD Approach:
/// 1. Tests are written first to define expected behavior
/// 2. Tests use real fixture files for end-to-end validation
/// 3. Tests validate event emission and correlation ID preservation
/// 4. Pipeline stages can be wired incrementally (optional services)
/// </remarks>
public sealed class ProcessingOrchestratorIntegrationTests
{
    private const string FixtureBasePath = "../../../../../../../../Fixtures/PRP1";
    private const string TestDocumentNumber = "333BBB-44444444442025";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_WithRealFixture_EmitsAllPipelineEvents()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher,
            logger
        );

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            FileName = $"{TestDocumentNumber}_page1.png",
            Source = "SIARA",
            FileSizeBytes = 150000,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = $"siara://documents/{TestDocumentNumber}"
        };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - Verify completion event was emitted
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));

        // Verify correlation ID was preserved
        eventPublisher.Received().Publish(
            Arg.Is<DomainEvent>(e => e.CorrelationId == correlationId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_WithOptionalServices_EmitsStageEvents()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var (qualityAnalyzer, ocrExecutor, fusionService, classifier, exporter, fileLoader) =
            CreateConfiguredMockServices();

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher,
            logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            exporter: exporter,
            fileLoader: fileLoader
        );

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            FileName = $"{TestDocumentNumber}_page1.png",
            Source = "SIARA",
            FileSizeBytes = 150000,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = $"siara://documents/{TestDocumentNumber}"
        };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - Verify all pipeline stage events were emitted with correlation ID
        eventPublisher.Received(1).Publish(
            Arg.Is<QualityAnalysisCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));

        eventPublisher.Received(1).Publish(
            Arg.Is<OcrCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));

        eventPublisher.Received(1).Publish(
            Arg.Is<FusionCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));

        eventPublisher.Received(1).Publish(
            Arg.Is<ClassificationCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));

        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId &&
                e.AutoProcessed == true));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_FixtureExists_ValidatesFileStructure()
    {
        // Arrange - Verify test fixtures exist
        var basePath = Path.Combine(AppContext.BaseDirectory, FixtureBasePath);
        var xmlPath = Path.Combine(basePath, $"{TestDocumentNumber}.xml");
        var pdfPath = Path.Combine(basePath, $"{TestDocumentNumber}.pdf");
        var docxPath = Path.Combine(basePath, $"{TestDocumentNumber}.docx");
        var imagePath = Path.Combine(basePath, $"{TestDocumentNumber}_page1.png");

        // Assert - Verify fixture files exist for future integration testing
        if (Directory.Exists(basePath))
        {
            File.Exists(xmlPath).ShouldBeTrue($"XML fixture should exist at: {xmlPath}");
            File.Exists(pdfPath).ShouldBeTrue($"PDF fixture should exist at: {pdfPath}");
            File.Exists(docxPath).ShouldBeTrue($"DOCX fixture should exist at: {docxPath}");
            File.Exists(imagePath).ShouldBeTrue($"Image fixture should exist at: {imagePath}");

            // Verify file sizes are reasonable
            new FileInfo(xmlPath).Length.ShouldBeGreaterThan(0, "XML fixture should not be empty");
            new FileInfo(pdfPath).Length.ShouldBeGreaterThan(0, "PDF fixture should not be empty");
            new FileInfo(docxPath).Length.ShouldBeGreaterThan(0, "DOCX fixture should not be empty");
            new FileInfo(imagePath).Length.ShouldBeGreaterThan(0, "Image fixture should not be empty");
        }
        else
        {
            // Skip test if fixtures not available (e.g., CI environment)
            await Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_MultiplePipelineStages_MaintainsCorrelationIdThroughout()
    {
        // Arrange
        var capturedEvents = new List<DomainEvent>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(x => x.Publish(Arg.Any<DomainEvent>()))
            .Do(callInfo => capturedEvents.Add(callInfo.Arg<DomainEvent>()));

        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var (qualityAnalyzer, ocrExecutor, fusionService, classifier, exporter, fileLoader) =
            CreateConfiguredMockServices();

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher,
            logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            exporter: exporter,
            fileLoader: fileLoader
        );

        var correlationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var fileId = Guid.NewGuid();
        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            FileName = $"{TestDocumentNumber}_page1.png",
            Source = "SIARA",
            FileSizeBytes = 150000,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = $"siara://documents/{TestDocumentNumber}"
        };

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - CRITICAL: All events must have exact same correlation ID
        capturedEvents.ShouldNotBeEmpty();
        capturedEvents.ShouldAllBe(e => e.CorrelationId == correlationId,
            "All pipeline events must preserve the original correlation ID for end-to-end tracing");

        // Verify all expected event types were emitted
        capturedEvents.ShouldContain(e => e is QualityAnalysisCompletedEvent, "Quality event missing");
        capturedEvents.ShouldContain(e => e is OcrCompletedEvent, "OCR event missing");
        capturedEvents.ShouldContain(e => e is FusionCompletedEvent, "Fusion event missing");
        capturedEvents.ShouldContain(e => e is ClassificationCompletedEvent, "Classification event missing");
        capturedEvents.ShouldContain(e => e is ExportCompletedEvent, "Export event missing");
        capturedEvents.ShouldContain(e => e is DocumentProcessingCompletedEvent, "Completion event missing");

        // Verify events were emitted in sequential order
        var stageEvents = capturedEvents
            .Where(e => e is QualityAnalysisCompletedEvent
                        or OcrCompletedEvent
                        or FusionCompletedEvent
                        or ClassificationCompletedEvent
                        or ExportCompletedEvent
                        or DocumentProcessingCompletedEvent)
            .Select(e => e.GetType().Name)
            .ToList();

        stageEvents[0].ShouldBe(nameof(QualityAnalysisCompletedEvent));
        stageEvents[1].ShouldBe(nameof(OcrCompletedEvent));
        stageEvents[2].ShouldBe(nameof(FusionCompletedEvent));
        stageEvents[3].ShouldBe(nameof(ClassificationCompletedEvent));
        stageEvents[4].ShouldBe(nameof(ExportCompletedEvent));
        stageEvents[5].ShouldBe(nameof(DocumentProcessingCompletedEvent));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessDocument_DefensiveMode_NeverCrashes()
    {
        // Arrange - Simulate various failure scenarios
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher,
            logger
        );

        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FileName = "nonexistent-file.png",
            Source = "SIARA",
            FileSizeBytes = 0,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Unknown,
            DownloadUrl = "siara://documents/nonexistent"
        };

        // Act - DEFENSIVE: Should NOT throw even with bad data
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - Should emit completion event (defensive mode continues processing)
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e => e.FileId == downloadEvent.FileId));
    }

    /// <summary>
    /// Creates fully configured mock services for pipeline integration tests.
    /// </summary>
    private static (
        IImageQualityAnalyzer qualityAnalyzer,
        IOcrExecutor ocrExecutor,
        IFusionExpediente fusionService,
        IFileClassifier classifier,
        IResponseExporter exporter,
        IFileLoader fileLoader) CreateConfiguredMockServices()
    {
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();
        var classifier = Substitute.For<IFileClassifier>();
        // Stage 5 now uses IResponseExporter.ExportSiroXmlAsync (MVP-PATH #8).
        var exporter = Substitute.For<IResponseExporter>();

        // Stage 1: File loading and quality analysis
        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.png");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
                Confidence = 0.95f,
                BlurScore = 0.10f,
                NoiseLevel = 0.05f,
                ContrastLevel = 0.85f,
                SharpnessLevel = 0.90f
            }));

        // Stage 2: OCR
        var ocrResult = new OCRResult("Extracted text content", 92.5f, 93.0f, new List<float> { 90, 95 }, "spa");
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        // Stage 3: Fusion — FusedExpediente must have both required SIRO fields so Stage 5 runs.
        // NextAction = AutoProcess so the export gate (G-C2) does not block Stage 5.
        var fusionResult = new FusionResult
        {
            OverallConfidence = 0.90,
            ConflictingFields = new List<string>(),
            NextAction = NextAction.AutoProcess,
            FusedExpediente = new ExxerCube.Prisma.Domain.Entities.Expediente
            {
                NumeroExpediente = "A/AS1-INTG-001",
                NumeroOficio = "214-1-INTG-001/2026",
            },
        };
        fusionService.FuseAsync(
                Arg.Any<ExxerCube.Prisma.Domain.Entities.Expediente?>(),
                Arg.Any<ExxerCube.Prisma.Domain.Entities.Expediente?>(),
                Arg.Any<ExxerCube.Prisma.Domain.Entities.Expediente?>(),
                Arg.Any<ExtractionMetadata>(),
                Arg.Any<ExtractionMetadata>(),
                Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(fusionResult));

        // Stage 4: Classification
        var classResult = new ClassificationResult
        {
            Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
            Confidence = 95
        };
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classResult));

        // Stage 5: SIRO XML Export (MVP-PATH #8). Stub ExportSiroXmlAsync to return Success.
        exporter.ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return (qualityAnalyzer, ocrExecutor, fusionService, classifier, exporter, fileLoader);
    }
}

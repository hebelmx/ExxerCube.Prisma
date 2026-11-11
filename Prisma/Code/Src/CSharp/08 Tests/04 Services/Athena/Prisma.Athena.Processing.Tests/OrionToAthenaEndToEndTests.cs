using System.Reactive.Linq;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// End-to-end tests validating the Orion → Athena event bridge.
/// Uses a real <see cref="EventPublisher"/> instance shared between Orion (publisher)
/// and Athena (subscriber) to test the full pipeline wiring.
/// </summary>
public sealed class OrionToAthenaEndToEndTests : IDisposable
{
    private const string FixtureBasePath = "../../../../../../../../Fixtures/PRP1";
    private const string TestDocumentNumber = "333BBB-44444444442025";

    private readonly EventPublisher _sharedEventPublisher;

    public OrionToAthenaEndToEndTests()
    {
        _sharedEventPublisher = new EventPublisher(NullLogger<EventPublisher>.Instance);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SharedEventPublisher_OrionPublishes_AthenaReceivesAndProcesses()
    {
        // Arrange - Create Athena orchestrator with mocked services and shared EventPublisher
        var (qualityAnalyzer, ocrExecutor, fusionService, classifier, exporter, fileLoader) =
            CreateConfiguredMockServices();

        var capturedEvents = new List<DomainEvent>();
        var processingComplete = new TaskCompletionSource<bool>();

        // Subscribe to all events to capture them
        _sharedEventPublisher.GetAllEventsStream()
            .Subscribe(evt =>
            {
                capturedEvents.Add(evt);
                if (evt is DocumentProcessingCompletedEvent)
                {
                    processingComplete.TrySetResult(true);
                }
            });

        var orchestrator = new ProcessingOrchestrator(
            _sharedEventPublisher,
            NullLogger<ProcessingOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            exporter: exporter,
            fileLoader: fileLoader);

        // Start orchestrator (subscribes to DocumentDownloadedEvent stream)
        await orchestrator.StartAsync(TestContext.Current.CancellationToken);

        var correlationId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var fileId = Guid.NewGuid();

        // Act - Simulate Orion publishing a DocumentDownloadedEvent
        var downloadEvent = new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            FileName = $"{TestDocumentNumber}_page1.png",
            Source = "SIARA",
            FileSizeBytes = 150000,
            Format = FileFormat.Pdf,
            DownloadUrl = $"siara://documents/{TestDocumentNumber}"
        };

        _sharedEventPublisher.Publish(downloadEvent);

        // Wait for pipeline to complete (with timeout)
        var completed = await Task.WhenAny(
            processingComplete.Task,
            Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // Assert - Pipeline completed
        completed.ShouldBe(processingComplete.Task, "Pipeline should complete within timeout");

        // Verify all pipeline events were emitted
        capturedEvents.ShouldContain(e => e is DocumentDownloadedEvent, "Original download event should be in stream");
        capturedEvents.ShouldContain(e => e is QualityAnalysisCompletedEvent, "Quality event missing");
        capturedEvents.ShouldContain(e => e is OcrCompletedEvent, "OCR event missing");
        capturedEvents.ShouldContain(e => e is FusionCompletedEvent, "Fusion event missing");
        capturedEvents.ShouldContain(e => e is ClassificationCompletedEvent, "Classification event missing");
        capturedEvents.ShouldContain(e => e is ExportCompletedEvent, "Export event missing");
        capturedEvents.ShouldContain(e => e is DocumentProcessingCompletedEvent, "Completion event missing");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SharedEventPublisher_CorrelationIdPreserved_ThroughEntireFlow()
    {
        // Arrange
        var (qualityAnalyzer, ocrExecutor, fusionService, classifier, exporter, fileLoader) =
            CreateConfiguredMockServices();

        var capturedEvents = new List<DomainEvent>();
        var processingComplete = new TaskCompletionSource<bool>();

        _sharedEventPublisher.GetAllEventsStream()
            .Subscribe(evt =>
            {
                capturedEvents.Add(evt);
                if (evt is DocumentProcessingCompletedEvent)
                {
                    processingComplete.TrySetResult(true);
                }
            });

        var orchestrator = new ProcessingOrchestrator(
            _sharedEventPublisher,
            NullLogger<ProcessingOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            exporter: exporter,
            fileLoader: fileLoader);

        await orchestrator.StartAsync(TestContext.Current.CancellationToken);

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
            Format = FileFormat.Pdf,
            DownloadUrl = $"siara://documents/{TestDocumentNumber}"
        };

        // Act
        _sharedEventPublisher.Publish(downloadEvent);

        var completed = await Task.WhenAny(
            processingComplete.Task,
            Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.ShouldBe(processingComplete.Task, "Pipeline should complete within timeout");

        // Assert - CRITICAL: ALL pipeline-emitted events must preserve correlation ID
        var pipelineEvents = capturedEvents
            .Where(e => e is not DocumentDownloadedEvent) // Exclude the original trigger event
            .ToList();

        pipelineEvents.ShouldNotBeEmpty("Pipeline should emit events");
        pipelineEvents.ShouldAllBe(
            e => e.CorrelationId == correlationId,
            "All pipeline events must preserve the original correlation ID for end-to-end tracing");

        // Verify sequential order of stage events
        var stageEvents = pipelineEvents
            .Where(e => e is QualityAnalysisCompletedEvent
                        or OcrCompletedEvent
                        or FusionCompletedEvent
                        or ClassificationCompletedEvent
                        or ExportCompletedEvent
                        or DocumentProcessingCompletedEvent)
            .Select(e => e.GetType().Name)
            .ToList();

        stageEvents.Count.ShouldBe(6, "Should have exactly 6 stage events");
        stageEvents[0].ShouldBe(nameof(QualityAnalysisCompletedEvent));
        stageEvents[1].ShouldBe(nameof(OcrCompletedEvent));
        stageEvents[2].ShouldBe(nameof(FusionCompletedEvent));
        stageEvents[3].ShouldBe(nameof(ClassificationCompletedEvent));
        stageEvents[4].ShouldBe(nameof(ExportCompletedEvent));
        stageEvents[5].ShouldBe(nameof(DocumentProcessingCompletedEvent));
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SharedEventPublisher_MultipleDocuments_ProcessedIndependently()
    {
        // Arrange
        var (qualityAnalyzer, ocrExecutor, fusionService, classifier, exporter, fileLoader) =
            CreateConfiguredMockServices();

        var completionEvents = new List<DocumentProcessingCompletedEvent>();
        var allComplete = new TaskCompletionSource<bool>();

        _sharedEventPublisher.GetEventStream<DocumentProcessingCompletedEvent>()
            .Subscribe(evt =>
            {
                completionEvents.Add(evt);
                if (completionEvents.Count >= 2)
                {
                    allComplete.TrySetResult(true);
                }
            });

        var orchestrator = new ProcessingOrchestrator(
            _sharedEventPublisher,
            NullLogger<ProcessingOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            exporter: exporter,
            fileLoader: fileLoader);

        await orchestrator.StartAsync(TestContext.Current.CancellationToken);

        var correlation1 = Guid.NewGuid();
        var correlation2 = Guid.NewGuid();
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();

        // Act - Publish two documents
        _sharedEventPublisher.Publish(new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlation1,
            FileId = fileId1,
            FileName = "doc1.png",
            Source = "SIARA",
            FileSizeBytes = 100000,
            Format = FileFormat.Pdf,
            DownloadUrl = "siara://documents/doc1"
        });

        _sharedEventPublisher.Publish(new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlation2,
            FileId = fileId2,
            FileName = "doc2.png",
            Source = "SIARA",
            FileSizeBytes = 200000,
            Format = FileFormat.Pdf,
            DownloadUrl = "siara://documents/doc2"
        });

        var completed = await Task.WhenAny(
            allComplete.Task,
            Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        // Assert
        completed.ShouldBe(allComplete.Task, "Both documents should complete within timeout");
        completionEvents.Count.ShouldBe(2, "Both documents should have completion events");

        // Verify each document has its own correlation ID
        completionEvents.ShouldContain(e => e.CorrelationId == correlation1, "Doc1 correlation missing");
        completionEvents.ShouldContain(e => e.CorrelationId == correlation2, "Doc2 correlation missing");

        // Verify each document has its own file ID
        completionEvents.ShouldContain(e => e.FileId == fileId1, "Doc1 file ID missing");
        completionEvents.ShouldContain(e => e.FileId == fileId2, "Doc2 file ID missing");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void FixtureFiles_ExistForIntegrationTesting()
    {
        // Arrange - Verify test fixtures exist for future real integration testing
        var basePath = Path.Combine(AppContext.BaseDirectory, FixtureBasePath);

        if (!Directory.Exists(basePath))
        {
            // Skip test if fixtures not available (e.g., CI environment)
            return;
        }

        // Assert - Verify fixture files exist
        var xmlPath = Path.Combine(basePath, $"{TestDocumentNumber}.xml");
        var pdfPath = Path.Combine(basePath, $"{TestDocumentNumber}.pdf");
        var docxPath = Path.Combine(basePath, $"{TestDocumentNumber}.docx");
        var imagePath = Path.Combine(basePath, $"{TestDocumentNumber}_page1.png");

        File.Exists(xmlPath).ShouldBeTrue($"XML fixture should exist at: {xmlPath}");
        File.Exists(pdfPath).ShouldBeTrue($"PDF fixture should exist at: {pdfPath}");
        File.Exists(docxPath).ShouldBeTrue($"DOCX fixture should exist at: {docxPath}");
        File.Exists(imagePath).ShouldBeTrue($"Image fixture should exist at: {imagePath}");

        // Verify files are non-empty
        new FileInfo(xmlPath).Length.ShouldBeGreaterThan(0);
        new FileInfo(pdfPath).Length.ShouldBeGreaterThan(0);
        new FileInfo(docxPath).Length.ShouldBeGreaterThan(0);
        new FileInfo(imagePath).Length.ShouldBeGreaterThan(0);
    }

    public void Dispose()
    {
        _sharedEventPublisher.Dispose();
    }

    /// <summary>
    /// Creates fully configured mock services for E2E pipeline tests.
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
                QualityLevel = ImageQualityLevel.Pristine,
                Confidence = Confidence.FromQuality(0.95),
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
            Confidence = Confidence.FromFusion(0.90),
            ConflictingFields = new List<string>(),
            NextAction = NextAction.AutoProcess,
            FusedExpediente = new ExxerCube.Prisma.Domain.Entities.Expediente
            {
                NumeroExpediente = "A/AS1-E2E-001",
                NumeroOficio = "214-1-E2E-001/2026",
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
            Level1 = ClassificationLevel1.Aseguramiento,
            Confidence = Confidence.FromInt(95)
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

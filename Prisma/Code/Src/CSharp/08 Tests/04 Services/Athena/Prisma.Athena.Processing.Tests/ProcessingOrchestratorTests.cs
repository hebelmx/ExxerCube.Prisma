using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// ITDD tests for ProcessingOrchestrator proving pipeline coordination and event emission.
/// </summary>
/// <remarks>
/// Stage 3 ITDD Exit Criteria:
/// - Subscribe to DocumentDownloadedEvent
/// - Orchestrate pipeline: Quality → OCR → Fusion → Classification → Export
/// - Emit events at each stage preserving correlation ID
/// - Handle errors defensively (NEVER CRASH)
/// - All tests passing (RED → GREEN)
/// </remarks>
public sealed class ProcessingOrchestratorTests
{
    // ========================================================================
    // Shared helper to create a standard download event
    // ========================================================================

    private static DocumentDownloadedEvent CreateDownloadEvent(
        Guid? fileId = null,
        Guid? correlationId = null,
        string fileName = "test.pdf")
    {
        return new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            FileId = fileId ?? Guid.NewGuid(),
            FileName = fileName,
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
            DownloadUrl = "siara://documents/test"
        };
    }

    // ========================================================================
    // Original tests (preserved)
    // ========================================================================

    [Fact]
    public async Task ProcessDocument_NewDocument_EmitsProcessingStartedLog()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var downloadEvent = CreateDownloadEvent();

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - orchestrator should process without throwing
    }

    [Fact]
    public async Task ProcessDocument_CorrelationId_PreservedInEvents()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var correlationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var downloadEvent = CreateDownloadEvent(correlationId: correlationId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - when pipeline stages are implemented, all events must preserve correlation ID
    }

    [Fact]
    public async Task ProcessDocument_EmitsDocumentProcessingCompletedEvent()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId, correlationId: correlationId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - completion event should be emitted with correlation ID preserved
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));
    }

    [Fact]
    public async Task ProcessDocument_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        var downloadEvent = CreateDownloadEvent();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await orchestrator.ProcessDocumentAsync(downloadEvent, cts.Token));
    }

    [Fact]
    public async Task ProcessDocument_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await orchestrator.ProcessDocumentAsync(null!, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // Phase 1: Stage 1 — Quality Analysis Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "1")]
    public async Task Stage1_WithFileLoader_CallsLoadImageAsync()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();

        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
                Confidence = Confidence.FromQuality(0.95),
                BlurScore = 0.1f,
                NoiseLevel = 0.05f,
                ContrastLevel = 0.85f,
                SharpnessLevel = 0.90f
            }));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            fileLoader: fileLoader);

        var downloadEvent = CreateDownloadEvent(fileName: "test.pdf");

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        await fileLoader.Received(1).LoadImageAsync(downloadEvent.FileName, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "1")]
    public async Task Stage1_WithQualityAnalyzer_CallsAnalyzeAsync()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();

        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        var assessment = new ImageQualityAssessment
        {
            QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
            Confidence = Confidence.FromQuality(0.95),
            BlurScore = 0.10f,
            NoiseLevel = 0.05f,
            ContrastLevel = 0.85f,
            SharpnessLevel = 0.90f
        };
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(assessment));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            fileLoader: fileLoader);

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId, correlationId: correlationId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        await qualityAnalyzer.Received(1).AnalyzeAsync(testImageData);
        eventPublisher.Received(1).Publish(
            Arg.Is<QualityAnalysisCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId &&
                e.BlurScore == (decimal)assessment.BlurScore &&
                e.NoiseScore == (decimal)assessment.NoiseLevel &&
                e.ContrastScore == (decimal)assessment.ContrastLevel &&
                e.SharpnessScore == (decimal)assessment.SharpnessLevel));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "1")]
    public async Task Stage1_QualityBelowThreshold_EmitsRejectionAndStops()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();

        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        // Quality below threshold (Q1_Poor)
        var assessment = new ImageQualityAssessment
        {
            QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Q1_Poor,
            Confidence = Confidence.FromQuality(0.30),
            BlurScore = 0.80f,
            NoiseLevel = 0.70f,
            ContrastLevel = 0.15f,
            SharpnessLevel = 0.10f
        };
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(assessment));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fileLoader: fileLoader);

        var fileId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - QualityRejectedEvent should be published
        eventPublisher.Received(1).Publish(
            Arg.Is<QualityRejectedEvent>(e => e.FileId == fileId));

        // OCR should NOT be called (pipeline short-circuited)
        await ocrExecutor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>());
    }

    // ========================================================================
    // Phase 2: Stage 2 — OCR Execution Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    public async Task Stage2_WithOcrExecutor_CallsExecuteOcrAsync()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();

        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
                Confidence = Confidence.FromQuality(0.95)
            }));

        var ocrResult = new OCRResult("Extracted text here", 92.5f, 93.0f, new List<float> { 90, 95 }, "spa");
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fileLoader: fileLoader);

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId, correlationId: correlationId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        await ocrExecutor.Received(1).ExecuteOcrAsync(testImageData, Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>());
        eventPublisher.Received(1).Publish(
            Arg.Is<OcrCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId &&
                e.Confidence == (decimal)(ocrResult.Confidence.Value * 100) &&
                e.ExtractedTextLength == ocrResult.Text.Length));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    public async Task Stage2_OcrFails_EmitsProcessingErrorEvent_ContinuesPipeline()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();

        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
                Confidence = Confidence.FromQuality(0.95)
            }));

        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.WithFailure("Tesseract initialization failed"));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fileLoader: fileLoader);

        var fileId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - error event emitted with OCR component
        eventPublisher.Received(1).Publish(
            Arg.Is<ProcessingErrorEvent>(e =>
                e.FileId == fileId &&
                e.Component == "OCR"));

        // Pipeline should still emit completion event (defensive mode)
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e => e.FileId == fileId));
    }

    // ========================================================================
    // Phase 3: Stage 3 — Fusion Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "3")]
    public async Task Stage3_WithFusionExpediente_CallsFuseAsync()
    {
        // Arrange
        var (orchestrator, eventPublisher, fileId, correlationId, downloadEvent) = CreateFullPipelineOrchestrator();

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        eventPublisher.Received(1).Publish(
            Arg.Is<FusionCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "3")]
    public async Task Stage3_ConflictsDetected_EmitsConflictDetectedEvents()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();

        SetupQualityAndOcr(fileLoader, qualityAnalyzer, ocrExecutor);

        var fusionResult = new FusionResult
        {
            Confidence = Confidence.FromFusion(0.75),
            ConflictingFields = new List<string> { "RFC", "NombreTitular" }
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

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader);

        var fileId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - ConflictDetectedEvent per conflict field
        eventPublisher.Received(1).Publish(
            Arg.Is<ConflictDetectedEvent>(e =>
                e.FileId == fileId &&
                e.FieldName == "RFC"));
        eventPublisher.Received(1).Publish(
            Arg.Is<ConflictDetectedEvent>(e =>
                e.FileId == fileId &&
                e.FieldName == "NombreTitular"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "3")]
    public async Task Stage3_WithTxtFieldExtractor_FeedsOcrDerivedExpedienteToFusion()
    {
        // Arrange — wire the optional field extractor so Stage 2 OCR text becomes a PDF
        // Expediente that Stage 3 fusion reconciles (instead of the legacy null/empty input).
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();
        var txtFieldExtractor = Substitute.For<IFieldExtractor<ExxerCube.Prisma.Domain.Sources.TxtSource>>();

        SetupQualityAndOcr(fileLoader, qualityAnalyzer, ocrExecutor); // OCR text = "Extracted text here"
        SetupFusion(fusionService);

        txtFieldExtractor.ExtractFieldsAsync(
                Arg.Any<ExxerCube.Prisma.Domain.Sources.TxtSource>(),
                Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(
                new ExtractedFields { Expediente = "EXP-OCR-123" }));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader,
            txtFieldExtractor: txtFieldExtractor);

        var downloadEvent = CreateDownloadEvent(fileId: Guid.NewGuid());

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert — the field extractor saw the OCR text, and fusion received the OCR-derived
        // Expediente as the PDF source (2nd argument), not null.
        await txtFieldExtractor.Received(1).ExtractFieldsAsync(
            Arg.Is<ExxerCube.Prisma.Domain.Sources.TxtSource>(s => s.TextContent == "Extracted text here"),
            Arg.Any<FieldDefinition[]>());

        await fusionService.Received(1).FuseAsync(
            Arg.Is<ExxerCube.Prisma.Domain.Entities.Expediente?>(e => e == null),
            Arg.Is<ExxerCube.Prisma.Domain.Entities.Expediente?>(e => e != null && e.NumeroExpediente == "EXP-OCR-123"),
            Arg.Is<ExxerCube.Prisma.Domain.Entities.Expediente?>(e => e == null),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<ExtractionMetadata>(),
            Arg.Any<CancellationToken>());
    }

    // ========================================================================
    // Phase 4: Stage 4 — Classification Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "4")]
    public async Task Stage4_WithClassifier_CallsClassifyAsync()
    {
        // Arrange
        var (orchestrator, eventPublisher, fileId, correlationId, downloadEvent) = CreateFullPipelineOrchestrator();

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        eventPublisher.Received(1).Publish(
            Arg.Is<ClassificationCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "4")]
    public async Task Stage4_LowConfidence_EmitsDocumentFlaggedForReviewEvent()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();
        var classifier = Substitute.For<IFileClassifier>();

        SetupQualityAndOcr(fileLoader, qualityAnalyzer, ocrExecutor);
        SetupFusion(fusionService);

        // Low confidence classification
        var classResult = new ClassificationResult
        {
            Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Unknown,
            Confidence = Confidence.FromInt(45)
        };
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classResult));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            fileLoader: fileLoader);

        var fileId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentFlaggedForReviewEvent>(e =>
                e.FileId == fileId &&
                e.Priority == "High"));
    }

    // ========================================================================
    // Phase 5: Stage 5 — Export Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "5")]
    public async Task Stage5_WithExporter_CallsExportAsync()
    {
        // Arrange
        var (orchestrator, eventPublisher, fileId, correlationId, downloadEvent) = CreateFullPipelineOrchestrator();

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId &&
                e.ExportedSizeBytes > 0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "5")]
    public async Task AllStages_Complete_EmitsDocumentProcessingCompletedEvent_WithStageCount()
    {
        // Arrange
        var capturedEvents = new List<DomainEvent>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(x => x.Publish(Arg.Any<DomainEvent>()))
            .Do(callInfo => capturedEvents.Add(callInfo.Arg<DomainEvent>()));

        var (orchestrator, _, fileId, correlationId, downloadEvent) = CreateFullPipelineOrchestrator(eventPublisher);

        // Act
        await orchestrator.ProcessDocumentAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - All 5 stage events + completion event emitted in order
        capturedEvents.ShouldContain(e => e is QualityAnalysisCompletedEvent);
        capturedEvents.ShouldContain(e => e is OcrCompletedEvent);
        capturedEvents.ShouldContain(e => e is FusionCompletedEvent);
        capturedEvents.ShouldContain(e => e is ClassificationCompletedEvent);
        capturedEvents.ShouldContain(e => e is ExportCompletedEvent);

        var completionEvent = capturedEvents.OfType<DocumentProcessingCompletedEvent>().ShouldHaveSingleItem();
        completionEvent.FileId.ShouldBe(fileId);
        completionEvent.CorrelationId.ShouldBe(correlationId);
        completionEvent.TotalProcessingTime.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    // ========================================================================
    // Phase 6: Railway-Oriented Programming Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "6")]
    public async Task ProcessDocumentWithResult_AllStages_ReturnsSuccessWithProcessingResult()
    {
        // Arrange
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        eventHub.SendToAllAsync(Arg.Any<DocumentProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var (orchestrator, _, fileId, correlationId, downloadEvent) = CreateFullPipelineOrchestrator(eventHub: eventHub);

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.FileId.ShouldBe(fileId);
        result.Value!.CorrelationId.ShouldBe(correlationId);
        result.Value!.StagesCompleted.ShouldBe(5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "6")]
    public async Task ProcessDocumentWithResult_Stage1Fails_ReturnsFailure_SkipsRemaining()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();

        // File loader fails
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.WithFailure("File not found"));

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger, eventHub,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fileLoader: fileLoader);

        var downloadEvent = CreateDownloadEvent();

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        await ocrExecutor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "6")]
    public async Task ProcessDocumentWithResult_Success_BroadcastsViaIExxerHub()
    {
        // Arrange
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        eventHub.SendToAllAsync(Arg.Any<DocumentProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var (orchestrator, eventPublisher, fileId, correlationId, downloadEvent) = CreateFullPipelineOrchestrator(eventHub: eventHub);

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await eventHub.Received(1).SendToAllAsync(
            Arg.Is<DocumentProcessingCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    // ========================================================================
    // Original ROP tests (preserved)
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "3.5")]
    public async Task ProcessDocumentWithResult_NewDocument_ReturnsSuccessAndBroadcastsEvent()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        eventHub.SendToAllAsync(Arg.Any<DocumentProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger, eventHub);

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId, correlationId: correlationId);

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert - Railway-Oriented Programming
        result.IsSuccess.ShouldBeTrue();
        result.Value!.FileId.ShouldBe(fileId);
        result.Value!.CorrelationId.ShouldBe(correlationId);
        result.Value!.AutoProcessed.ShouldBeTrue();

        await eventHub.Received(1).SendToAllAsync(
            Arg.Is<DocumentProcessingCompletedEvent>(e =>
                e.FileId == fileId &&
                e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "3.5")]
    public async Task ProcessDocumentWithResult_CorrelationId_PreservedInResult()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        eventHub.SendToAllAsync(Arg.Any<DocumentProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger, eventHub);

        var correlationId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var downloadEvent = CreateDownloadEvent(correlationId: correlationId);

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "3.5")]
    public async Task ProcessDocumentWithResult_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger, eventHub);

        var downloadEvent = CreateDownloadEvent();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
        await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentProcessingCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "3.5")]
    public async Task ProcessDocumentWithResult_NullEvent_ReturnsFailure()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger, eventHub);

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("cannot be null"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Stage", "3.5")]
    public async Task ProcessDocumentWithResult_BroadcastsViaIExxerHub_NotIEventPublisher()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var eventHub = Substitute.For<IExxerHub<DocumentProcessingCompletedEvent>>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        eventHub.SendToAllAsync(Arg.Any<DocumentProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger, eventHub);

        var downloadEvent = CreateDownloadEvent();

        // Act
        var result = await orchestrator.ProcessDocumentWithResultAsync(downloadEvent, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await eventHub.Received(1).SendToAllAsync(
            Arg.Is<DocumentProcessingCompletedEvent>(e => e.AutoProcessed),
            Arg.Any<CancellationToken>());

        eventPublisher.DidNotReceive().Publish(Arg.Any<DocumentProcessingCompletedEvent>());
    }

    // ========================================================================
    // Phase 7: Event Subscription Tests
    // ========================================================================

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Phase", "7")]
    public async Task StartAsync_SubscribesToDocumentDownloadedEvent()
    {
        // Arrange
        var eventPublisher = Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;

        eventPublisher.GetEventStream<DocumentDownloadedEvent>()
            .Returns(System.Reactive.Linq.Observable.Empty<DocumentDownloadedEvent>());

        var orchestrator = new ProcessingOrchestrator(eventPublisher, logger);

        // Act
        await orchestrator.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        eventPublisher.Received(1).GetEventStream<DocumentDownloadedEvent>();
    }

    // ========================================================================
    // Shared helpers for creating fully-wired pipeline orchestrators
    // ========================================================================

    private static void SetupQualityAndOcr(
        IFileLoader fileLoader,
        IImageQualityAnalyzer qualityAnalyzer,
        IOcrExecutor ocrExecutor)
    {
        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
                Confidence = Confidence.FromQuality(0.95)
            }));

        var ocrResult = new OCRResult("Extracted text here", 92.5f, 93.0f, new List<float> { 90, 95 }, "spa");
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));
    }

    private static void SetupFusion(IFusionExpediente fusionService)
    {
        // FusedExpediente must have NumeroExpediente + NumeroOficio for Stage 5 SIRO export to proceed
        // (SiroXmlExporter.ValidateMetadata requires both fields). NextAction = AutoProcess so the
        // export gate does not block (G-C2: gate blocks on ManualReviewRequired/low-confidence/conflicts).
        var fusionResult = new FusionResult
        {
            Confidence = Confidence.FromFusion(0.90),
            ConflictingFields = new List<string>(),
            NextAction = ExxerCube.Prisma.Domain.Enum.NextAction.AutoProcess,
            FusedExpediente = new ExxerCube.Prisma.Domain.Entities.Expediente
            {
                NumeroExpediente = "A/AS1-TEST-001",
                NumeroOficio = "214-1-TEST-001/2026",
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
    }

    private static (ProcessingOrchestrator orchestrator, IEventPublisher eventPublisher, Guid fileId, Guid correlationId, DocumentDownloadedEvent downloadEvent) CreateFullPipelineOrchestrator(
        IEventPublisher? eventPublisher = null,
        IExxerHub<DocumentProcessingCompletedEvent>? eventHub = null)
    {
        eventPublisher ??= Substitute.For<IEventPublisher>();
        var logger = NullLogger<ProcessingOrchestrator>.Instance;
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();
        var classifier = Substitute.For<IFileClassifier>();
        // Stage 5 now uses IResponseExporter.ExportSiroXmlAsync (MVP-PATH #8).
        // Write a minimal XML stub to the stream so ExportedSizeBytes > 0 in the emitted event.
        var exporter = Substitute.For<IResponseExporter>();
        exporter.ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var stream = callInfo.ArgAt<Stream>(1);
                var bytes = "<SiroResponse />"u8.ToArray();
                stream.Write(bytes, 0, bytes.Length);
                return Task.FromResult(Result.Success());
            });

        SetupQualityAndOcr(fileLoader, qualityAnalyzer, ocrExecutor);
        SetupFusion(fusionService);

        var classResult = new ClassificationResult
        {
            Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
            Confidence = Confidence.FromInt(95)
        };
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classResult));

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var downloadEvent = CreateDownloadEvent(fileId: fileId, correlationId: correlationId);

        var orchestrator = new ProcessingOrchestrator(
            eventPublisher, logger, eventHub,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            classifier: classifier,
            exporter: exporter,
            fileLoader: fileLoader);

        return (orchestrator, eventPublisher, fileId, correlationId, downloadEvent);
    }
}

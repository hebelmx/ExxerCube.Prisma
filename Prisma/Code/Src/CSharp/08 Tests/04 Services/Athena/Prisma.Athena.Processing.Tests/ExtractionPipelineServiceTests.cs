using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="ExtractionPipelineService"/> (MVP-PATH 1.4 Reconciliator edge): on a downloaded
/// document it runs the Extractor half, persists the fused expediente as a shared-storage handoff, and
/// broadcasts an <see cref="ExtractionCompletedEvent"/> carrying the handoff path — and degrades cleanly when
/// quality rejects or no expediente is produced.
/// </summary>
public sealed class ExtractionPipelineServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DocumentDownloadedEvent CreateDownloadEvent(Guid? fileId = null, Guid? correlationId = null) => new()
    {
        FileId = fileId ?? Guid.NewGuid(),
        CorrelationId = correlationId ?? Guid.NewGuid(),
        FileName = "test.pdf",
        Source = "SIARA",
        Format = ExxerCube.Prisma.Domain.Enum.FileFormat.Pdf,
    };

    private static void SetupQualityAndOcr(
        IFileLoader fileLoader,
        IImageQualityAnalyzer qualityAnalyzer,
        IOcrExecutor ocrExecutor,
        ExxerCube.Prisma.Domain.Enum.ImageQualityLevel level)
    {
        var testImageData = new ImageData(new byte[] { 1, 2, 3 }, "test.pdf");
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(testImageData));

        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = level,
                Confidence = 0.95f
            }));

        var ocrResult = new OCRResult("Extracted text here", 92.5f, 93.0f, new List<float> { 90, 95 }, "spa");
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));
    }

    private static void SetupFusion(IFusionExpediente fusionService, Expediente? fused)
    {
        fusionService.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(new FusionResult
            {
                OverallConfidence = 0.90,
                ConflictingFields = new List<string>(),
                FusedExpediente = fused
            }));
    }

    private static (ExtractionPipelineService sut, IExpedienteHandoffStore store, IExxerHub<ExtractionCompletedEvent> hub)
        CreateSut(ExxerCube.Prisma.Domain.Enum.ImageQualityLevel level, Expediente? fused)
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var fileLoader = Substitute.For<IFileLoader>();
        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var fusionService = Substitute.For<IFusionExpediente>();

        SetupQualityAndOcr(fileLoader, qualityAnalyzer, ocrExecutor, level);
        SetupFusion(fusionService, fused);

        var orchestrator = new ExtractionOrchestrator(
            eventPublisher,
            NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader);

        var store = Substitute.For<IExpedienteHandoffStore>();
        store.SaveAsync(Arg.Any<Expediente>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result<string>.Success(ci.ArgAt<string>(1)));

        var hub = Substitute.For<IExxerHub<ExtractionCompletedEvent>>();
        hub.SendToAllAsync(Arg.Any<ExtractionCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var sut = new ExtractionPipelineService(
            eventPublisher, orchestrator, store, hub, NullLogger<ExtractionPipelineService>.Instance);
        return (sut, store, hub);
    }

    [Fact]
    public async Task ProcessAsync_WithFusedExpediente_SavesHandoffAndBroadcasts()
    {
        var fileId = Guid.NewGuid();
        var (sut, store, hub) = CreateSut(
            ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
            new Expediente { NumeroExpediente = "EXP-OCR-123" });

        var result = await sut.ProcessAsync(CreateDownloadEvent(fileId: fileId), Ct);

        result.IsSuccess.ShouldBeTrue();
        await store.Received(1).SaveAsync(
            Arg.Is<Expediente>(e => e.NumeroExpediente == "EXP-OCR-123"),
            Arg.Is<string>(p => p.EndsWith(".fusion.json") && p.Contains(fileId.ToString())),
            Arg.Any<CancellationToken>());
        await hub.Received(1).SendToAllAsync(
            Arg.Is<ExtractionCompletedEvent>(e => e.FileId == fileId && e.Path.EndsWith(".fusion.json")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_QualityRejected_DoesNotHandoff()
    {
        var (sut, store, hub) = CreateSut(
            ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Q1_Poor,
            new Expediente { NumeroExpediente = "EXP" });

        var result = await sut.ProcessAsync(CreateDownloadEvent(), Ct);

        result.IsSuccess.ShouldBeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<Expediente>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await hub.DidNotReceive().SendToAllAsync(Arg.Any<ExtractionCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NoFusedExpediente_DoesNotHandoff()
    {
        var (sut, store, hub) = CreateSut(
            ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
            fused: null);

        var result = await sut.ProcessAsync(CreateDownloadEvent(), Ct);

        result.IsSuccess.ShouldBeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<Expediente>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await hub.DidNotReceive().SendToAllAsync(Arg.Any<ExtractionCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NullEvent_ReturnsFailure()
    {
        var (sut, _, _) = CreateSut(ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine, new Expediente());

        var result = await sut.ProcessAsync(null!, Ct);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessAsync_StorePersistFails_ReturnsFailure_NoBroadcast()
    {
        var (sut, store, hub) = CreateSut(
            ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
            new Expediente { NumeroExpediente = "EXP" });
        store.SaveAsync(Arg.Any<Expediente>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.WithFailure("disk full"));

        var result = await sut.ProcessAsync(CreateDownloadEvent(), Ct);

        result.IsFailure.ShouldBeTrue();
        await hub.DidNotReceive().SendToAllAsync(Arg.Any<ExtractionCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Story 2.1b: the Expediente saved to the handoff store must carry the OCR body text in
    /// <see cref="ExxerCube.Prisma.Domain.Entities.Expediente.BodyText"/> so the Reconciliator
    /// process can feed it into Stage-4 keyword scoring without the raw OCRResult crossing the
    /// process boundary (ADR-011 approved 2026-06-25).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WithOcrResult_SetsBodyTextOnExpedienteBeforeHandoff()
    {
        // "Extracted text here" is the OCR text returned by SetupQualityAndOcr.
        const string expectedBodyText = "Extracted text here";

        Expediente? capturedExpediente = null;

        var (sut, store, _) = CreateSut(
            ExxerCube.Prisma.Domain.Enum.ImageQualityLevel.Pristine,
            new Expediente { NumeroExpediente = "EXP-BODY-123" });

        store.SaveAsync(Arg.Any<Expediente>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedExpediente = ci.ArgAt<Expediente>(0);
                return Result<string>.Success(ci.ArgAt<string>(1));
            });

        var result = await sut.ProcessAsync(CreateDownloadEvent(), Ct);

        result.IsSuccess.ShouldBeTrue();
        capturedExpediente.ShouldNotBeNull("SaveAsync must have been called");
        capturedExpediente!.BodyText.ShouldBe(expectedBodyText,
            "BodyText must equal the OCR text so the Reconciliator can keyword-score it");
    }
}

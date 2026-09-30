using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="HybridExtractionService"/>.
/// All extractors, the PDF converter, and the reconciler are mocked — no real I/O or LLM calls.
/// LLM tracks are now mocked via <see cref="ILlmExpedienteExtractor{T}"/> (not IFieldExtractor),
/// which makes them trivially mockable and ensures <c>SolicitudPartes</c> can be asserted.
/// </summary>
public sealed class HybridExtractionServiceTests
{
    // ───────────────────────────────────────────────────────────────────────
    // Test fixtures
    // ───────────────────────────────────────────────────────────────────────

    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // "%PDF"
    private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47];     // PNG magic
    private const string DocId = "test-doc-001";

    // ───────────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────────

    private static ExtractedFields MakeDetFields(string expediente = "123/2024", string ocrText = "OCR text sample")
    {
        var f = new ExtractedFields { Expediente = expediente };
        f.AdditionalFields["_OcrText"] = ocrText;
        f.AdditionalFields["_OcrConfidence"] = "0.92";
        return f;
    }

    private static Expediente MakeLlmExpediente(string expediente = "123/2024")
    {
        var e = new Expediente { NumeroExpediente = expediente };
        e.AdditionalFields["NombreSolicitante"] = "Juan Pérez";
        e.AdditionalFields["_ExtractionSource"] = "llm-text";
        return e;
    }

    private static Expediente MakeVisionExpediente(string expediente = "123/2024")
    {
        var e = new Expediente { NumeroExpediente = expediente };
        e.AdditionalFields["_ExtractionSource"] = "llm-vision";
        return e;
    }

    private static ReconciliationResult FakeReconciliation(IReadOnlyList<LabelledExtraction> candidates) =>
        new(new Expediente(), candidates, Array.Empty<string>());

    private static IOptionsMonitor<LlmProvidersOptions> MakeOptions(
        bool textEnabled = false,
        bool visionEnabled = false)
    {
        var opts = Substitute.For<IOptionsMonitor<LlmProvidersOptions>>();
        opts.CurrentValue.Returns(new LlmProvidersOptions
        {
            TextExtractorEnabled = textEnabled,
            VisionExtractorEnabled = visionEnabled,
        });
        return opts;
    }

    private static IPdfToImageConverter MakeConverter(bool success = true)
    {
        var converter = Substitute.For<IPdfToImageConverter>();
        if (success)
        {
            converter
                .ConvertToImagesAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result<IReadOnlyList<byte[]>>.WithSuccess(
                    new List<byte[]> { FakePng })));
        }
        else
        {
            converter
                .ConvertToImagesAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result<IReadOnlyList<byte[]>>.WithFailure("Conversion failed")));
        }

        return converter;
    }

    private static IExtractionReconciler MakeReconciler()
    {
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Any<IReadOnlyList<LabelledExtraction>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var list = (IReadOnlyList<LabelledExtraction>)ci[0];
                return Task.FromResult(Result<ReconciliationResult>.WithSuccess(FakeReconciliation(list)));
            });
        return reconciler;
    }

    private HybridExtractionService Build(
        IFieldExtractor<PdfSource>? det = null,
        ILlmExpedienteExtractor<TxtSource>? llmText = null,
        ILlmExpedienteExtractor<ImageSource>? llmVision = null,
        IPdfToImageConverter? converter = null,
        IExtractionReconciler? reconciler = null,
        IOptionsMonitor<LlmProvidersOptions>? options = null,
        ITestOutputHelper? output = null)
    {
        det ??= Substitute.For<IFieldExtractor<PdfSource>>();
        llmText ??= Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmVision ??= Substitute.For<ILlmExpedienteExtractor<ImageSource>>();
        converter ??= MakeConverter();
        reconciler ??= MakeReconciler();
        options ??= MakeOptions();
        var logger = XUnitLogger.CreateLogger<HybridExtractionService>(output!);
        return new HybridExtractionService(det, llmText, llmVision, converter, reconciler, options, logger);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Guard rails
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_NullBytes_ReturnsFailure()
    {
        var sut = Build();
        var result = await sut.ExtractAsync(null!, DocId, visionModelOverride: null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractAsync_EmptyBytes_ReturnsFailure()
    {
        var sut = Build();
        var result = await sut.ExtractAsync([], DocId, visionModelOverride: null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractAsync_EmptyDocumentId_ReturnsFailure()
    {
        var sut = Build();
        var result = await sut.ExtractAsync(FakePdfBytes, string.Empty, visionModelOverride: null, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeFalse();
    }

    // ───────────────────────────────────────────────────────────────────────
    // Flag routing: only deterministic when both flags are off
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_BothFlagsOff_OnlyDeterministicCandidatePassedToReconciler()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var reconciler = MakeReconciler();
        var sut = Build(det: det, reconciler: reconciler, options: MakeOptions(false, false));

        // Act
        var result = await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        // Assert — exactly 1 candidate (deterministic only)
        result.IsSuccess.ShouldBeTrue();
        await reconciler.Received(1).ReconcileAsync(
            Arg.Is<IReadOnlyList<LabelledExtraction>>(c => c.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_BothFlagsOff_CandidateSourceIsDeterministic()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, reconciler: reconciler, options: MakeOptions(false, false));
        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        captured.ShouldNotBeNull();
        captured!.Count.ShouldBe(1);
        captured[0].Source.ShouldBe("deterministic");
        captured[0].Status.ShouldBe(TrackStatus.Available);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Flag routing: TextExtractorEnabled only
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_TextFlagOn_TwoCandidatesPassedToReconciler()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(Arg.Any<TxtSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(MakeLlmExpediente())));

        var reconciler = MakeReconciler();
        var sut = Build(det: det, llmText: llmText, reconciler: reconciler,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        await reconciler.Received(1).ReconcileAsync(
            Arg.Is<IReadOnlyList<LabelledExtraction>>(c => c.Count == 2),
            Arg.Any<CancellationToken>());
    }

    // ───────────────────────────────────────────────────────────────────────
    // Flag routing: both flags on
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_BothFlagsOn_ThreeCandidatesPassedToReconciler()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(Arg.Any<TxtSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(MakeLlmExpediente())));

        var llmVision = Substitute.For<ILlmExpedienteExtractor<ImageSource>>();
        llmVision.ExtractExpedienteAsync(Arg.Any<ImageSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(MakeVisionExpediente())));

        var reconciler = MakeReconciler();
        var sut = Build(det: det, llmText: llmText, llmVision: llmVision,
            reconciler: reconciler, options: MakeOptions(textEnabled: true, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        await reconciler.Received(1).ReconcileAsync(
            Arg.Is<IReadOnlyList<LabelledExtraction>>(c => c.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_BothFlagsOn_CandidateSourcesCorrect()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(Arg.Any<TxtSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(MakeLlmExpediente())));

        var llmVision = Substitute.For<ILlmExpedienteExtractor<ImageSource>>();
        llmVision.ExtractExpedienteAsync(Arg.Any<ImageSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(MakeVisionExpediente())));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmText: llmText, llmVision: llmVision,
            reconciler: reconciler, options: MakeOptions(textEnabled: true, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        captured.ShouldNotBeNull();
        captured!.Select(c => c.Source).ShouldBe(["deterministic", "llm-text", "llm-vision"], ignoreOrder: false);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Honesty: failed tracks are NOT dropped — they are passed as Failed status
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_LlmTextFails_FailedCandidateStillPassedToReconciler()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(Arg.Any<TxtSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithFailure("LLM timeout")));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmText: llmText, reconciler: reconciler,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        // Reconciler must still receive 2 candidates (det + failed llm-text)
        await reconciler.Received(1).ReconcileAsync(
            Arg.Is<IReadOnlyList<LabelledExtraction>>(c => c.Count == 2),
            Arg.Any<CancellationToken>());

        captured.ShouldNotBeNull();
        var llmCand = captured!.FirstOrDefault(c => c.Source == "llm-text");
        llmCand.ShouldNotBeNull();
        llmCand!.Status.ShouldBe(TrackStatus.Failed);
        llmCand.Fields.ShouldBeNull();
    }

    // ───────────────────────────────────────────────────────────────────────
    // Deterministic failure is surfaced (Failed status in candidate list)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_DeterministicFails_FailedCandidatePassedAndResultIndicatesIssue()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithFailure("OCR engine crashed")));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, reconciler: reconciler, options: MakeOptions(false, false));

        var result = await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        // Overall orchestration succeeds (reconciler decided); deterministic candidate is Failed
        result.IsSuccess.ShouldBeTrue();

        captured.ShouldNotBeNull();
        var detCand = captured!.FirstOrDefault(c => c.Source == "deterministic");
        detCand.ShouldNotBeNull();
        detCand!.Status.ShouldBe(TrackStatus.Failed);
        detCand.Fields.ShouldBeNull();
    }

    // ───────────────────────────────────────────────────────────────────────
    // LLM-text track receives the OCR text from the deterministic result
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_TextFlagOn_LlmTextReceivesOcrTextFromDeterministicResult()
    {
        var ct = TestContext.Current.CancellationToken;
        const string expectedOcrText = "CNBV Oficio Núm. 222/AAA 2025";

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields(ocrText: expectedOcrText))));

        TxtSource? capturedSource = null;
        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(Arg.Do<TxtSource>(s => capturedSource = s), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(new Expediente())));

        var sut = Build(det: det, llmText: llmText,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        capturedSource.ShouldNotBeNull();
        capturedSource!.TextContent.ShouldBe(expectedOcrText);
    }

    // ───────────────────────────────────────────────────────────────────────
    // LLM-vision track uses the correct DocumentId
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_VisionFlagOn_ImageSourceHasCorrectDocumentId()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        ImageSource? capturedSource = null;
        var llmVision = Substitute.For<ILlmExpedienteExtractor<ImageSource>>();
        llmVision.ExtractExpedienteAsync(Arg.Do<ImageSource>(s => capturedSource = s), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(new Expediente())));

        var sut = Build(det: det, llmVision: llmVision,
            options: MakeOptions(textEnabled: false, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        capturedSource.ShouldNotBeNull();
        capturedSource!.DocumentId.ShouldBe(DocId);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Runtime vision-model override is threaded to the vision track ONLY
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_VisionModelOverride_IsThreadedToVisionTrackOnly()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        // Sentinel start values distinguish "received null" from "never called".
        string? capturedVisionModel = "SENTINEL";
        var llmVision = Substitute.For<ILlmExpedienteExtractor<ImageSource>>();
        llmVision.ExtractExpedienteAsync(
                Arg.Any<ImageSource>(),
                Arg.Do<string?>(m => capturedVisionModel = m),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(new Expediente())));

        string? capturedTextModel = "SENTINEL";
        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(
                Arg.Any<TxtSource>(),
                Arg.Do<string?>(m => capturedTextModel = m),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(new Expediente())));

        var sut = Build(det: det, llmText: llmText, llmVision: llmVision,
            options: MakeOptions(textEnabled: true, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: "granite3.2-vision", ct);

        capturedVisionModel.ShouldBe("granite3.2-vision");   // vision track gets the override
        capturedTextModel.ShouldBeNull();                    // text track must NOT (stays configured default)
    }

    // ───────────────────────────────────────────────────────────────────────
    // Vision SkippedNoCapability vs. Failed distinction
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_VisionProviderNoCapability_CandidateIsSkippedNoCapability()
    {
        var ct = TestContext.Current.CancellationToken;

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var llmVision = Substitute.For<ILlmExpedienteExtractor<ImageSource>>();
        llmVision.ExtractExpedienteAsync(Arg.Any<ImageSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithFailure(
                "Active provider 'Ollama' does not support vision (VisionGenerate capability is required)")));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmVision: llmVision,
            reconciler: reconciler, options: MakeOptions(textEnabled: false, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        captured.ShouldNotBeNull();
        var visCand = captured!.FirstOrDefault(c => c.Source == "llm-vision");
        visCand.ShouldNotBeNull();
        visCand!.Status.ShouldBe(TrackStatus.SkippedNoCapability);
    }

    // ───────────────────────────────────────────────────────────────────────
    // ExtractedFields → Expediente mapping (key fields, deterministic track)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_DeterministicReturnsRichFields_ExpedienteIsMappedCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;

        var richFields = new ExtractedFields { Expediente = "A/AS1-1111-222222-AAA" };
        richFields.AdditionalFields["_OcrText"] = "some ocr";
        richFields.AdditionalFields["NumeroOficio"] = "222/AAA/-4444444444/2025";
        richFields.AdditionalFields["AutoridadNombre"] = "SUBDELEGACION 8 SAN ANGEL";
        richFields.AdditionalFields["NombreSolicitante"] = "PEPE TOÑO PALOMA FLORES";
        richFields.AdditionalFields["DiasPlazo"] = "7";
        richFields.AdditionalFields["TieneAseguramiento"] = "True";

        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(richFields)));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, reconciler: reconciler, options: MakeOptions(false, false));
        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        captured.ShouldNotBeNull();
        var detExpediente = captured![0].Fields;
        detExpediente.ShouldNotBeNull();
        detExpediente!.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        detExpediente.NumeroOficio.ShouldBe("222/AAA/-4444444444/2025");
        detExpediente.AutoridadNombre.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
        detExpediente.NombreSolicitante.ShouldBe("PEPE TOÑO PALOMA FLORES");
        detExpediente.DiasPlazo.ShouldBe(7);
        detExpediente.TieneAseguramiento.ShouldBeTrue();
    }

    // ───────────────────────────────────────────────────────────────────────
    // KEY: SolicitudPartes survive through the llm-text candidate
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_LlmTextReturnsExpedienteWithPartes_PartesPreservedInCandidate()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — deterministic provides OCR text; llm-text returns Expediente with 2 partes.
        var det = Substitute.For<IFieldExtractor<PdfSource>>();
        det.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeDetFields())));

        var expedienteWithPartes = new Expediente { NumeroExpediente = "123/2024" };
        expedienteWithPartes.SolicitudPartes.Add(new SolicitudParte
        {
            ParteId = 1,
            Nombre = "Juan Pérez García",
            Rfc = "PEJJ800101AAA",
            Caracter = "Contribuyente",
        });
        expedienteWithPartes.SolicitudPartes.Add(new SolicitudParte
        {
            ParteId = 2,
            Nombre = "María López Ruiz",
            Rfc = "LOMM850202BBB",
            Caracter = "Patrón",
        });

        var llmText = Substitute.For<ILlmExpedienteExtractor<TxtSource>>();
        llmText.ExtractExpedienteAsync(Arg.Any<TxtSource>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Expediente>.WithSuccess(expedienteWithPartes)));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmText: llmText, reconciler: reconciler,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        // Act
        await sut.ExtractAsync(FakePdfBytes, DocId, visionModelOverride: null, ct);

        // Assert — 2 partes survive into the llm-text candidate without being dropped
        captured.ShouldNotBeNull();
        var llmCand = captured!.FirstOrDefault(c => c.Source == "llm-text");
        llmCand.ShouldNotBeNull();
        llmCand!.Status.ShouldBe(TrackStatus.Available);
        llmCand.Fields.ShouldNotBeNull();
        llmCand.Fields!.SolicitudPartes.Count.ShouldBe(2);
        llmCand.Fields.SolicitudPartes[0].Nombre.ShouldBe("Juan Pérez García");
        llmCand.Fields.SolicitudPartes[1].Rfc.ShouldBe("LOMM850202BBB");
    }
}

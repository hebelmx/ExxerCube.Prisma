using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="HybridExtractionService"/>.
/// All extractors, the PDF converter, and the reconciler are mocked — no real I/O or LLM calls.
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

    private static ExtractedFields MakeLlmFields(string expediente = "123/2024")
    {
        var f = new ExtractedFields { Expediente = expediente };
        f.AdditionalFields["NombreSolicitante"] = "Juan Pérez";
        f.AdditionalFields["_ExtractionSource"] = "llm-text";
        return f;
    }

    private static ExtractedFields MakeVisionFields(string expediente = "123/2024")
    {
        var f = new ExtractedFields { Expediente = expediente };
        f.AdditionalFields["_ExtractionSource"] = "llm-vision";
        return f;
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
        IFieldExtractor<TxtSource>? llmText = null,
        IFieldExtractor<ImageSource>? llmVision = null,
        IPdfToImageConverter? converter = null,
        IExtractionReconciler? reconciler = null,
        IOptionsMonitor<LlmProvidersOptions>? options = null,
        ITestOutputHelper? output = null)
    {
        det ??= Substitute.For<IFieldExtractor<PdfSource>>();
        llmText ??= Substitute.For<IFieldExtractor<TxtSource>>();
        llmVision ??= Substitute.For<IFieldExtractor<ImageSource>>();
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
        var result = await sut.ExtractAsync(null!, DocId, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractAsync_EmptyBytes_ReturnsFailure()
    {
        var sut = Build();
        var result = await sut.ExtractAsync([], DocId, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractAsync_EmptyDocumentId_ReturnsFailure()
    {
        var sut = Build();
        var result = await sut.ExtractAsync(FakePdfBytes, string.Empty, TestContext.Current.CancellationToken);
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
        var result = await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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
        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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

        var llmText = Substitute.For<IFieldExtractor<TxtSource>>();
        llmText.ExtractFieldsAsync(Arg.Any<TxtSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeLlmFields())));

        var reconciler = MakeReconciler();
        var sut = Build(det: det, llmText: llmText, reconciler: reconciler,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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

        var llmText = Substitute.For<IFieldExtractor<TxtSource>>();
        llmText.ExtractFieldsAsync(Arg.Any<TxtSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeLlmFields())));

        var llmVision = Substitute.For<IFieldExtractor<ImageSource>>();
        llmVision.ExtractFieldsAsync(Arg.Any<ImageSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeVisionFields())));

        var reconciler = MakeReconciler();
        var sut = Build(det: det, llmText: llmText, llmVision: llmVision,
            reconciler: reconciler, options: MakeOptions(textEnabled: true, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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

        var llmText = Substitute.For<IFieldExtractor<TxtSource>>();
        llmText.ExtractFieldsAsync(Arg.Any<TxtSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeLlmFields())));

        var llmVision = Substitute.For<IFieldExtractor<ImageSource>>();
        llmVision.ExtractFieldsAsync(Arg.Any<ImageSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeVisionFields())));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmText: llmText, llmVision: llmVision,
            reconciler: reconciler, options: MakeOptions(textEnabled: true, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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

        var llmText = Substitute.For<IFieldExtractor<TxtSource>>();
        llmText.ExtractFieldsAsync(Arg.Any<TxtSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithFailure("LLM timeout")));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmText: llmText, reconciler: reconciler,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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

        var result = await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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
        var llmText = Substitute.For<IFieldExtractor<TxtSource>>();
        llmText.ExtractFieldsAsync(Arg.Do<TxtSource>(s => capturedSource = s), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeLlmFields())));

        var sut = Build(det: det, llmText: llmText,
            options: MakeOptions(textEnabled: true, visionEnabled: false));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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
        var llmVision = Substitute.For<IFieldExtractor<ImageSource>>();
        llmVision.ExtractFieldsAsync(Arg.Do<ImageSource>(s => capturedSource = s), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithSuccess(MakeVisionFields())));

        var sut = Build(det: det, llmVision: llmVision,
            options: MakeOptions(textEnabled: false, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

        capturedSource.ShouldNotBeNull();
        capturedSource!.DocumentId.ShouldBe(DocId);
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

        var llmVision = Substitute.For<IFieldExtractor<ImageSource>>();
        llmVision.ExtractFieldsAsync(Arg.Any<ImageSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Task.FromResult(Result<ExtractedFields>.WithFailure(
                "Active provider 'Ollama' does not support vision (VisionGenerate capability is required)")));

        IReadOnlyList<LabelledExtraction>? captured = null;
        var reconciler = Substitute.For<IExtractionReconciler>();
        reconciler
            .ReconcileAsync(Arg.Do<IReadOnlyList<LabelledExtraction>>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result<ReconciliationResult>.WithSuccess(
                FakeReconciliation((IReadOnlyList<LabelledExtraction>)ci[0]))));

        var sut = Build(det: det, llmVision: llmVision,
            reconciler: reconciler, options: MakeOptions(textEnabled: false, visionEnabled: true));

        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

        captured.ShouldNotBeNull();
        var visCand = captured!.FirstOrDefault(c => c.Source == "llm-vision");
        visCand.ShouldNotBeNull();
        visCand!.Status.ShouldBe(TrackStatus.SkippedNoCapability);
    }

    // ───────────────────────────────────────────────────────────────────────
    // ExtractedFields → Expediente mapping (key fields)
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
        await sut.ExtractAsync(FakePdfBytes, DocId, ct);

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
}

using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// T1-style gated unit tests for <see cref="HeaderImageOcrStage"/>: exercises the render-crop →
/// OCR → regex-match → abstention pipeline against a FAKE <see cref="IHeaderProductOcrEngine"/>
/// (no real Tesseract call, design doc <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §6
/// "T1 — gated snapshot test"). The fake engine's canned return value stands in for a committed
/// OCR snapshot — the assertion is on the stage's regex-match/abstention behavior given a known
/// OCR-text input, independent of whether Tesseract itself is available in this CI lane.
/// </summary>
public sealed class HeaderImageOcrStageTests
{
    // -----------------------------------------------------------------------
    // Synthetic single-page PDF (content is irrelevant — the fake engine supplies OCR text
    // directly, so the render/crop path just needs a valid, renderable PDF).
    // -----------------------------------------------------------------------

    private static byte[] BuildOnePagePdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        page.AddText("Estado de cuenta — placeholder content", 12, new PdfPoint(50, 750), font);
        return builder.Build();
    }

    private static FieldResolutionContext<string> BuildContext(byte[] pdfBytes) =>
        new(
            FieldKind.Product,
            pdfBytes,
            new LazyPdfCorpus(pdfBytes),
            Array.Empty<FieldCandidate<string>>(),
            new StageBudget());

    private static HeaderImageOcrStage CreateStage(IHeaderProductOcrEngine engine) =>
        new(engine, NullLogger<HeaderImageOcrStage>.Instance);

    // -----------------------------------------------------------------------
    // Stage identity
    // -----------------------------------------------------------------------

    [Fact]
    public void Stage_IsHeaderImageOcr()
    {
        var stage = CreateStage(Substitute.For<IHeaderProductOcrEngine>());

        stage.Stage.ShouldBe(StageId.HeaderImageOcr);
    }

    // -----------------------------------------------------------------------
    // Happy path — clean OCR read recognizes the product heading
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryResolveAsync_CleanProductHeading_ReturnsFoundWithNormalizedValue()
    {
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess("Tarjeta de Crédito COSTCO BANAMEX")));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsSuccess.ShouldBeTrue();
        var candidate = result.Value!;
        candidate.HasValue.ShouldBeTrue();
        candidate.Value.ShouldBe("Tarjeta de Crédito COSTCO BANAMEX");
        candidate.Stage.ShouldBe(StageId.HeaderImageOcr);
    }

    [Fact]
    public async Task TryResolveAsync_HeadingEmbeddedInNoisierText_MatchesSameLineOnly()
    {
        // Simulates OCR noise above/below the heading (bank logo text, address block) — the
        // regex must extract only the heading's own line, not bleed into surrounding prose.
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess(
                "BANAMEX\nTarjeta de Crédito COSTCO BANAMEX\nEstado de cuenta")));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Tarjeta de Crédito COSTCO BANAMEX");
    }

    [Fact]
    public async Task TryResolveAsync_AccentDroppedByOcr_StillMatches()
    {
        // Tesseract occasionally drops the accent on "Crédito" — the regex tolerates both forms.
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess("Tarjeta de Credito COSTCO BANAMEX")));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasValue.ShouldBeTrue();
        result.Value.Value.ShouldBe("Tarjeta de Credito COSTCO BANAMEX");
    }

    // -----------------------------------------------------------------------
    // Honesty discipline — abstain, never fabricate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryResolveAsync_OcrTextDoesNotContainHeadingShape_ReturnsNone()
    {
        // "Alias/fallback-starved" fixture: OCR ran and returned real text, but none of it is a
        // product-heading shape — the stage must abstain, never fabricate a Found candidate.
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess("Estado de cuenta — Banco Ejemplo")));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasValue.ShouldBeFalse();
        result.Value.Stage.ShouldBe(StageId.HeaderImageOcr);
    }

    [Fact]
    public async Task TryResolveAsync_EmptyOcrText_ReturnsNone()
    {
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithSuccess(string.Empty)));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task TryResolveAsync_OcrEngineFails_ReturnsNoneNotFailure()
    {
        // Infrastructure failure inside the OCR engine must not fail the whole field resolution —
        // it degrades to an honest abstention so the document can still be processed.
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.WithFailure("native engine crashed")));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasValue.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryResolveAsync_PreCancelledToken_ReturnsCancelledWithoutCallingEngine()
    {
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        var stage = CreateStage(engine);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await engine.DidNotReceive().RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryResolveAsync_EngineReportsCancelled_PropagatesCancelled()
    {
        var engine = Substitute.For<IHeaderProductOcrEngine>();
        engine.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ResultExtensions.Cancelled<string>()));

        var stage = CreateStage(engine);
        var ct = TestContext.Current.CancellationToken;

        var result = await stage.TryResolveAsync(BuildContext(BuildOnePagePdf()), ct);

        result.IsCancelled().ShouldBeTrue();
    }
}

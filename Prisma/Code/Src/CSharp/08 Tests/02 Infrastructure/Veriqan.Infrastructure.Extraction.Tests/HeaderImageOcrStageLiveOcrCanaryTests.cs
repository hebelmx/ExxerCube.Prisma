using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// T2 — LiveOcr canary (design doc <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §6):
/// invokes the REAL <see cref="TesseractHeaderProductOcrEngine"/> against the shared demo
/// header crop and asserts a genuine, provenance-tagged recovery. Non-gating in the sense that a
/// native-Tesseract-unavailable box would fail loudly here rather than silently in T1 — but this
/// suite is expected to run green wherever real Tesseract + <c>spa</c> tessdata are available
/// (this machine: <c>Extraction.Teseract</c> already runs 154/154 against real Tesseract per
/// CLAUDE.md's live-verification note).
/// </summary>
/// <remarks>
/// Runs the real singleton engine, so this class belongs to
/// <see cref="VeriqanHeaderOcrCollection"/> to serialize with every other test class that
/// constructs it (the native engine deadlocks on a second concurrent instantiation).
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(VeriqanHeaderOcrCollection.Name)]
public sealed class HeaderImageOcrStageLiveOcrCanaryTests
{
    private static readonly string GoodPdfPath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", "good.pdf");

    private static readonly TesseractHeaderProductOcrEngine OcrEngine =
        new(NullLogger<TesseractHeaderProductOcrEngine>.Instance);

    // -----------------------------------------------------------------------
    // T2 — direct stage-level canary: real OCR reads the header crop cleanly
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryResolveAsync_RealOcrOnGoodPdf_RecognizesCostcoBanamexHeading()
    {
        var ct = TestContext.Current.CancellationToken;
        if (!File.Exists(GoodPdfPath))
            Assert.Skip($"Demo corpus fixture not found at: {GoodPdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(GoodPdfPath, ct);
        var stage = new HeaderImageOcrStage(OcrEngine, NullLogger<HeaderImageOcrStage>.Instance);

        var context = new FieldResolutionContext<string>(
            FieldKind.Product,
            pdfBytes,
            new LazyPdfCorpus(pdfBytes),
            Array.Empty<FieldCandidate<string>>(),
            new StageBudget());

        var result = await stage.TryResolveAsync(context, ct);

        result.IsSuccess.ShouldBeTrue($"HeaderImageOcrStage failed: {result.Error}");
        var candidate = result.Value!;

        candidate.HasValue.ShouldBeTrue(
            "Real Tesseract OCR over good.pdf's page-1 header crop must recognize the product "
            + "heading (confirmed GO by the S7.0 spike) — if this fails, either the crop-band "
            + "fraction drifted off the heading or native Tesseract/tessdata is unavailable.");
        candidate.Value!.ShouldContain("COSTCO BANAMEX",
            customMessage: $"Recognized text was: '{candidate.Value}'");
        candidate.Stage.ShouldBe(StageId.HeaderImageOcr);
    }

    // -----------------------------------------------------------------------
    // Provenance — end-to-end through the escalating extractor on real good.pdf
    // -----------------------------------------------------------------------

    /// <summary>
    /// Dedicated provenance proof (DoD requirement): resolves good.pdf's Product field through
    /// the full <see cref="EscalatingStatementFieldExtractor"/> seam (positional → OCR ladder)
    /// and asserts the value came from <see cref="StageId.HeaderImageOcr"/>, not a reintroduced
    /// positional shortcut.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_GoodPdf_ProductProvenanceIsHeaderImageOcr()
    {
        var ct = TestContext.Current.CancellationToken;
        if (!File.Exists(GoodPdfPath))
            Assert.Skip($"Demo corpus fixture not found at: {GoodPdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(GoodPdfPath, ct);

        var inner = new PdfPigStatementFieldExtractor(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

        var stageProvider = new DefaultFieldStageProvider(OcrEngine, NullLoggerFactory.Instance);
        var orchestrator = new FieldResolutionOrchestrator(
            new FieldEscalationLadderRegistry(),
            stageProvider,
            XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());
        // No referenceBundle is ever passed by this test's ExtractFullAsync call, so the resolver
        // is never invoked — a bare substitute is sufficient (Story 3.3a).
        var escalating = new EscalatingStatementFieldExtractor(
            inner,
            orchestrator,
            Substitute.For<IProductResolver>(),
            new SectionAnchorOcrEscalationStage(SectionOcrEscalationTestHelpers.NoOpSectionOcrEngine(), NullLogger<SectionAnchorOcrEscalationStage>.Instance),
            XUnitLogger.CreateLogger<EscalatingStatementFieldExtractor>());

        var result = await escalating.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.PeriodSummary.ShouldNotBeNull();

        var product = model.PeriodSummary!.Product;

        // The neutered positional card-number-band fallback must have abstained first (this is
        // what makes the escalation ladder's StatusGate fire) — a Found-via-Positional result
        // here would mean the alias hack silently survived.
        product.Status.ShouldBe(
            ExtractionStatus.ExtractedByInference,
            $"Product.Status was {product.Status} (Value='{product.Value}') — expected the "
            + "positional extractor to abstain and HeaderImageOcr to recover the value.");
        product.Provenance.Stage.ShouldBe(StageId.HeaderImageOcr);
        product.Value.ShouldNotBeNullOrWhiteSpace();
        product.Value!.ShouldContain("COSTCO BANAMEX");
    }
}

using System.Text.Json;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// T1 — gated regression net (design doc <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c>
/// §3.3 / §5 step 10): exercises the render→crop GEOMETRY and the text→product RESOLUTION halves
/// of <see cref="HeaderImageOcrStage"/> against the real <c>good.pdf</c> fixture and a committed
/// real-OCR text snapshot, WITHOUT constructing native Tesseract anywhere in this class.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes (adversarial-review finding #1):</b> <see cref="HeaderImageOcrStageTests"/>
/// only ever exercises the stage against a fake <see cref="Ocr.IHeaderProductOcrEngine"/> returning
/// hardcoded literals — it never touches the real render/crop path on the real fixture. The only
/// test that does (<see cref="HeaderImageOcrStageLiveOcrCanaryTests"/>) is
/// <c>[Trait("Category","LiveOcr")]</c>, which the design doc explicitly declares NON-gating. A CI
/// lane with no native Tesseract provisioning therefore had zero coverage of the render/crop
/// geometry or the regex/resolver path. This class is gated (no trait, runs in the default set)
/// and needs no native OCR dependency at test time.
/// </para>
/// <para>
/// <b>Why NOT a byte-exact crop hash:</b> PDFium's rendered PIXEL CONTENT is not portable across
/// OS/hardware/library-version combinations (the same reason the extraction pipeline uses
/// perceptual, not exact, hashing elsewhere). This class never compares crop bytes or a hash of
/// them — <see cref="RenderHeaderCrop_GoodPdfPage1_MatchesGeometryComputedFromRealMediaBox"/> only
/// asserts pixel DIMENSIONS (width/height), which are deterministic integer arithmetic over the
/// page's point-space <c>MediaBox</c>, the render DPI, and the crop fraction — all three of which
/// are portable. <see cref="ProductionRegexAndResolver_CommittedRealOcrSnapshot_ResolvesToCostcoBanamex"/>
/// never calls OCR at all — it replays a snapshot of what real Tesseract already produced.
/// </para>
/// </remarks>
public sealed class HeaderImageOcrStageSnapshotTests
{
    private static readonly string GoodPdfPath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", "good.pdf");

    private static readonly string SnapshotPath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", "good-header-ocr.snapshot.json");

    // -----------------------------------------------------------------------
    // T1a — crop geometry: render->crop pixel dimensions, no OCR involved
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renders <c>good.pdf</c> page 1 and crops the header band through the EXACT production path
    /// (<see cref="HeaderImageOcrStage.RenderHeaderCrop"/>), then asserts the crop's pixel
    /// dimensions equal what the production <see cref="HeaderImageOcrStage.RenderDpi"/> /
    /// <see cref="HeaderImageOcrStage.HeaderCropFraction"/> constants predict from the page's real
    /// point-space size (read independently via PdfPig, not hardcoded). Catches a DPI or
    /// crop-fraction regression without any OCR engine — PDFtoImage/SkiaSharp rendering is already
    /// exercised gated elsewhere in this project (<c>PerceptualHashSmokeTests</c>), so it carries no
    /// native-Tesseract dependency.
    /// </summary>
    [Fact]
    public void RenderHeaderCrop_GoodPdfPage1_MatchesGeometryComputedFromRealMediaBox()
    {
        if (!File.Exists(GoodPdfPath))
            Assert.Skip($"Demo corpus fixture not found at: {GoodPdfPath}");

        var pdfBytes = File.ReadAllBytes(GoodPdfPath);

        double pageWidthPt;
        double pageHeightPt;
        using (var document = PdfDocument.Open(pdfBytes))
        {
            var page = document.GetPage(1);
            pageWidthPt = page.Width;
            pageHeightPt = page.Height;
        }

        var expectedRenderWidthPx = (int)Math.Round(pageWidthPt * HeaderImageOcrStage.RenderDpi / 72.0);
        var expectedRenderHeightPx = (int)Math.Round(pageHeightPt * HeaderImageOcrStage.RenderDpi / 72.0);
        var expectedCropHeightPx = Math.Clamp(
            (int)Math.Round(expectedRenderHeightPx * HeaderImageOcrStage.HeaderCropFraction),
            1,
            expectedRenderHeightPx);

        var cropBytes = HeaderImageOcrStage.RenderHeaderCrop(pdfBytes);

        cropBytes.ShouldNotBeNull("Production render/crop path failed on the real good.pdf fixture.");

        using var croppedImage = SixLabors.ImageSharp.Image.Load(cropBytes);

        croppedImage.Width.ShouldBe(
            expectedRenderWidthPx,
            $"Crop width should equal the full rendered page width at {HeaderImageOcrStage.RenderDpi} DPI "
            + $"(page {pageWidthPt}pt × {HeaderImageOcrStage.RenderDpi}/72 ≈ {expectedRenderWidthPx}px). "
            + "A mismatch means RenderDpi regressed relative to what this test independently computed "
            + "from the page's real MediaBox.");

        croppedImage.Height.ShouldBe(
            expectedCropHeightPx,
            $"Crop height should equal {HeaderImageOcrStage.HeaderCropFraction:P0} of the rendered page "
            + $"height ({expectedRenderHeightPx}px × {HeaderImageOcrStage.HeaderCropFraction} ≈ {expectedCropHeightPx}px). "
            + "A mismatch means HeaderCropFraction or RenderDpi regressed.");
    }

    // -----------------------------------------------------------------------
    // T1b — text -> product resolution: committed real-OCR snapshot through the PRODUCTION regex
    // + PRODUCTION ProductResolver, no OCR engine involved
    // -----------------------------------------------------------------------

    /// <summary>
    /// Feeds a committed snapshot of REAL Tesseract OCR text (captured once from good.pdf's header
    /// crop — see <c>good-header-ocr.snapshot.json</c>'s <c>//provenance</c> field) through the
    /// PRODUCTION <see cref="HeaderImageOcrStage.TryMatchProductHeading"/> regex and the
    /// PRODUCTION <see cref="IProductResolver"/> (resolved via real DI via
    /// <c>AddVeriqanBinding</c>, never a hand-rolled fake), asserting the end-to-end resolved
    /// <c>ProductId</c>. This is the "a Tesseract-less CI lane still catches a regex or resolver
    /// regression" half of T1 — no native OCR engine is constructed anywhere in this test.
    /// </summary>
    [Fact]
    public void ProductionRegexAndResolver_CommittedRealOcrSnapshot_ResolvesToCostcoBanamex()
    {
        if (!File.Exists(SnapshotPath))
            Assert.Skip($"OCR snapshot fixture not found at: {SnapshotPath}");

        using var snapshotDoc = JsonDocument.Parse(File.ReadAllText(SnapshotPath));
        var rawOcrText = snapshotDoc.RootElement.GetProperty("rawOcrText").GetString();
        rawOcrText.ShouldNotBeNullOrWhiteSpace("Committed snapshot's rawOcrText must not be empty.");

        // Step 1: PRODUCTION regex-match + normalize (the exact code TryResolveAsync calls after a
        // real OCR read) — pure text-in/text-out, no OCR engine involved.
        var matched = HeaderImageOcrStage.TryMatchProductHeading(rawOcrText!);

        matched.ShouldNotBeNull(
            "The production regex failed to match the committed real-OCR snapshot text — this is "
            + "exactly the kind of regression T1 exists to catch without needing native Tesseract.");
        matched.ShouldContain("COSTCO BANAMEX");

        // Step 2: PRODUCTION ProductResolver, resolved via real DI (AddVeriqanBinding), against a
        // bundle carrying the real demo catalog's TC-COSTCO-BANAMEX row.
        var services = new ServiceCollection();
        services.AddVeriqanBinding();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IProductResolver>();

        var result = resolver.Resolve(matched!, DemoCatalogBundle());

        result.IsSuccess.ShouldBeTrue($"ProductResolver failed to resolve '{matched}': {result.Error}");
        result.Value!.ProductId.ShouldBe("TC-COSTCO-BANAMEX");
    }

    /// <summary>
    /// Minimal in-memory reference bundle carrying the same TC-COSTCO-BANAMEX row shipped in the
    /// real demo catalog
    /// (<c>Prisma/Fixtures/PRP2/demo/reference-bundle/Demo_Bank_(Iqubica)/products.csv</c>; mirrors
    /// <c>ProductResolverFuzzyTests.BundleWithCostcoBanamex</c> in <c>Veriqan.Application.Tests</c>).
    /// This test's job is to gate the OCR-text → regex → resolver PATH, not to re-verify
    /// CSV-catalog-loading machinery, so a hand-built bundle with the real row's exact values is
    /// the right fixture weight.
    /// </summary>
    private static VecReferenceBundle DemoCatalogBundle() => new(
        BundleMetadata: new BundleMetadata("1.0.0", "Demo Bank", null, null, null, null),
        Products:
        [
            new VecProduct(
                ProductId: "TC-COSTCO-BANAMEX",
                ProductName: "Tarjeta de Crédito COSTCO BANAMEX",
                Aliases: ["COSTCO BANAMEX", "Tarjeta de Crédito COSTCO BANAMEX"],
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: null),
        ],
        InterestRates: null,
        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        ClientAccounts: null,
        PriorStatements: null,
        ExpectedTransactions: null,
        ToleranceConfig: null,
        ValidationConstants: null);
}

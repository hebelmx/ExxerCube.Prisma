using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for RC1.S4.b: <c>ComputeMaxVerticalGap</c> must treat embedded images as content,
/// not as blank space, for the CL-48 "sin espacio en blanco mayor a 2 cm" check.
/// </summary>
/// <remarks>
/// <para>
/// Real Banamex/Citibanamex statement pages are image-heavy (a rendered/scanned background can
/// carry 900+ embedded images per page). Before this fix, <c>ComputeMaxVerticalGap</c> only
/// considered TEXT words, so an image-covered region between two text lines was reported as a
/// blank gap — producing a false CL-48 "blank page" Fail on every real B-series statement
/// (RC1.S4.b calibration evidence, `docs/qa/calibration/real-corpus-triage-2026-07.md`).
/// </para>
/// <para>
/// A tiny (4×4 px, solid-red) in-memory PNG is used as the embedded image — its raster content
/// is irrelevant, only its placement <see cref="PdfRectangle"/> matters for this check.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorVerticalGapImageTests
{
    /// <summary>
    /// Minimal valid 4×4 solid-red PNG (73 bytes), used purely as embeddable image content —
    /// the gap computation only consults the image's placement rectangle, never its pixels.
    /// </summary>
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR4nGP4z8AARwzEcQCukw/x0F8jngAAAABJRU5ErkJggg==";

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    /// <summary>
    /// Builds a single-page A4 PDF (595 × 842 pt) with two widely-separated text lines and no
    /// image between them — the baseline case, where the reported gap must be large.
    /// </summary>
    private static byte[] BuildPdfWithTwoLinesNoImage()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 10f;

        // Upper line near the top of the page.
        page.AddText("Encabezado", fontSize, new PdfPoint(50, 700), font);

        // Lower line near the bottom of the page — large blank span between the two.
        page.AddText("Pie de pagina", fontSize, new PdfPoint(50, 100), font);

        return builder.Build();
    }

    /// <summary>
    /// Builds the same two-line layout as <see cref="BuildPdfWithTwoLinesNoImage"/>, but with a
    /// large embedded image placed in the middle of the blank span (Y ≈ 150 to 680) — mimicking
    /// a real statement's rendered background/watermark image. The image must be treated as
    /// content, collapsing what would otherwise be one large blank gap into (at most) two small
    /// residual gaps at the top and bottom edges of the image.
    /// </summary>
    private static byte[] BuildPdfWithTwoLinesAndBridgingImage()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 10f;

        page.AddText("Encabezado", fontSize, new PdfPoint(50, 700), font);
        page.AddText("Pie de pagina", fontSize, new PdfPoint(50, 100), font);

        var pngBytes = Convert.FromBase64String(TinyPngBase64);
        // Large placement rectangle spanning most of the blank span between the two text lines.
        var placement = new PdfRectangle(x1: 50, y1: 150, x2: 545, y2: 680);
        page.AddPng(pngBytes, placement);

        return builder.Build();
    }

    // -----------------------------------------------------------------------
    // Test 1 — baseline: no image between two widely-spaced lines → large gap
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_TwoWidelySpacedLinesNoImage_ReportsLargeGap()
    {
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithTwoLinesNoImage();
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var gap = model.Pages[0].MaxVerticalGapPoints;

        // The two lines are ~600 pt apart with no intervening content — the reported gap must
        // be large (well above the CL-48 threshold of ~56.7 pt / 2 cm).
        gap.ShouldBeGreaterThan(
            400.0,
            $"Expected a large blank gap between two widely-spaced lines with no image between " +
            $"them, but got {gap:F2} pt.");
    }

    // -----------------------------------------------------------------------
    // Test 2 — an embedded image bridging the gap must be treated as content
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_TwoWidelySpacedLinesWithBridgingImage_ReportsSmallGap()
    {
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithTwoLinesAndBridgingImage();
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page = model.Pages[0];

        // The embedded image must be visible to the extractor at all (sanity check that the
        // synthetic PDF actually carries the image the test intends to exercise).
        page.ImageCount.ShouldBeGreaterThanOrEqualTo(
            1, "The synthetic PDF must carry at least one embedded image.");

        // With the image bridging almost the entire span between the two text lines, only a
        // small residual gap should remain at the image's top/bottom edges — nowhere near the
        // ~600 pt gap reported when no image is present (Test 1).
        page.MaxVerticalGapPoints.ShouldBeLessThan(
            100.0,
            $"Expected the embedded image to be treated as content and collapse the blank gap, " +
            $"but got {page.MaxVerticalGapPoints:F2} pt.");
    }

    // -----------------------------------------------------------------------
    // Test 3 — an image-only page (no text at all) still reports a sane gap
    // -----------------------------------------------------------------------

    /// <summary>
    /// A page with only an embedded image and no text words at all must not throw and must
    /// report a well-defined (non-negative) gap — regression guard for the removed
    /// <c>visibleWords.Count &lt; 2</c> early-out, which is now superseded by the combined
    /// word+image interval count.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_ImageOnlyPageNoText_DoesNotThrowAndReportsNonNegativeGap()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var pngBytes = Convert.FromBase64String(TinyPngBase64);
        var placement = new PdfRectangle(x1: 50, y1: 150, x2: 545, y2: 680);
        page.AddPng(pngBytes, placement);
        var pdfBytes = builder.Build();

        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        model.Pages[0].MaxVerticalGapPoints.ShouldBeGreaterThanOrEqualTo(
            0.0, "MaxVerticalGapPoints must never be negative.");
    }

    // -----------------------------------------------------------------------
    // RC1.S5 (owner ruling 2026-07-23) — footer-aware gap exclusion
    // -----------------------------------------------------------------------
    //
    // A probe of 8 real credit-card statements found every CL-48 gap flag was genuinely
    // trailing whitespace down to the page footer (a small logo raster near the bottom edge,
    // PdfPig y ~= 24.5-45.5 pt on 792 pt pages). Because the footer counts as content, the
    // blank span between the last real content block and the footer was registering as an
    // intra-page gap. The owner ruled: content lying entirely within the trailing footer zone
    // (FooterZoneHeightPoints = 56.7 pt, same physical 2 cm constant as the CL-48 threshold)
    // must be excluded before measuring gaps; internal gaps between real content blocks must
    // still be measured and must still fail.

    /// <summary>
    /// A footer-only image near the bottom edge (well inside the footer zone) must not turn the
    /// trailing whitespace below the last real content line into a reported gap.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_TrailingFooterOnlyContent_GapNotCounted()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 10f;

        // Single real content line near the top of the page.
        page.AddText("Encabezado", fontSize, new PdfPoint(50, 700), font);

        // Footer-only image well within the footer zone (y in [20, 40], both < 56.7 pt).
        var pngBytes = Convert.FromBase64String(TinyPngBase64);
        var footerPlacement = new PdfRectangle(x1: 50, y1: 20, x2: 100, y2: 40);
        page.AddPng(pngBytes, footerPlacement);

        var pdfBytes = builder.Build();
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page1 = model.Pages[0];
        page1.ImageCount.ShouldBeGreaterThanOrEqualTo(
            1, "The synthetic PDF must carry the footer image the test intends to exercise.");

        // Without the footer-zone exclusion, this would report a ~650 pt gap (line at y~700
        // down to the footer image top at y=40). With the exclusion, only one non-footer
        // content block remains, so the gap is 0.
        page1.MaxVerticalGapPoints.ShouldBe(
            0.0,
            $"Expected the trailing gap down to a footer-only block to be excluded, " +
            $"but got {page1.MaxVerticalGapPoints:F2} pt.");
    }

    /// <summary>
    /// An internal gap between two real (non-footer) content blocks must still be measured and
    /// must still exceed the CL-48 threshold, even when the page also carries a footer.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_InternalGapBetweenRealContentBlocks_StillCounted()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 10f;

        // Two real content lines with a genuine internal gap (~200 pt) between them.
        page.AddText("Encabezado", fontSize, new PdfPoint(50, 800), font);
        page.AddText("Cuerpo", fontSize, new PdfPoint(50, 600), font);

        // Footer-only image well within the footer zone — must not affect the internal gap.
        var pngBytes = Convert.FromBase64String(TinyPngBase64);
        var footerPlacement = new PdfRectangle(x1: 50, y1: 20, x2: 100, y2: 40);
        page.AddPng(pngBytes, footerPlacement);

        var pdfBytes = builder.Build();
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var gap = model.Pages[0].MaxVerticalGapPoints;

        // The internal gap between the two real lines (~200 pt apart) must still be reported —
        // well above the CL-48 56.7 pt threshold.
        gap.ShouldBeGreaterThan(
            100.0,
            $"Expected the internal gap between two real content blocks to still be counted, " +
            $"but got {gap:F2} pt.");

        // Upper bound rules out the footer contaminating the measurement: if the footer-only
        // block were NOT excluded, the reported gap would instead run from the lower text line
        // down to the footer (~560 pt), far exceeding this bound.
        gap.ShouldBeLessThan(
            300.0,
            $"Expected the reported gap to be the internal ~200 pt gap between the two real " +
            $"lines, not a gap inflated by the (excluded) trailing footer block, " +
            $"but got {gap:F2} pt.");
    }

    /// <summary>
    /// A page whose only content is its footer must report no gap. Note: this does NOT make
    /// the page pass CL-48's whole-blank-page branch — that branch (in
    /// <c>Cl48BlankPageRule</c>) separately fails on <c>HasContent == false &amp;&amp;
    /// ImageCount == 0</c>, and this page has <c>ImageCount == 1</c> (the footer image), so it
    /// is correctly not blank either. Blank-page detection for a truly empty page is unaffected
    /// by this fix.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_OnlyContentIsFooter_ReportsNoGap()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);

        var pngBytes = Convert.FromBase64String(TinyPngBase64);
        var footerPlacement = new PdfRectangle(x1: 50, y1: 20, x2: 100, y2: 40);
        page.AddPng(pngBytes, footerPlacement);

        var pdfBytes = builder.Build();
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page1 = model.Pages[0];
        page1.ImageCount.ShouldBeGreaterThanOrEqualTo(1, "the footer-only image must be present");
        page1.MaxVerticalGapPoints.ShouldBe(
            0.0,
            $"A page whose only content is its footer must report no gap, " +
            $"but got {page1.MaxVerticalGapPoints:F2} pt.");
    }
}

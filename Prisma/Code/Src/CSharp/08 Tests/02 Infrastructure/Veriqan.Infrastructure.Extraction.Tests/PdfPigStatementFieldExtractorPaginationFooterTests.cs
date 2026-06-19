using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for VERIQAN-E2-S7: pagination "N de M" regex anchored to the page footer band.
/// </summary>
/// <remarks>
/// <para>
/// PdfPig uses a <b>bottom-left coordinate origin</b>: Y=0 is the physical bottom of the page
/// and Y=pageHeight is the top.  The footer band is the bottom 10% of the page
/// (<c>word.BoundingBox.Bottom &lt; pageHeight * 0.10</c>).
/// </para>
/// <para>
/// For a standard A4 page (842 pt), the footer threshold is ≈ 84 pt.
/// Words placed at Y=30 fall inside the footer; words at Y=400 are in the body.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorPaginationFooterTests
{
    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    // -----------------------------------------------------------------------
    // PDF builders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a single-page A4 PDF (595 × 842 pt) with:
    /// <list type="bullet">
    ///   <item>Body text "5 de 10" at Y=400 (mid-page body area, above footer band).</item>
    ///   <item>Footer pagination "1 de 3" at Y=30 (inside the bottom-10% footer band, Y &lt; 84.2).</item>
    /// </list>
    /// The extractor must ignore the body phrase and extract (1, 3) from the footer.
    /// </summary>
    private static byte[] BuildPdfWithBodyAndFooterPagination()
    {
        // A4: 595 × 842 pt.  Footer threshold = 842 * 0.10 = 84.2 pt.
        // Body Y=400 > 84.2 → outside footer band.
        // Footer Y=30 < 84.2 → inside footer band.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;

        // Body phrase that must NOT be extracted as pagination.
        // Placed at Y=400, well above the footer band (threshold ≈ 84 pt).
        page.AddText("5", fontSize, new PdfPoint(100, 400), font);
        page.AddText("de", fontSize, new PdfPoint(115, 400), font);
        page.AddText("10", fontSize, new PdfPoint(128, 400), font);
        page.AddText("pagos", fontSize, new PdfPoint(145, 400), font);

        // Real footer pagination "1 de 3" at Y=30 — inside the footer band.
        page.AddText("1", fontSize, new PdfPoint(280, 30), font);
        page.AddText("de", fontSize, new PdfPoint(290, 30), font);
        page.AddText("3", fontSize, new PdfPoint(303, 30), font);

        return builder.Build();
    }

    /// <summary>
    /// Builds a single-page A4 PDF (595 × 842 pt) with only a real footer pagination label
    /// "2 de 4" at Y=35 (inside the bottom-10% footer band, no body confusion).
    /// </summary>
    private static byte[] BuildPdfWithOnlyFooterPagination()
    {
        // Footer threshold = 842 * 0.10 = 84.2 pt.  Y=35 < 84.2 → inside band.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;

        // Only footer pagination — "2 de 4" at Y=35.
        page.AddText("2", fontSize, new PdfPoint(280, 35), font);
        page.AddText("de", fontSize, new PdfPoint(290, 35), font);
        page.AddText("4", fontSize, new PdfPoint(303, 35), font);

        return builder.Build();
    }

    // -----------------------------------------------------------------------
    // Test 1 — body phrase must NOT win over the real footer label
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the page contains a body phrase "5 de 10" (Y=400, above footer band) AND
    /// a footer pagination label "1 de 3" (Y=30, inside footer band), the extractor
    /// must return PaginationCurrent=1 and PaginationTotal=3 — not (5, 10).
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_BodyPhraseAboveFooterRealLabelInFooter_ReturnsFooterPagination()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithBodyAndFooterPagination();
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert — extraction must succeed.
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.ShouldNotBeNull();
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page = model.Pages[0];

        // The footer-anchored filter must skip "5 de 10" in the body and pick "1 de 3".
        page.PaginationCurrent.ShouldBe(1,
            "PaginationCurrent must come from the footer label '1 de 3', not the body phrase '5 de 10'");
        page.PaginationTotal.ShouldBe(3,
            "PaginationTotal must come from the footer label '1 de 3', not the body phrase '5 de 10'");
    }

    // -----------------------------------------------------------------------
    // Test 2 — correct footer label is preserved unchanged
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the pagination label "2 de 4" appears correctly in the footer band and there
    /// is no body confusion, the extractor must return PaginationCurrent=2 and
    /// PaginationTotal=4 — the footer-band filter must not discard it.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_PaginationOnlyInFooter_ReturnsPaginationUnchanged()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithOnlyFooterPagination();
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert — extraction must succeed.
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.ShouldNotBeNull();
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page = model.Pages[0];

        // Footer label "2 de 4" must be extracted without modification.
        page.PaginationCurrent.ShouldBe(2,
            "PaginationCurrent must be 2 from the footer label '2 de 4'");
        page.PaginationTotal.ShouldBe(4,
            "PaginationTotal must be 4 from the footer label '2 de 4'");
    }
}

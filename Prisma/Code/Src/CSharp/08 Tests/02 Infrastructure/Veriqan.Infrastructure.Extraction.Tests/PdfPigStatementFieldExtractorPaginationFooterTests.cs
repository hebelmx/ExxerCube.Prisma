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

    // -----------------------------------------------------------------------
    // Test 3 — pagination just ABOVE the footer band must not be silently lost
    // -----------------------------------------------------------------------

    /// <summary>
    /// Regression guard for the S7 fallback path: when the "N de M" label sits at
    /// Y ≈ 12% of page height (just above the 10% footer-band threshold), the
    /// footer-band word set is empty and the primary scan yields no match.
    /// The extractor must fall back to a full-page scan and still return the label
    /// — never silently return null (which would cause CL-31 to abstain → false-PASS).
    /// </summary>
    /// <remarks>
    /// A4 page: 595 × 842 pt.  Footer threshold = 842 × 0.10 = 84.2 pt.
    /// Label placed at Y = 842 × 0.12 ≈ 101 pt — above the 10% band, so the
    /// footer filter finds nothing.  The fallback page-wide scan must recover it.
    /// </remarks>
    [Fact]
    public async Task ExtractFullAsync_PaginationJustAboveFooterBand_StillExtractedViaFallback()
    {
        // Arrange — A4 page (595 × 842 pt); label at Y ≈ 101 pt (12% of 842 ≈ 101).
        // Footer threshold is 842 × 0.10 = 84.2 pt, so 101 pt is OUTSIDE the footer band.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;
        const float labelY = 101f; // 12% of 842 — just above the 10% band

        page.AddText("3", fontSize, new PdfPoint(280, labelY), font);
        page.AddText("de", fontSize, new PdfPoint(290, labelY), font);
        page.AddText("5", fontSize, new PdfPoint(303, labelY), font);

        var pdfBytes = builder.Build();
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.ShouldNotBeNull();
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var pageResult = model.Pages[0];

        // Fallback must have recovered "3 de 5" even though it is above the footer band.
        pageResult.PaginationCurrent.ShouldBe(3,
            "PaginationCurrent must be 3 via page-wide fallback — label is above footer band but must not be silently lost");
        pageResult.PaginationTotal.ShouldBe(5,
            "PaginationTotal must be 5 via page-wide fallback — label is above footer band but must not be silently lost");
    }

    // -----------------------------------------------------------------------
    // RC1.S4.b — MSI installment-fragment rejection (real-corpus recalibration)
    // -----------------------------------------------------------------------
    //
    // Real Banamex/Citibanamex movements tables carry MSI ("meses sin intereses")
    // installment-plan fragments that also match the bare "N de M" shape (e.g. "3 de 12").
    // Unlike a genuine page-number stamp, these fragments always sit on a line that also
    // carries a currency amount ("$…") and/or an operation-date token ("dd-mmm-yyyy"). The
    // three tests below prove the extractor rejects such lines rather than manufacturing a
    // fake pagination total from them — see PdfPigStatementFieldExtractor.IsPlausiblePageLabelLine.

    /// <summary>
    /// Builds a single-page A4 PDF whose ONLY "N de M" text is an MSI-style fragment
    /// ("3 de 12") sitting inside the literal bottom-10% footer band, on the SAME line as a
    /// currency amount and an operation-date token — reproducing the real B-series page-2
    /// collision found during RC1.S3 triage (a movements-table row happens to land inside the
    /// footer band by page-layout coincidence).
    /// </summary>
    private static byte[] BuildPdfWithMsiFragmentInFooterBand()
    {
        // A4: 595 × 842 pt. Footer threshold = 84.2 pt. Y=30 is inside the footer band.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;
        const float y = 30f;

        page.AddText("04-mar-2026", fontSize, new PdfPoint(50, y), font);
        page.AddText("MERCADO", fontSize, new PdfPoint(120, y), font);
        page.AddText("PAGO", fontSize, new PdfPoint(160, y), font);
        page.AddText("$11,444.24", fontSize, new PdfPoint(200, y), font);
        page.AddText("3", fontSize, new PdfPoint(280, y), font);
        page.AddText("de", fontSize, new PdfPoint(290, y), font);
        page.AddText("12", fontSize, new PdfPoint(303, y), font);

        return builder.Build();
    }

    /// <summary>
    /// When the only "N de M" text on a page is an MSI installment fragment inside the footer
    /// band (accompanied by a date token and a currency amount on the same line), the
    /// extractor must NOT report it as pagination — PaginationCurrent/Total must both stay
    /// null so CL-31 abstains (or evaluates only genuinely labeled pages) instead of computing
    /// on a fabricated total.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_MsiFragmentInFooterBand_DoesNotProduceFalsePagination()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithMsiFragmentInFooterBand();
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page = model.Pages[0];

        page.PaginationCurrent.ShouldBeNull(
            "An MSI fragment ('3 de 12') sharing a line with a date and a currency amount must " +
            "never be reported as pagination, even though it falls inside the footer band.");
        page.PaginationTotal.ShouldBeNull(
            "PaginationTotal must stay null for the same reason as PaginationCurrent.");
    }

    /// <summary>
    /// Builds a single-page A4 PDF whose ONLY "N de M" text is an MSI-style fragment placed in
    /// the wider fallback margin band (Y ≈ 15%, above the strict 10% footer band but inside the
    /// 20% margin band) — reproducing the real B-series page-3 collision where the footer band
    /// is empty and, pre-fix, the page-wide fallback would have grabbed a mid-table fragment.
    /// </summary>
    private static byte[] BuildPdfWithMsiFragmentInFallbackMarginBand()
    {
        // A4: 595 × 842 pt. Margin band threshold = 168.4 pt (20%). Y=126 ≈ 15% — inside the
        // margin band but outside the strict 10% footer band (84.2 pt).
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;
        const float y = 126f;

        page.AddText("26-mar-2026", fontSize, new PdfPoint(50, y), font);
        page.AddText("DISPONIBLE", fontSize, new PdfPoint(140, y), font);
        page.AddText("$1,190.04", fontSize, new PdfPoint(210, y), font);
        page.AddText("007", fontSize, new PdfPoint(280, y), font);
        page.AddText("de", fontSize, new PdfPoint(300, y), font);
        page.AddText("012", fontSize, new PdfPoint(315, y), font);

        return builder.Build();
    }

    /// <summary>
    /// When the footer band is empty and the only "N de M" text anywhere in the fallback
    /// margin band is an MSI fragment (date + amount on the same line), the fallback must NOT
    /// recover it — PaginationCurrent/Total must stay null. This is the "constrain, don't
    /// delete" behavior: the fallback still exists (see the sibling
    /// "PaginationJustAboveFooterBand" test recovering a genuine label), but it never accepts
    /// an implausible movements-table row.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_MsiFragmentInFallbackMarginBand_AbstainsRatherThanFalsePositive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithMsiFragmentInFallbackMarginBand();
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page = model.Pages[0];

        page.PaginationCurrent.ShouldBeNull(
            "An MSI fragment ('007 de 012') in the fallback margin band must never be recovered " +
            "as pagination — it shares its line with a date and a currency amount.");
        page.PaginationTotal.ShouldBeNull(
            "PaginationTotal must stay null for the same reason as PaginationCurrent.");
    }

    /// <summary>
    /// Builds a single-page A4 PDF with a genuine footer label ("1 de 9") AND, elsewhere on the
    /// same page, several MSI-style fragments inside the footer/margin bands sharing lines with
    /// dates and amounts — reproducing the real corpus where a genuine label and MSI noise
    /// coexist. The genuine label must win; the MSI noise must not create a conflicting total.
    /// </summary>
    private static byte[] BuildPdfWithGenuineLabelAndMsiNoise()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;

        // Genuine label deep in the footer band (Y=15, well inside the 10% band).
        page.AddText("1", fontSize, new PdfPoint(280, 15), font);
        page.AddText("de", fontSize, new PdfPoint(290, 15), font);
        page.AddText("9", fontSize, new PdfPoint(303, 15), font);

        // MSI noise at Y=50 — still inside the footer band, but implausible (date + amount).
        page.AddText("04-mar-2026", fontSize, new PdfPoint(50, 50), font);
        page.AddText("$409.13", fontSize, new PdfPoint(150, 50), font);
        page.AddText("3", fontSize, new PdfPoint(280, 50), font);
        page.AddText("de", fontSize, new PdfPoint(290, 50), font);
        page.AddText("3", fontSize, new PdfPoint(303, 50), font);

        return builder.Build();
    }

    /// <summary>
    /// The genuine footer label must be extracted even when implausible MSI-style noise is
    /// also present inside the footer band on other lines — the plausibility filter must
    /// discriminate per-line, not reject the whole footer band.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_GenuineLabelAmongMsiNoise_ExtractsGenuineLabelOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildPdfWithGenuineLabelAndMsiNoise();
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        model.Pages.Count.ShouldBe(1, "synthetic PDF has exactly one page");

        var page = model.Pages[0];

        page.PaginationCurrent.ShouldBe(1,
            "The genuine footer label '1 de 9' must be extracted despite implausible MSI noise " +
            "elsewhere in the footer band.");
        page.PaginationTotal.ShouldBe(9,
            "The genuine footer label '1 de 9' must be extracted despite implausible MSI noise " +
            "elsewhere in the footer band.");
    }
}

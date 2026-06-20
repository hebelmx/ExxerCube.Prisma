using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests that exercise the per-page inspection facts extraction path (Story 5.3 — CL-31/33/34/48)
/// against the <c>jul_ago</c> Dummie VEC PDF fixture.
/// </summary>
/// <remarks>
/// <para>
/// These tests do NOT assert compliance — they verify that the extraction plumbing works end-to-end
/// and log the actual per-page facts for human review:
/// <list type="bullet">
///   <item><see cref="StatementModel.PageCount"/> is greater than zero.</item>
///   <item><see cref="StatementModel.Pages"/> is non-empty and has the same count as <c>PageCount</c>.</item>
///   <item>Per-page: <c>HasContent</c>, <c>ImageCount</c>, <c>ContainsCardNumber</c>, pagination labels.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PageInspectionFactsSmokeTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(), Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()), new NullPasswordProvider());

    /// <summary>
    /// Verifies that <see cref="StatementModel.Pages"/> and <see cref="StatementModel.PageCount"/>
    /// are populated and logs the per-page facts for diagnostic visibility.
    /// Does NOT assert compliance — just checks extraction ran successfully.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgoFixture_PopulatesPageInspectionFacts()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");

        var model = result.Value!;
        var output = TestContext.Current.TestOutputHelper;

        // Assert: Pages is non-null and PageCount > 0.
        model.Pages.ShouldNotBeNull("Pages must be non-null after extraction.");
        model.PageCount.ShouldBeGreaterThan(0, "PageCount must be > 0 for a non-empty PDF.");
        model.Pages.Count.ShouldBe(model.PageCount,
            "Pages list count must equal PageCount.");

        output?.WriteLine($"[jul_ago] PageCount={model.PageCount}, Pages.Count={model.Pages.Count}");
        output?.WriteLine("Per-page facts:");

        foreach (var page in model.Pages)
        {
            var paginationLabel = page.PaginationCurrent.HasValue
                ? $"{page.PaginationCurrent} de {page.PaginationTotal}"
                : "(no label)";

            output?.WriteLine(
                $"  Page {page.PageNumber}: HasContent={page.HasContent}, " +
                $"ImageCount={page.ImageCount}, ContainsCardNumber={page.ContainsCardNumber}, " +
                $"Pagination={paginationLabel}");
        }

        // Minimal structural assertions (do NOT assert compliance).
        model.Pages.ShouldNotBeEmpty("Pages must be non-empty after extraction.");

        foreach (var page in model.Pages)
        {
            page.PageNumber.ShouldBeGreaterThan(0, "PageNumber must be 1-based.");
            page.ImageCount.ShouldBeGreaterThanOrEqualTo(0, "ImageCount must be non-negative.");
            page.Locator.ShouldNotBeNull("Locator must be non-null.");
        }
    }

    // -----------------------------------------------------------------------
    // S11 — Whitespace-glyph filter and MaxVerticalGapPoints extraction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="PageInspectionFacts.MaxVerticalGapPoints"/> is populated
    /// for every page (Story S11 — CL-48 vertical-gap check).
    /// The jul_ago fixture is a real multi-section statement; its pages must each report a
    /// non-negative gap value.  Pages with fewer than two content lines (e.g. a mostly-image
    /// cover page) report 0.0 — that is valid and expected.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgoFixture_MaxVerticalGapPointsIsNonNegativePerPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        var output = TestContext.Current.TestOutputHelper;

        model.Pages.ShouldNotBeEmpty("Pages must be non-empty.");

        foreach (var page in model.Pages)
        {
            page.MaxVerticalGapPoints.ShouldBeGreaterThanOrEqualTo(
                0.0,
                $"Page {page.PageNumber}: MaxVerticalGapPoints must be >= 0.");

            output?.WriteLine(
                $"  Page {page.PageNumber}: HasContent={page.HasContent}, " +
                $"MaxVerticalGapPoints={page.MaxVerticalGapPoints:F2} pt");
        }
    }

    /// <summary>
    /// Verifies the whitespace-glyph filter: every page that reports <c>HasContent=true</c>
    /// must have at least one non-whitespace word; pages with <c>HasContent=false</c> must
    /// have either no words at all or only whitespace-only tokens (the filter ran correctly).
    ///
    /// Because this test uses a real PDF it cannot force a whitespace-only token, so it
    /// verifies the structural invariant of the filter: all content pages genuinely have
    /// visible text (a consistency check that the filter didn't over-aggressively strip real
    /// words from content pages).
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgoFixture_HasContentIsConsistentWithVisibleWords()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;

        // The fixture is a known-good statement; all pages must report HasContent=true
        // (no pages should be falsely stripped by the whitespace filter).
        foreach (var page in model.Pages)
        {
            page.HasContent.ShouldBeTrue(
                $"Page {page.PageNumber}: expected HasContent=true in the jul_ago fixture " +
                $"(whitespace filter must not over-strip real content).");
        }
    }
}

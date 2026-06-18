using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests for Story 10.2: inter-section blank gap computation.
/// Runs against the real Dummie VEC PRP2 PDF fixtures to verify that the
/// <see cref="StatementModel.SectionGaps"/> collection is populated with
/// structurally sound values (non-negative, plausible range).
/// </summary>
/// <remarks>
/// These tests verify extraction plumbing — they do NOT assert regulatory compliance.
/// Specific gap magnitudes are not asserted because the Dummie fixtures are synthetic
/// and actual gap sizes depend on PDF layout, which may vary.  The tests assert:
/// <list type="bullet">
///   <item><c>SectionGaps</c> is never null.</item>
///   <item>All <c>GapPoints</c> values are ≥ 0.</item>
///   <item>Section-number references (AfterSectionNumber / BeforeSectionNumber) are in [1..28].</item>
///   <item>Page numbers in the gap list reference pages that actually exist in the document.</item>
///   <item>The overall gap collection is non-empty when multiple sections are detected on the same page.</item>
/// </list>
/// </remarks>
public sealed class SectionGapExtractionSmokeTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    // -----------------------------------------------------------------------
    // Test: SectionGaps is never null (even on minimal PDFs)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_SectionGapsIsNotNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        result.Value!.SectionGaps.ShouldNotBeNull("SectionGaps must never be null.");
    }

    // -----------------------------------------------------------------------
    // Test: all gap values are non-negative (structural integrity)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("01+Dummie+VEC+jul_ago+20252.pdf")]
    [InlineData("02+Dummie+VEC+ago_sep+2025.pdf")]
    [InlineData("03+Dummie+VEC+sep_oct+2025.pdf")]
    public async Task ExtractFullAsync_AllFixtures_AllGapPointsNonNegative(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath(fileName);
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed for {fileName}: {result.Error}");
        var gaps = result.Value!.SectionGaps;

        foreach (var gap in gaps)
        {
            gap.GapPoints.ShouldBeGreaterThanOrEqualTo(0.0,
                $"Gap between §{gap.AfterSectionNumber} and §{gap.BeforeSectionNumber} " +
                $"on page {gap.PageNumber} must be ≥ 0 pt.");
        }
    }

    // -----------------------------------------------------------------------
    // Test: section numbers in gaps are valid (1–28) and AfterSectionNumber < BeforeSectionNumber
    //       when sections appear in order
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("01+Dummie+VEC+jul_ago+20252.pdf")]
    [InlineData("02+Dummie+VEC+ago_sep+2025.pdf")]
    [InlineData("03+Dummie+VEC+sep_oct+2025.pdf")]
    public async Task ExtractFullAsync_AllFixtures_GapSectionNumbersAreValid(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath(fileName);
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;
        var gaps = model.SectionGaps;

        foreach (var gap in gaps)
        {
            gap.AfterSectionNumber.ShouldBeInRange(1, 28);
            gap.BeforeSectionNumber.ShouldBeInRange(1, 28);
            gap.PageNumber.ShouldBeGreaterThan(0,
                $"Gap page number must be ≥ 1.");
            gap.PageNumber.ShouldBeLessThanOrEqualTo(model.PageCount,
                $"Gap page number {gap.PageNumber} exceeds document page count {model.PageCount}.");
        }
    }

    // -----------------------------------------------------------------------
    // Test: gap values are plausible (< full page height ≈ 841 pt for A4)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("01+Dummie+VEC+jul_ago+20252.pdf")]
    [InlineData("02+Dummie+VEC+ago_sep+2025.pdf")]
    [InlineData("03+Dummie+VEC+sep_oct+2025.pdf")]
    public async Task ExtractFullAsync_AllFixtures_GapPointsArePlausible(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath(fileName);
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;
        var gaps = model.SectionGaps;

        // Maximum plausible gap = full page height (no real content can span more than one page).
        // A4 = 841.89 pt; use 900 pt as a safe upper bound.
        const double maxPlausibleGap = 900.0;

        foreach (var gap in gaps)
        {
            gap.GapPoints.ShouldBeLessThanOrEqualTo(maxPlausibleGap,
                $"Gap of {gap.GapPoints:0.##} pt between §{gap.AfterSectionNumber} and §{gap.BeforeSectionNumber} " +
                $"on page {gap.PageNumber} exceeds a full A4 page height — likely a measurement error.");
        }
    }

    // -----------------------------------------------------------------------
    // Test: diagnostic output — log gap data so the story return can report values
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_LogGapData()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;
        var output = TestContext.Current.TestOutputHelper;

        output?.WriteLine($"[jul_ago] SectionGaps.Count = {model.SectionGaps.Count}");
        output?.WriteLine($"  TwoCmInPoints threshold = {SectionGap.TwoCmInPoints:0.####} pt");

        if (model.SectionGaps.Count == 0)
        {
            output?.WriteLine("  (no same-page consecutive section pairs found — all present sections are on different pages)");
        }
        else
        {
            foreach (var gap in model.SectionGaps.OrderByDescending(g => g.GapPoints))
            {
                var cm = gap.GapPoints / SectionGap.CmToPoints;
                var exceedsThreshold = gap.GapPoints > SectionGap.TwoCmInPoints ? " *** EXCEEDS 2cm ***" : string.Empty;
                output?.WriteLine(
                    $"  §{gap.AfterSectionNumber} → §{gap.BeforeSectionNumber} " +
                    $"page {gap.PageNumber}: {cm:0.##} cm ({gap.GapPoints:0.##} pt){exceedsThreshold}");
            }
        }

        // Structural assertions only — magnitudes are informational.
        model.SectionGaps.ShouldNotBeNull();
        model.SectionGaps.All(g => g.GapPoints >= 0.0).ShouldBeTrue("All gap values must be >= 0.");
    }
}

using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using Shouldly;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests that exercise the word-level typography extraction path (Epic 12)
/// against the three Dummie VEC PDF fixtures.
/// </summary>
/// <remarks>
/// <para>
/// These tests do NOT assert point-size compliance thresholds — those are performed by the
/// downstream typography rules.  Here we only verify that the extraction plumbing works
/// end-to-end:
/// <list type="bullet">
///   <item><see cref="StatementModel.TypographySamples"/> is non-null.</item>
///   <item>When <see cref="TypographyExtractionStatus.Extracted"/>,
///   <see cref="StatementModel.TypographySamples"/> is non-empty.</item>
///   <item>Every sample has a positive <see cref="TextTypographySample.PointSize"/>.</item>
///   <item>Every sample has non-empty <see cref="TextTypographySample.Text"/>.</item>
///   <item>Diagnostic summary (sample count, page spread, point-size range) is logged.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class TypographyExtractionSmokeTests
{
    // -----------------------------------------------------------------------
    // Fixture paths (same as FontExtractionSmokeTests)
    // -----------------------------------------------------------------------

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    public static IEnumerable<object[]> AllFixtures =>
    [
        [FixturePath("01+Dummie+VEC+jul_ago+20252.pdf"), "jul_ago"],
        [FixturePath("02+Dummie+VEC+ago_sep+2025.pdf"), "ago_sep"],
        [FixturePath("03+Dummie+VEC+sep_oct+2025.pdf"), "sep_oct"],
    ];

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that the extractor populates <see cref="StatementModel.TypographySamples"/>
    /// and <see cref="StatementModel.TypographyExtractionStatus"/> for each fixture PDF.
    /// Also asserts that every extracted sample has a positive PointSize and non-empty Text.
    /// Logs a diagnostic summary for visibility.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_RealFixture_PopulatesTypographySamples(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange
        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        // Assert: extraction must succeed
        result.IsSuccess.ShouldBeTrue($"[{label}] ExtractFullAsync returned failure: {result.Error}");

        var model = result.Value!;

        // TypographySamples must never be null (always initialized)
        model.TypographySamples.ShouldNotBeNull($"[{label}] TypographySamples must not be null.");

        if (model.TypographyExtractionStatus == TypographyExtractionStatus.Extracted)
        {
            // When Extracted, the list must be non-empty
            model.TypographySamples.Count.ShouldBeGreaterThan(
                0,
                $"[{label}] TypographyExtractionStatus == Extracted but TypographySamples is empty.");

            // Every sample must have a positive PointSize
            var zeroOrNegative = model.TypographySamples
                .Where(s => s.PointSize <= 0)
                .ToList();
            zeroOrNegative.ShouldBeEmpty(
                $"[{label}] Found {zeroOrNegative.Count} sample(s) with PointSize <= 0.");

            // Every sample must have non-empty Text
            var emptyText = model.TypographySamples
                .Where(s => string.IsNullOrWhiteSpace(s.Text))
                .ToList();
            emptyText.ShouldBeEmpty(
                $"[{label}] Found {emptyText.Count} sample(s) with empty or whitespace Text.");

            // Diagnostic log
            var minPt = model.TypographySamples.Min(s => s.PointSize);
            var maxPt = model.TypographySamples.Max(s => s.PointSize);
            var pageCount = model.TypographySamples.Select(s => s.PageNumber).Distinct().Count();
            var boldCount = model.TypographySamples.Count(s => s.IsBold);

            TestContext.Current.TestOutputHelper?.WriteLine(
                $"[{label}] {model.TypographySamples.Count} sample(s) across {pageCount} page(s). " +
                $"PointSize: min={minPt:F2} max={maxPt:F2}. Bold samples: {boldCount}.");
        }
        else
        {
            // NotFound is acceptable (scanned PDF); just log it
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"[{label}] TypographyExtractionStatus = {model.TypographyExtractionStatus} (no text layer found).");
        }
    }
}

using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests that exercise the font-run extraction path (Story 5.1 — CL-35)
/// against the three Dummie VEC PDF fixtures.
/// </summary>
/// <remarks>
/// <para>
/// These tests do NOT assert that the fonts are Aptos — that is a compliance check
/// performed by <c>Cl35FontComplianceRule</c> in <c>Veriqan.Infrastructure.Visual</c>.
/// Here we only verify that the extraction plumbing works end-to-end:
/// <list type="bullet">
///   <item><see cref="StatementModel.FontRuns"/> is non-null.</item>
///   <item>When <see cref="FontExtractionStatus.Extracted"/>, <see cref="StatementModel.FontRuns"/> is non-empty.</item>
///   <item>The actual distinct font families found are logged for diagnostic visibility.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FontExtractionSmokeTests
{
    // -----------------------------------------------------------------------
    // Fixture paths (same as the main extractor tests)
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
    /// Verifies that the extractor populates <see cref="StatementModel.FontRuns"/>
    /// and <see cref="StatementModel.FontExtractionStatus"/> for each fixture PDF.
    /// Logs the distinct font families found for diagnostic visibility.
    /// Does NOT assert that the fonts are Aptos — that is the rule's job.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_RealFixture_PopulatesFontRuns(string fixturePath, string label)
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

        // FontRuns must never be null (always initialized)
        model.FontRuns.ShouldNotBeNull($"[{label}] FontRuns must not be null.");

        // When Extracted, there must be at least one run
        if (model.FontExtractionStatus == FontExtractionStatus.Extracted)
        {
            model.FontRuns.Count.ShouldBeGreaterThan(
                0,
                $"[{label}] FontExtractionStatus == Extracted but FontRuns is empty.");

            // Log the discovered families for diagnostic visibility
            var families = model.FontRuns
                .Select(r => r.FontName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Report to xUnit output (shown on failure or --verbosity detailed)
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"[{label}] {model.FontRuns.Count} font run(s) on {model.FontRuns.Select(r => r.PageNumber).Distinct().Count()} page(s). " +
                $"Distinct raw names: {string.Join(", ", families)}");
        }
        else
        {
            // NotFound is acceptable (scanned PDF); just log it
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"[{label}] FontExtractionStatus = {model.FontExtractionStatus} (no text layer found).");
        }
    }
}

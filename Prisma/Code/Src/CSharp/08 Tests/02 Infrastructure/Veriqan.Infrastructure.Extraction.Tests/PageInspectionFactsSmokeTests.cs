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
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

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
}

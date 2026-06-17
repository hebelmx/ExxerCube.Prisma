using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests that exercise the text-overlap (CL-28) and section-header-style (CL-29)
/// extraction paths (Story 5.2) against the <c>jul_ago</c> Dummie VEC PDF fixture.
/// </summary>
/// <remarks>
/// <para>
/// These tests do NOT assert compliance against the VEC typographic standard — that is
/// the job of the visual rules (CL-28 / CL-29).  Here we verify only that the
/// extraction plumbing works end-to-end and log the actual facts found so they can be
/// confirmed by a human reviewer:
/// <list type="bullet">
///   <item>How many text-overlap incidents were found (expect ~0 for a clean generated PDF).</item>
///   <item>Which section headers were detected + their <c>IsBold</c> / <c>IsUppercase</c> flags.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class VisualFactsExtractionSmokeTests
{
    // -----------------------------------------------------------------------
    // Fixture path (same directory as other extraction tests)
    // -----------------------------------------------------------------------

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    // -----------------------------------------------------------------------
    // Test 1: Text-overlap incidents — jul_ago fixture
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="StatementModel.TextOverlapIncidents"/> is populated
    /// and logs the actual count for human review.
    /// The assertion only checks that the property is non-null and extraction succeeded;
    /// the actual count is reported via test output.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgoFixture_ReportsTextOverlapIncidents()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");

        var model = result.Value!;
        var incidents = model.TextOverlapIncidents;

        // TextOverlapIncidents must never be null.
        incidents.ShouldNotBeNull("TextOverlapIncidents must be non-null after extraction.");

        // Log the actual count and details for human review.
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"[jul_ago] Text-overlap incidents found: {incidents.Count}");

        if (incidents.Count > 0)
        {
            foreach (var inc in incidents.OrderByDescending(i => i.OverlapPoints).Take(10))
            {
                output?.WriteLine(
                    $"  page {inc.PageNumber}: {inc.OverlapPoints:0.##} pt — {inc.SampleText}");
            }
        }
        else
        {
            output?.WriteLine("  (none — this is expected for a clean generated PDF)");
        }

        // The assertion: extraction succeeded and the list is non-null (count may be 0).
        // A clean synthesized PDF should have ~0 incidents; if we see any, they are logged.
        // We do NOT assert incidents.Count == 0 because an unexpected overlap is a finding
        // to report, not a test failure.
        incidents.Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Test 2: Section-header styles — jul_ago fixture
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="StatementModel.SectionHeaderStyles"/> is populated and
    /// logs which known section headers were detected, plus their bold/uppercase flags,
    /// for human review.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgoFixture_ReportsSectionHeaderStyles()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");

        var model = result.Value!;
        var headers = model.SectionHeaderStyles;

        // SectionHeaderStyles must never be null.
        headers.ShouldNotBeNull("SectionHeaderStyles must be non-null after extraction.");

        // Log actual detected headers for human review.
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"[jul_ago] Section headers detected: {headers.Count}");

        foreach (var h in headers)
        {
            output?.WriteLine(
                $"  page {h.PageNumber}: IsBold={h.IsBold}, IsUppercase={h.IsUppercase} — \"{h.HeaderText}\"");
        }

        if (headers.Count == 0)
        {
            output?.WriteLine("  (no known section titles found — check fixture content)");
        }

        // The assertion: the list is non-null and (if found) every entry has a non-empty text.
        foreach (var h in headers)
        {
            h.HeaderText.ShouldNotBeNullOrWhiteSpace("Every detected header must have text.");
            h.PageNumber.ShouldBeGreaterThan(0, "Header page number must be 1-based.");
        }
    }
}

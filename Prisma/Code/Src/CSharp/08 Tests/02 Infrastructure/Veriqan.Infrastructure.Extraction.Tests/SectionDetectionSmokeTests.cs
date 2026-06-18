using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests for Story 10.1: §1–28 section detection and per-page geometry (Width/Height).
/// Runs against the three real Dummie VEC PRP2 PDF fixtures.
/// </summary>
/// <remarks>
/// These tests verify extraction plumbing — they do NOT assert regulatory compliance.
/// Specific assertions are limited to structural guarantees (count == 28, Width &gt; 0, etc.)
/// because the Dummie fixtures are synthetic and may omit certain sections intentionally.
/// Sections that are NOT present in the fixtures are logged and noted in the story return.
/// </remarks>
public sealed class SectionDetectionSmokeTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    // -----------------------------------------------------------------------
    // Known-present anchor phrases (normalized) confirmed against the fixtures
    // These are unconditionally present in at least one fixture and are safe to assert.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sections whose normalized anchor is reliably found in the jul_ago fixture.
    /// Derived from the §-anchor table in PdfPigStatementFieldExtractor.
    /// </summary>
    private static readonly int[] KnownPresentInJulAgo =
    [
        7,   // "RESUMEN DE CARGOS Y ABONOS"
        13,  // "NIVEL DE USO DE TU TARJETA"
        22,  // "DESGLOSE DE MOVIMIENTOS"
        18,  // "PROGRAMAS DE BENEFICIOS"
    ];

    // -----------------------------------------------------------------------
    // Test: jul_ago fixture
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_SectionsPopulatedWith28Entries()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var extractor = CreateExtractor();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");

        var model = result.Value!;
        var output = TestContext.Current.TestOutputHelper;

        // --- §1–28 section map --------------------------------------------
        model.Sections.ShouldNotBeNull("Sections must not be null.");
        model.Sections.Count.ShouldBe(28,
            "Sections must always contain exactly 28 entries — one per Acuerdo §.");

        // Section numbers must be 1..28 in order.
        var numbers = model.Sections.Select(s => s.SectionNumber).ToList();
        numbers.ShouldBe(Enumerable.Range(1, 28).ToList(),
            "Sections must be ordered §1–§28 with no gaps.");

        // Known-present sections must be detected.
        foreach (var n in KnownPresentInJulAgo)
        {
            var sec = model.Sections.First(s => s.SectionNumber == n);
            sec.IsPresent.ShouldBeTrue(
                $"§{n} ({sec.Name}) should be present in the jul_ago fixture.");
            sec.IsApplicable.ShouldBeTrue($"§{n} is unconditional; IsApplicable must be true.");
            sec.Locator.ShouldNotBeNull($"§{n} must have a locator.");
            sec.Locator.PageNumber.ShouldBeGreaterThan(0,
                $"§{n} locator must point to a real page (PageNumber > 0).");
        }

        // Conditional sections (§16, §23, §25) must never be IsApplicable when absent.
        foreach (var n in new[] { 16, 23, 25 })
        {
            var sec = model.Sections.First(s => s.SectionNumber == n);
            if (!sec.IsPresent)
            {
                sec.IsApplicable.ShouldBeFalse(
                    $"Conditional §{n} that is absent must have IsApplicable=false.");
            }
        }

        // --- Per-page geometry --------------------------------------------
        model.Pages.ShouldNotBeEmpty("Pages must be populated.");
        foreach (var page in model.Pages)
        {
            page.Width.ShouldBeGreaterThan(0,
                $"Page {page.PageNumber} Width must be > 0 (Story 10.1 geometry).");
            page.Height.ShouldBeGreaterThan(0,
                $"Page {page.PageNumber} Height must be > 0 (Story 10.1 geometry).");
        }

        // --- Diagnostic log -----------------------------------------------
        output?.WriteLine($"[jul_ago] Sections.Count={model.Sections.Count}");
        output?.WriteLine("Section detection results:");
        foreach (var sec in model.Sections)
        {
            output?.WriteLine(
                $"  §{sec.SectionNumber:D2} {sec.Name,-50} " +
                $"IsPresent={sec.IsPresent,-5} IsApplicable={sec.IsApplicable,-5} " +
                $"Page={sec.Locator.PageNumber}");
        }

        output?.WriteLine($"\nPage geometry:");
        foreach (var p in model.Pages)
            output?.WriteLine($"  Page {p.PageNumber}: {p.Width:F1} × {p.Height:F1} pt");
    }

    // -----------------------------------------------------------------------
    // Test: ago_sep fixture
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_AgoSep_SectionsContain28Entries()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("02+Dummie+VEC+ago_sep+2025.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        model.Sections.Count.ShouldBe(28, "Sections must always contain 28 entries.");

        // All section numbers 1–28 present exactly once.
        model.Sections
            .Select(s => s.SectionNumber)
            .ShouldBe(Enumerable.Range(1, 28).ToList());

        // Page geometry populated.
        model.Pages.ShouldNotBeEmpty();
        model.Pages.All(p => p.Width > 0 && p.Height > 0).ShouldBeTrue(
            "All pages must have Width > 0 and Height > 0.");

        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"[ago_sep] Sections.Count={model.Sections.Count}, Pages={model.Pages.Count}");
        foreach (var sec in model.Sections.Where(s => s.IsPresent))
            output?.WriteLine($"  §{sec.SectionNumber:D2} {sec.Name} — page {sec.Locator.PageNumber}");
    }

    // -----------------------------------------------------------------------
    // Test: sep_oct fixture
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_SepOct_SectionsContain28Entries()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("03+Dummie+VEC+sep_oct+2025.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        model.Sections.Count.ShouldBe(28, "Sections must always contain 28 entries.");

        model.Sections
            .Select(s => s.SectionNumber)
            .ShouldBe(Enumerable.Range(1, 28).ToList());

        model.Pages.All(p => p.Width > 0 && p.Height > 0).ShouldBeTrue(
            "All pages must have Width > 0 and Height > 0.");

        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"[sep_oct] Sections.Count={model.Sections.Count}, Pages={model.Pages.Count}");
        foreach (var sec in model.Sections.Where(s => s.IsPresent))
            output?.WriteLine($"  §{sec.SectionNumber:D2} {sec.Name} — page {sec.Locator.PageNumber}");
    }

    // -----------------------------------------------------------------------
    // Test: count of present vs. absent sections is sane (not all absent)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_AtLeastFourSectionsPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);
        result.IsSuccess.ShouldBeTrue();

        var presentCount = result.Value!.Sections.Count(s => s.IsPresent);
        presentCount.ShouldBeGreaterThanOrEqualTo(4,
            "At least 4 of the 28 sections should be detectable in a real VEC fixture.");
    }
}

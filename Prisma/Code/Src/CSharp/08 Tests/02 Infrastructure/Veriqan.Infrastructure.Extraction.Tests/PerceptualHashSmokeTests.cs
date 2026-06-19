using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Integration smoke tests for the per-page perceptual-hash extraction pass
/// (VERIQAN-E2-S4 — <see cref="StatementModel.PagePerceptualHashes"/>).
/// </summary>
/// <remarks>
/// <para>
/// These tests exercise the <c>enableCatalogImageHashing</c> opt-in flag that was
/// added to <see cref="PdfPigStatementFieldExtractor"/> in VERIQAN-E2-S4.
/// They do NOT assert specific hash values (those are corpus-dependent) — they verify
/// that the extraction plumbing (PDFtoImage render → Bgra32 → Rgba32 → CoenM pHash)
/// runs without error and produces the expected count and non-zero values.
/// </para>
/// <para>
/// The multi-page PDF is built in-memory via <c>PdfDocumentBuilder</c> so no external
/// fixture file is required.  The fixture PDFs under <c>Fixtures/</c> are used for the
/// flag-OFF regression to confirm the default behaviour is truly empty.
/// </para>
/// </remarks>
public sealed class PerceptualHashSmokeTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    /// <summary>Creates an extractor with the hashing flag OFF (default).</summary>
    private static PdfPigStatementFieldExtractor CreateExtractorHashingOff() =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    /// <summary>Creates an extractor with the hashing flag ON.</summary>
    private static PdfPigStatementFieldExtractor CreateExtractorHashingOn() =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider(),
            timeProvider: null,
            enableCatalogImageHashing: true);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds an in-memory multi-page PDF with <paramref name="pageCount"/> pages.
    /// Each page has a unique text label so the rendered bitmaps are visually distinct.
    /// Uses A4 dimensions (595 × 842 PDF points).
    /// </summary>
    private static byte[] BuildMultiPagePdf(int pageCount)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        for (var i = 1; i <= pageCount; i++)
        {
            var page = builder.AddPage(595, 842);
            page.AddText($"Page {i} of {pageCount} — VERIQAN-E2-S4 perceptual hash test", 14,
                new PdfPoint(50, 750), font);
            page.AddText($"Unique content block: {Guid.NewGuid():N}", 10,
                new PdfPoint(50, 700), font);
        }

        return builder.Build();
    }

    // -----------------------------------------------------------------------
    // Test 1 — Flag ON: hashes populated with correct count and non-zero values
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <c>enableCatalogImageHashing = true</c>, <see cref="StatementModel.PagePerceptualHashes"/>
    /// must contain exactly one hash per page and every hash must be non-zero.
    /// </summary>
    /// <remarks>
    /// Uses a 3-page in-memory PDF so the test is self-contained and does not depend on
    /// external fixture files that may not exercise the full page-count path.
    /// If PDFium cannot load its native library on this box, PDFtoImage will throw and
    /// the extractor's per-page catch block will log and skip — the test will then fail
    /// with a count-mismatch assertion, and the failure message will contain the native
    /// load error from the extractor log output, making the root cause clear.
    /// </remarks>
    [Fact]
#pragma warning disable CA1416 // PDFtoImage is cross-platform
    public async Task ExtractFullAsync_HashingFlagOn_PopulatesPagePerceptualHashesWithCorrectCount()
    {
        const int pageCount = 3;
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = BuildMultiPagePdf(pageCount);
        var extractor = CreateExtractorHashingOn();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;
        var hashes = model.PagePerceptualHashes;

        // Log raw hash values for diagnostic purposes.
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"PagePerceptualHashes count: {hashes.Count} (expected {pageCount})");
        for (var i = 0; i < hashes.Count; i++)
            output?.WriteLine($"  page {i + 1}: 0x{hashes[i]:X16}");

        // Count must equal the number of pages — every page rendered successfully.
        hashes.Count.ShouldBe(
            pageCount,
            $"Expected one hash per page ({pageCount} pages). " +
            "A count mismatch typically means PDFium native loading failed on this box " +
            "— check the extractor log output for 'PerceptualHash: failed to render or hash page'.");

        // Every hash must be non-zero — a zero hash would mean the rendered bitmap was blank.
        for (var i = 0; i < hashes.Count; i++)
        {
            hashes[i].ShouldNotBe(0UL,
                $"Page {i + 1} hash is zero — the rendered bitmap was blank or the pHash " +
                "computation returned an unexpected zero for a non-blank page.");
        }
    }
#pragma warning restore CA1416

    // -----------------------------------------------------------------------
    // Test 2 — Flag OFF (default): hashes list stays empty
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <c>enableCatalogImageHashing</c> is not set (default <see langword="false"/>),
    /// <see cref="StatementModel.PagePerceptualHashes"/> must be empty regardless of the
    /// PDF content.  This ensures the <c>CatalogImagePresenceRule</c> abstains (never
    /// produces a false FAIL) on normal extraction paths.
    /// Uses the real jul_ago Dummie VEC fixture to confirm the default production path.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_HashingFlagOff_PagePerceptualHashesIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);

        // Default extractor — hashing OFF.
        var extractor = CreateExtractorHashingOff();
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;

        model.PagePerceptualHashes.ShouldNotBeNull(
            "PagePerceptualHashes must never be null (init = empty list).");
        model.PagePerceptualHashes.Count.ShouldBe(
            0,
            "With enableCatalogImageHashing=false (default), PagePerceptualHashes must remain " +
            "empty so that CatalogImagePresenceRule abstains rather than running on missing data.");
    }
}

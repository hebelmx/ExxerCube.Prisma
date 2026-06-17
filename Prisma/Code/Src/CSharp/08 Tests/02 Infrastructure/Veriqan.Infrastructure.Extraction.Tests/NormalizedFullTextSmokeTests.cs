using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Smoke tests for the <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/>
/// property (Story 6.1 — CL-32/46).
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that the extraction plumbing populates
/// <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> and report the
/// presence or absence of two compliance-relevant strings in the <c>jul_ago</c> fixture:
/// <list type="bullet">
///   <item><description>"COMPARA TU TARJETA" — required section heading (CL-32).</description></item>
///   <item><description>"ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA SIN VALIDEZ FISCAL"
///         — example mandatory legend (CL-46).</description></item>
/// </list>
/// </para>
/// <para>
/// The compliance assertions (Pass/Fail) are NOT enforced here — this is a diagnostic
/// smoke test only.  The actual compliance rules live in <c>Cl32ComparaTuTarjetaRule</c>
/// and <c>Cl46MandatoryLegendsRule</c> (tested in <c>Validation.Tests/LegendRulesTests</c>).
/// </para>
/// </remarks>
public sealed class NormalizedFullTextSmokeTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    // -----------------------------------------------------------------------
    // Candidate strings to search — normalized to match stored form.
    // -----------------------------------------------------------------------

    /// <summary>CL-32 mandatory section heading (already in normalized form).</summary>
    private const string ComparaTuTarjeta = "COMPARA TU TARJETA";

    /// <summary>
    /// Example mandatory legend from the reference bundle (accent-stripped, upper-case).
    /// Original (with accents): "ESTE DOCUMENTO ES UNA REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL"
    /// </summary>
    private const string FiscalLegend =
        "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA SIN VALIDEZ FISCAL";

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs <c>ExtractFullAsync</c> against the jul_ago fixture and verifies that
    /// <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is non-empty.
    /// Logs whether the two compliance strings are found in the fixture.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgoFixture_PopulatesNormalizedFullText()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        var output = TestContext.Current.TestOutputHelper;

        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at: {fixturePath}");
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);

        var extractor = CreateExtractor();
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");

        var model = result.Value!;

        // Core assertion: NormalizedFullText must be populated.
        model.NormalizedFullText.ShouldNotBeNull(
            "NormalizedFullText must never be null.");
        model.NormalizedFullText.ShouldNotBeEmpty(
            "NormalizedFullText must be non-empty for a PDF with a text layer.");

        output?.WriteLine(
            $"[jul_ago] NormalizedFullText length: {model.NormalizedFullText.Length} chars");

        // Log the first 200 chars of the normalized text for diagnostic visibility.
        var preview = model.NormalizedFullText.Length > 200
            ? model.NormalizedFullText[..200] + "…"
            : model.NormalizedFullText;
        output?.WriteLine($"[jul_ago] NormalizedFullText preview: {preview}");

        // ---- Compliance-indicator findings (NOT hard assertions — diagnostic only) ----

        var comparaFound = model.NormalizedFullText.Contains(
            ComparaTuTarjeta, StringComparison.Ordinal);
        var legendFound = model.NormalizedFullText.Contains(
            FiscalLegend, StringComparison.Ordinal);

        output?.WriteLine(
            $"[jul_ago] 'COMPARA TU TARJETA' found: {comparaFound} (CL-32 indicator)");
        output?.WriteLine(
            $"[jul_ago] Fiscal legend found:         {legendFound} (CL-46 indicator — example legend)");

        // Diagnostic: log whether individual words that should be in any VEC are present.
        // This helps distinguish "text layer absent" from "sections not present".
        var hasBasicWords = model.NormalizedFullText.Contains("TARJETA", StringComparison.Ordinal)
                         || model.NormalizedFullText.Contains("CREDITO", StringComparison.Ordinal)
                         || model.NormalizedFullText.Contains("ESTADO", StringComparison.Ordinal);

        output?.WriteLine(
            $"[jul_ago] Basic VEC word found (sanity): {hasBasicWords}");

        // The NormalizedFullText should contain at least basic VEC terminology for a valid fixture.
        hasBasicWords.ShouldBeTrue(
            "NormalizedFullText should contain basic VEC terms (TARJETA / CREDITO / ESTADO) " +
            "for the jul_ago Dummie VEC fixture.");
    }

    /// <summary>
    /// Verifies the <see cref="PdfPigStatementFieldExtractor.NormalizeText"/> helper
    /// directly: accent stripping and upper-casing work as documented.
    /// </summary>
    [Theory]
    [InlineData("compara tu tarjeta",                    "COMPARA TU TARJETA")]
    [InlineData("Compara tú Tarjeta",                    "COMPARA TU TARJETA")]
    [InlineData("representación impresa",                "REPRESENTACION IMPRESA")]
    [InlineData("REPRESENTACIÓN IMPRESA SIN VALIDEZ",   "REPRESENTACION IMPRESA SIN VALIDEZ")]
    [InlineData("   múltiple   espacios   ",             "MULTIPLE ESPACIOS")]
    [InlineData("",                                      "")]
    [InlineData("   ",                                   "")]
    public void NormalizeText_VariousInputs_ProducesExpectedNormalizedForm(
        string input, string expected)
    {
        var actual = PdfPigStatementFieldExtractor.NormalizeText(input);
        actual.ShouldBe(expected);
    }
}

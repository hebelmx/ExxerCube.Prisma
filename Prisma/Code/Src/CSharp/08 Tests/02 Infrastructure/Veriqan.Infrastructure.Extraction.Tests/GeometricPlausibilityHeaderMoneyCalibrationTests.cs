using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story <b>C2.1b</b> — production-wiring calibration for the recompute-operand HeaderMoney
/// (<c>SaldoCargosRegulares</c>/<c>PagoParaNoGenerarIntereses</c>) geometric-plausibility slice,
/// mirroring <c>GeometricPlausibilityTotalRowCalibrationTests</c>'s C1.6 pattern. Runs the REAL,
/// unmodified <see cref="PdfPigStatementFieldExtractor"/> (armed via the
/// <c>emitGeometricConfidence</c> constructor flag) end-to-end over the C2.0a/C2.1a synthetic
/// corpus specimens, asserting the same C1.0b/C2.0b bar: <c>min(clean) &gt;= 0.8</c>,
/// <c>max(decoy) &lt; 0.8</c>, <c>margin &gt;= 0.15</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>DARK-capable, ARMED by default</b>: every fixture is also round-tripped with the flag OFF
/// to prove the flag-off path stays at the pre-C2.1b constant confidence of 1.0 (byte-identical
/// behaviour) — see <see cref="FlagOff_EveryCleanAndDecoySpecimen_ReportsConstantCeilingConfidence"/>.
/// Production itself now defaults <c>PdfExtractionOptions.EmitGeometricConfidence</c> to
/// <see langword="true"/> (C1.7 owner ruling), so the flag-ON assertions below are the live path.
/// </para>
/// <para>
/// <b>Both profiles</b> (dummievec + realbanamex) and both C2.1a hardening tiers (plain digit
/// decoy + money-formatted decoy + footnote-marker clean) are exercised — the footnote specimens
/// are the AC1 regression guard: a harmless single-digit superscript marker in the value band
/// must NOT drag a genuinely clean pick below the 0.8 floor.
/// </para>
/// </remarks>
public sealed class GeometricPlausibilityHeaderMoneyCalibrationTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic");

    private static string FixturePath(string fileName) => Path.Combine(FixturesDir, fileName);

    private static PdfPigStatementFieldExtractor CreateExtractor(bool emitGeometricConfidence) =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider(),
            timeProvider: null,
            enableCatalogImageHashing: false,
            emitGeometricConfidence: emitGeometricConfidence);

    private static readonly IReadOnlyDictionary<string, Func<PeriodSummary, ExtractedField<decimal>>> HeaderMoneyAccessors =
        new Dictionary<string, Func<PeriodSummary, ExtractedField<decimal>>>
        {
            ["SaldoCargosRegulares"] = ps => ps.SaldoCargosRegulares,
            ["PagoParaNoGenerarIntereses"] = ps => ps.PagoParaNoGenerarIntereses,
        };

    private static async Task<PeriodSummary> ExtractPeriodSummaryAsync(
        string fileName,
        bool emitGeometricConfidence,
        CancellationToken ct)
    {
        var pdfPath = FixturePath(fileName);
        File.Exists(pdfPath).ShouldBeTrue($"Synthetic fixture not found: {pdfPath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);
        var extractor = CreateExtractor(emitGeometricConfidence);
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed for {fileName}. Error: {result.Error}");

        var periodSummary = result.Value!.PeriodSummary;
        periodSummary.ShouldNotBeNull($"{fileName}: PeriodSummary must be populated.");
        return periodSummary!;
    }

    private static ExtractedField<decimal> Field(PeriodSummary ps, string fieldName) =>
        HeaderMoneyAccessors[fieldName](ps);

    // -----------------------------------------------------------------------
    // Flag OFF — behaviour-neutral gate (C2.1b's arm-with-a-killswitch contract)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("clean-nivel-pago.pdf")]
    [InlineData("clean-nivel-pago-realbanamex.pdf")]
    [InlineData("clean-nivel-pago-footnote.pdf")]
    [InlineData("clean-nivel-pago-footnote-realbanamex.pdf")]
    [InlineData("decoy-nivel-uso-amount.pdf")]
    [InlineData("decoy-nivel-uso-amount-realbanamex.pdf")]
    [InlineData("decoy-nivel-uso-amount-moneyfmt.pdf")]
    [InlineData("decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf")]
    [InlineData("decoy-pago-sin-intereses-amount.pdf")]
    [InlineData("decoy-pago-sin-intereses-amount-realbanamex.pdf")]
    public async Task FlagOff_EveryCleanAndDecoySpecimen_ReportsConstantCeilingConfidence(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: false, ct);

        foreach (var fieldName in HeaderMoneyAccessors.Keys)
        {
            var field = Field(periodSummary, fieldName);
            if (field.Status != ExtractionStatus.Extracted)
                continue; // nothing to score on this fixture.

            field.Confidence.ShouldBe(1.0, $"{fileName}/{fieldName}: flag-off must be byte-identical");
        }
    }

    // -----------------------------------------------------------------------
    // Flag ON — clean baseline picks (including the C2.1a footnote guard) must
    // clear the 0.8 guard floor.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("clean-nivel-pago.pdf")]
    [InlineData("clean-nivel-pago-realbanamex.pdf")]
    [InlineData("clean-nivel-pago-footnote.pdf")]
    [InlineData("clean-nivel-pago-footnote-realbanamex.pdf")]
    public async Task FlagOn_CleanSpecimen_BothHeaderMoneyFieldsScoreAtCeiling(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: true, ct);

        foreach (var fieldName in HeaderMoneyAccessors.Keys)
        {
            var field = Field(periodSummary, fieldName);
            field.Status.ShouldBe(ExtractionStatus.Extracted, $"{fileName}/{fieldName} must extract on a clean specimen.");
            field.Confidence.ShouldBe(1.0, $"{fileName}/{fieldName}: clean pick must score at ceiling");
        }
    }

    // -----------------------------------------------------------------------
    // Flag ON — the decoy pick must abstain-gate below the 0.8 guard floor;
    // its co-located, undisturbed sibling on the SAME fixture must stay clean.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("decoy-nivel-uso-amount.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses")]
    [InlineData("decoy-nivel-uso-amount-realbanamex.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses")]
    [InlineData("decoy-nivel-uso-amount-moneyfmt.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses")]
    [InlineData("decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses")]
    [InlineData("decoy-pago-sin-intereses-amount.pdf", "PagoParaNoGenerarIntereses", "SaldoCargosRegulares")]
    [InlineData("decoy-pago-sin-intereses-amount-realbanamex.pdf", "PagoParaNoGenerarIntereses", "SaldoCargosRegulares")]
    public async Task FlagOn_DecoySpecimen_MisreadFieldScoresBelowFloor_CoLocatedSiblingStaysClean(
        string fileName, string misreadField, string coLocatedField)
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: true, ct);

        var misread = Field(periodSummary, misreadField);
        misread.Status.ShouldBe(ExtractionStatus.Extracted, "the decoy is still a confident (wrong) pick.");
        misread.Confidence.ShouldBeLessThan(
            0.8, $"{fileName}/{misreadField}: the decoy-competed pick must abstain-gate below the 0.8 guard floor");

        var coLocated = Field(periodSummary, coLocatedField);
        coLocated.Status.ShouldBe(ExtractionStatus.Extracted);
        coLocated.Confidence.ShouldBe(1.0, $"{fileName}/{coLocatedField}: co-located sibling must stay at ceiling");
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the full clean/decoy partition, production wiring
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verdict_ProductionHeaderMoneyScorer_MeetsTheC2_0bSeparationBar()
    {
        var ct = TestContext.Current.CancellationToken;

        var cleanScores = new List<double>();
        foreach (var fileName in new[]
                 {
                     "clean-nivel-pago.pdf",
                     "clean-nivel-pago-realbanamex.pdf",
                     "clean-nivel-pago-footnote.pdf",
                     "clean-nivel-pago-footnote-realbanamex.pdf",
                 })
        {
            var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: true, ct);
            foreach (var fieldName in HeaderMoneyAccessors.Keys)
                cleanScores.Add(Field(periodSummary, fieldName).Confidence);
        }

        var decoyScores = new List<double>();
        foreach (var (fileName, misreadField, coLocatedField) in new[]
                 {
                     ("decoy-nivel-uso-amount.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses"),
                     ("decoy-nivel-uso-amount-realbanamex.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses"),
                     ("decoy-nivel-uso-amount-moneyfmt.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses"),
                     ("decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf", "SaldoCargosRegulares", "PagoParaNoGenerarIntereses"),
                     ("decoy-pago-sin-intereses-amount.pdf", "PagoParaNoGenerarIntereses", "SaldoCargosRegulares"),
                     ("decoy-pago-sin-intereses-amount-realbanamex.pdf", "PagoParaNoGenerarIntereses", "SaldoCargosRegulares"),
                 })
        {
            var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: true, ct);
            decoyScores.Add(Field(periodSummary, misreadField).Confidence);
            cleanScores.Add(Field(periodSummary, coLocatedField).Confidence); // co-located undisturbed sibling
        }

        var minClean = cleanScores.Min();
        var maxDecoy = decoyScores.Max();
        var margin = minClean - maxDecoy;

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxDecoy.ShouldBeLessThan(0.8, "the decoy pick must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar the C1.0b/C2.0b spikes proved");
    }
}

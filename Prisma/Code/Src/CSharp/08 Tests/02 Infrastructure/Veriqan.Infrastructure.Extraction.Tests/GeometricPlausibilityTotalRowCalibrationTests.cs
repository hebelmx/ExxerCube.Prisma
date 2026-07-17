using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story <b>C1.6</b> — production-wiring calibration for the DESGLOSE total-row
/// (<c>TotalCargos</c>/<c>TotalAbonos</c>) geometric-plausibility slice, mirroring
/// <see cref="GeometricPlausibilityResumenCalibrationTests"/>'s C1.4 pattern. Runs the REAL,
/// unmodified <see cref="PdfPigStatementFieldExtractor"/> (armed via the
/// <c>emitGeometricConfidence</c> constructor flag) end-to-end over
/// <c>s6211-desglose.pdf</c> (existing clean baseline) and the new
/// <c>decoy-total-amount</c>/<c>decoy-total-amount-realbanamex</c> specimens, asserting the same
/// C1.0b bar: <c>min(clean) &gt;= 0.8</c>, <c>max(ambiguous) &lt; 0.8</c>, <c>margin &gt;= 0.15</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>DARK by default</b>: every fixture is also round-tripped with the flag OFF to prove the
/// flag-off path stays at the pre-C1.6 constant confidence of 1.0 (byte-identical behaviour) —
/// see <see cref="FlagOff_EveryCleanAndDecoySpecimen_ReportsConstantCeilingConfidence"/>.
/// </para>
/// <para>
/// <b>Both profiles</b> (dummievec + realbanamex): <c>decoy-total-amount.pdf</c> and
/// <c>decoy-total-amount-realbanamex.pdf</c> exercise <c>TryParseTotalRow</c> on each profile's
/// own page geometry. Unlike C1.4's RESUMEN pair, <c>TryParseTotalRow</c> has ONE shared code
/// path (no left/right dual-pass split — see the generator's module comment in
/// <c>scripts/veriqan-corpus/synth_gen.py</c>), so the realbanamex specimen proves generalization
/// to the real-Banamex page geometry, not a distinct extractor code path.
/// </para>
/// <para>
/// <b>Only one true "clean total row" specimen exists for the dummievec profile</b>
/// (<c>s6211-desglose.pdf</c>, pre-existing, S6.2.5) — there is no standalone realbanamex clean
/// total-row baseline; the realbanamex profile's only clean data point is the undisturbed
/// <c>TotalCargos</c> sibling inside <c>decoy-total-amount-realbanamex.pdf</c> itself. Documented
/// honestly here rather than overclaiming a second independent baseline.
/// </para>
/// </remarks>
public sealed class GeometricPlausibilityTotalRowCalibrationTests
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

    private static readonly IReadOnlyDictionary<string, Func<PeriodSummary, ExtractedField<decimal>>> TotalRowAccessors =
        new Dictionary<string, Func<PeriodSummary, ExtractedField<decimal>>>
        {
            ["TotalCargos"] = ps => ps.TotalCargos,
            ["TotalAbonos"] = ps => ps.TotalAbonos,
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
        TotalRowAccessors[fieldName](ps);

    // -----------------------------------------------------------------------
    // Flag OFF — behaviour-neutral gate (C1.6's ship-dark contract)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s6211-desglose.pdf")]
    [InlineData("decoy-total-amount.pdf")]
    [InlineData("decoy-total-amount-realbanamex.pdf")]
    public async Task FlagOff_EveryCleanAndDecoySpecimen_ReportsConstantCeilingConfidence(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: false, ct);

        foreach (var fieldName in TotalRowAccessors.Keys)
        {
            var field = Field(periodSummary, fieldName);
            if (field.Status != ExtractionStatus.Extracted)
                continue; // nothing to score on this fixture.

            field.Confidence.ShouldBe(1.0, $"{fileName}/{fieldName}: flag-off must be byte-identical");
        }
    }

    // -----------------------------------------------------------------------
    // Flag ON — clean baseline picks must clear the 0.8 guard floor
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FlagOn_S6211Desglose_BothTotalFieldsScoreAtCeiling()
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync("s6211-desglose.pdf", emitGeometricConfidence: true, ct);

        foreach (var fieldName in TotalRowAccessors.Keys)
        {
            var field = Field(periodSummary, fieldName);
            field.Status.ShouldBe(ExtractionStatus.Extracted, $"{fieldName} must extract on the clean baseline.");
            field.Confidence.ShouldBe(1.0, $"{fieldName}: clean pick must score at ceiling");
        }
    }

    // -----------------------------------------------------------------------
    // Flag ON — the decoy pick must abstain-gate below the 0.8 guard floor;
    // its undisturbed sibling on the SAME fixture must stay clean.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FlagOn_DecoyTotalAmount_Dummievec_TotalAbonosScoresBelowFloor_TotalCargosStaysClean()
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(
            "decoy-total-amount.pdf", emitGeometricConfidence: true, ct);

        var abonos = Field(periodSummary, "TotalAbonos");
        abonos.Status.ShouldBe(ExtractionStatus.Extracted, "the decoy is still a confident (wrong) pick.");
        abonos.Confidence.ShouldBeLessThan(
            0.8, "the decoy-competed pick must abstain-gate below the 0.8 guard floor");

        var cargos = Field(periodSummary, "TotalCargos");
        cargos.Status.ShouldBe(ExtractionStatus.Extracted);
        cargos.Confidence.ShouldBe(1.0, "TotalCargos: undisturbed sibling must stay at ceiling");
    }

    [Fact]
    public async Task FlagOn_DecoyTotalAmount_Realbanamex_TotalAbonosScoresBelowFloor_TotalCargosStaysClean()
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(
            "decoy-total-amount-realbanamex.pdf", emitGeometricConfidence: true, ct);

        var abonos = Field(periodSummary, "TotalAbonos");
        abonos.Status.ShouldBe(ExtractionStatus.Extracted, "the decoy is still a confident (wrong) pick.");
        abonos.Confidence.ShouldBeLessThan(
            0.8, "the decoy-competed pick must abstain-gate below the 0.8 guard floor");

        var cargos = Field(periodSummary, "TotalCargos");
        cargos.Status.ShouldBe(ExtractionStatus.Extracted);
        cargos.Confidence.ShouldBe(1.0, "TotalCargos: undisturbed sibling must stay at ceiling");
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the full clean/decoy partition, production wiring
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verdict_ProductionTotalRowScorer_MeetsTheC1_0bSeparationBar()
    {
        var ct = TestContext.Current.CancellationToken;

        var cleanScores = new List<double>();

        var baseline = await ExtractPeriodSummaryAsync("s6211-desglose.pdf", emitGeometricConfidence: true, ct);
        foreach (var fieldName in TotalRowAccessors.Keys)
            cleanScores.Add(Field(baseline, fieldName).Confidence);

        var decoyDummievec = await ExtractPeriodSummaryAsync(
            "decoy-total-amount.pdf", emitGeometricConfidence: true, ct);
        cleanScores.Add(Field(decoyDummievec, "TotalCargos").Confidence);

        var decoyRealbanamex = await ExtractPeriodSummaryAsync(
            "decoy-total-amount-realbanamex.pdf", emitGeometricConfidence: true, ct);
        cleanScores.Add(Field(decoyRealbanamex, "TotalCargos").Confidence);

        var ambiguousScores = new List<double>
        {
            Field(decoyDummievec, "TotalAbonos").Confidence,
            Field(decoyRealbanamex, "TotalAbonos").Confidence,
        };

        var minClean = cleanScores.Min();
        var maxAmbiguous = ambiguousScores.Max();
        var margin = minClean - maxAmbiguous;

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxAmbiguous.ShouldBeLessThan(0.8, "the decoy pick must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar the C1.0b spike proved");
    }
}

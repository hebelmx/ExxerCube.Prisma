using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story <b>C1.4</b> — production-wiring calibration for the RESUMEN/NIVEL money-field
/// geometric-plausibility slice (signal #1, label-to-pick rank adjacency), mirroring
/// <see cref="GeometricPlausibilityCalibrationTests"/>'s C1.2 pattern for Tasa/Cat. Runs the REAL,
/// unmodified <see cref="PdfPigStatementFieldExtractor"/> (armed via the
/// <c>emitGeometricConfidence</c> constructor flag) end-to-end over the
/// <c>decoy-resumen-amount</c>/<c>decoy-resumen-amount-realbanamex</c> specimens and asserts the
/// same C1.0b bar: <c>min(clean) &gt;= 0.8</c>, <c>max(ambiguous) &lt; 0.8</c>,
/// <c>margin &gt;= 0.15</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>DARK by default</b>: every fixture is also round-tripped with the flag OFF to prove the
/// flag-off path stays at the pre-C1.4 constant confidence of 1.0 (byte-identical behaviour) —
/// see <see cref="FlagOff_EveryCleanAndDecoySpecimen_ReportsConstantCeilingConfidence"/>.
/// </para>
/// <para>
/// <b>Both profiles</b> (Mary's C1 tracker non-negotiable): <c>decoy-resumen-amount.pdf</c>
/// exercises the dummievec RIGHT-column <c>ScanResumenColumn</c> pass;
/// <c>decoy-resumen-amount-realbanamex.pdf</c> exercises the real-Banamex LEFT-column pass — a
/// genuinely different code path (<c>labelMinX</c>/<c>labelMaxX</c>/<c>amtMaxX</c> bounds differ).
/// </para>
/// </remarks>
public sealed class GeometricPlausibilityResumenCalibrationTests
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

    /// <summary>
    /// Every RESUMEN money field wired to the C1.4 scorer, keyed by its <see cref="PeriodSummary"/>
    /// accessor — same 7 fields as <c>FieldCalibrationTable.Resumen</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<PeriodSummary, ExtractedField<decimal>>> ResumenAccessors =
        new Dictionary<string, Func<PeriodSummary, ExtractedField<decimal>>>
        {
            ["AdeudoPeriodoAnterior"] = ps => ps.AdeudoPeriodoAnterior,
            ["CargosRegularesNoMeses"] = ps => ps.CargosRegularesNoMeses,
            ["CargosComprasAMesesCapital"] = ps => ps.CargosComprasAMesesCapital,
            ["MontoIntereses"] = ps => ps.MontoIntereses,
            ["MontoComisiones"] = ps => ps.MontoComisiones,
            ["IvaInteresesYComisiones"] = ps => ps.IvaInteresesYComisiones,
            ["PagosYAbonos"] = ps => ps.PagosYAbonos,
        };

    private static readonly string[] CleanFieldNames =
    [
        "CargosRegularesNoMeses",
        "CargosComprasAMesesCapital",
        "MontoIntereses",
        "MontoComisiones",
        "IvaInteresesYComisiones",
        "PagosYAbonos",
    ];

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

    private static double Confidence(PeriodSummary ps, string fieldName)
    {
        var field = ResumenAccessors[fieldName](ps);
        field.Status.ShouldBe(ExtractionStatus.Extracted, $"{fieldName} must extract.");
        return field.Confidence;
    }

    // -----------------------------------------------------------------------
    // Flag OFF — behaviour-neutral gate (C1.4's ship-dark contract)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s6211-baseline.pdf")]
    [InlineData("s622-realbanamex-baseline.pdf")]
    [InlineData("decoy-resumen-amount.pdf")]
    [InlineData("decoy-resumen-amount-realbanamex.pdf")]
    public async Task FlagOff_EveryCleanAndDecoySpecimen_ReportsConstantCeilingConfidence(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: false, ct);

        foreach (var fieldName in ResumenAccessors.Keys)
        {
            Confidence(periodSummary, fieldName).ShouldBe(1.0, $"{fileName}/{fieldName}: flag-off must be byte-identical");
        }
    }

    // -----------------------------------------------------------------------
    // Flag ON — clean baseline picks must clear the 0.8 guard floor
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s6211-baseline.pdf")]
    [InlineData("s622-realbanamex-baseline.pdf")]
    public async Task FlagOn_BaselineSpecimen_EveryResumenFieldScoresAtCeiling(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(fileName, emitGeometricConfidence: true, ct);

        foreach (var fieldName in ResumenAccessors.Keys)
        {
            Confidence(periodSummary, fieldName).ShouldBe(1.0, $"{fileName}/{fieldName}: clean pick must score at ceiling");
        }
    }

    // -----------------------------------------------------------------------
    // Flag ON — the decoy pick must abstain-gate below the 0.8 guard floor;
    // its 6 undisturbed siblings on the SAME fixture must stay clean.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FlagOn_DecoyResumenAmount_Dummievec_AdeudoScoresBelowFloor_SiblingsStayClean()
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(
            "decoy-resumen-amount.pdf", emitGeometricConfidence: true, ct);

        Confidence(periodSummary, "AdeudoPeriodoAnterior").ShouldBeLessThan(
            0.8, "the decoy-leaked pick must abstain-gate below the 0.8 guard floor");

        foreach (var fieldName in CleanFieldNames)
        {
            Confidence(periodSummary, fieldName).ShouldBe(1.0, $"{fieldName}: undisturbed sibling must stay at ceiling");
        }
    }

    [Fact]
    public async Task FlagOn_DecoyResumenAmount_Realbanamex_AdeudoScoresBelowFloor_SiblingsStayClean()
    {
        var ct = TestContext.Current.CancellationToken;
        var periodSummary = await ExtractPeriodSummaryAsync(
            "decoy-resumen-amount-realbanamex.pdf", emitGeometricConfidence: true, ct);

        Confidence(periodSummary, "AdeudoPeriodoAnterior").ShouldBeLessThan(
            0.8, "the decoy-leaked pick (left-column pass) must abstain-gate below the 0.8 guard floor");

        foreach (var fieldName in CleanFieldNames)
        {
            Confidence(periodSummary, fieldName).ShouldBe(1.0, $"{fieldName}: undisturbed sibling must stay at ceiling");
        }
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the full clean/decoy partition, production wiring
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verdict_ProductionResumenScorer_MeetsTheC1_0bSeparationBar()
    {
        var ct = TestContext.Current.CancellationToken;

        var cleanScores = new List<double>();

        var baseline = await ExtractPeriodSummaryAsync("s6211-baseline.pdf", emitGeometricConfidence: true, ct);
        foreach (var fieldName in ResumenAccessors.Keys)
            cleanScores.Add(Confidence(baseline, fieldName));

        var realbanamexBaseline = await ExtractPeriodSummaryAsync(
            "s622-realbanamex-baseline.pdf", emitGeometricConfidence: true, ct);
        foreach (var fieldName in ResumenAccessors.Keys)
            cleanScores.Add(Confidence(realbanamexBaseline, fieldName));

        var decoyDummievec = await ExtractPeriodSummaryAsync(
            "decoy-resumen-amount.pdf", emitGeometricConfidence: true, ct);
        foreach (var fieldName in CleanFieldNames)
            cleanScores.Add(Confidence(decoyDummievec, fieldName));

        var decoyRealbanamex = await ExtractPeriodSummaryAsync(
            "decoy-resumen-amount-realbanamex.pdf", emitGeometricConfidence: true, ct);
        foreach (var fieldName in CleanFieldNames)
            cleanScores.Add(Confidence(decoyRealbanamex, fieldName));

        var ambiguousScores = new List<double>
        {
            Confidence(decoyDummievec, "AdeudoPeriodoAnterior"),
            Confidence(decoyRealbanamex, "AdeudoPeriodoAnterior"),
        };

        var minClean = cleanScores.Min();
        var maxAmbiguous = ambiguousScores.Max();
        var margin = minClean - maxAmbiguous;

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxAmbiguous.ShouldBeLessThan(0.8, "the decoy pick must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar the C1.0b spike proved");
    }
}

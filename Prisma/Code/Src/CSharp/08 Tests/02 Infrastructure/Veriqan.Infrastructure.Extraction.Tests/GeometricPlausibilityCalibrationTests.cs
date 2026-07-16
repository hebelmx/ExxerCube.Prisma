using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story <b>C1.2</b> — production-analogue of the C1.0b make-or-break separation spike. Runs the
/// REAL, unmodified <see cref="PdfPigStatementFieldExtractor"/> (armed via the
/// <c>emitGeometricConfidence</c> constructor flag) end-to-end over the same synthetic corpus
/// specimens the spike proved separation on, and asserts the same bar:
/// <c>min(clean) &gt;= 0.8</c>, <c>max(ambiguous) &lt; 0.8</c>, <c>margin &gt;= 0.15</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the C1.0b spike (which duplicated <c>ExtractTasaAndCat</c>'s band-location logic in
/// <c>SpikeBandLocator</c> on purpose, to avoid depending on production internals before they
/// existed), this suite exercises the actual production wiring: the real extractor, the real
/// <c>GeometricPlausibilityScorer</c>, the real <c>FieldCalibrationTable</c>, through the real
/// <see cref="ExtractedField{T}.Confidence"/> the C1 lever is meant to carry. That is the point
/// of graduating from spike to production — this test would fail if C1.2's wiring diverged from
/// what C1.0b proved, even though the math is identical.
/// </para>
/// <para>
/// <b>DARK by default</b>: every fixture is also round-tripped with the flag OFF to prove the
/// flag-off path stays at the pre-C1.2 constant confidence of 1.0 (byte-identical behaviour) —
/// see <see cref="FlagOff_EveryCleanAndAmbiguousSpecimen_ReportsConstantCeilingConfidence"/>.
/// </para>
/// </remarks>
public sealed class GeometricPlausibilityCalibrationTests
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

    private static async Task<(double TasaConfidence, double CatConfidence)> ExtractTasaCatConfidenceAsync(
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

        periodSummary!.Tasa.Status.ShouldBe(ExtractionStatus.Extracted, $"{fileName}: Tasa must extract.");
        periodSummary.Cat.Status.ShouldBe(ExtractionStatus.Extracted, $"{fileName}: Cat must extract.");

        return (periodSummary.Tasa.Confidence, periodSummary.Cat.Confidence);
    }

    // -----------------------------------------------------------------------
    // Flag OFF — behaviour-neutral gate (C1.2's ship-dark contract)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s6211-baseline.pdf")]
    [InlineData("s622-realbanamex-baseline.pdf")]
    [InlineData("missing-order-marker.pdf")]
    [InlineData("decoy-percent.pdf")]
    [InlineData("s-c1-swap-displaced.pdf")]
    [InlineData("s-c1-swap.pdf")]
    public async Task FlagOff_EveryCleanAndAmbiguousSpecimen_ReportsConstantCeilingConfidence(string fileName)
    {
        // With emitGeometricConfidence=false (the default), Tasa/Cat confidence must stay the
        // pre-C1.2 constant 1.0 REGARDLESS of the specimen's geometry defect — the score is
        // computed but never surfaced. This is the byte-identical flag-off contract C1.2 promises.
        var (tasaConfidence, catConfidence) = await ExtractTasaCatConfidenceAsync(
            fileName, emitGeometricConfidence: false, TestContext.Current.CancellationToken);

        tasaConfidence.ShouldBe(1.0);
        catConfidence.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // Flag ON — clean specimens must clear the 0.8 guard floor
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s6211-baseline.pdf")]
    [InlineData("s622-realbanamex-baseline.pdf")]
    [InlineData("s6211-var-a.pdf")]
    [InlineData("s6211-var-b.pdf")]
    [InlineData("s6211-var-c.pdf")]
    public async Task FlagOn_CleanSpecimen_ScoresAtCeiling(string fileName)
    {
        var (tasaConfidence, catConfidence) = await ExtractTasaCatConfidenceAsync(
            fileName, emitGeometricConfidence: true, TestContext.Current.CancellationToken);

        tasaConfidence.ShouldBe(1.0);
        catConfidence.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // Flag ON — ambiguous specimens must abstain-gate below the 0.8 guard floor
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FlagOn_MissingOrderMarker_ScoresBelowFloor_ViaSiblingSignal()
    {
        // Extraction is unaffected/correct here — proves signal #1 fires independently of
        // whether the underlying pick is numerically right.
        var (tasaConfidence, catConfidence) = await ExtractTasaCatConfidenceAsync(
            "missing-order-marker.pdf", emitGeometricConfidence: true, TestContext.Current.CancellationToken);

        tasaConfidence.ShouldBeLessThan(0.8);
        catConfidence.ShouldBeLessThan(0.8);
    }

    [Fact]
    public async Task FlagOn_DecoyPercent_ScoresBelowFloor_ViaCompetitionSignal()
    {
        var (tasaConfidence, catConfidence) = await ExtractTasaCatConfidenceAsync(
            "decoy-percent.pdf", emitGeometricConfidence: true, TestContext.Current.CancellationToken);

        tasaConfidence.ShouldBeLessThan(0.8);
        catConfidence.ShouldBeLessThan(0.8);
    }

    [Fact]
    public async Task FlagOn_MarkerDisplacedSwap_ScoresBelowFloor()
    {
        // The realistic swap the C1.0b holdout gate proved catchable — the negative control
        // (central-marker swap, s-c1-swap) is intentionally excluded below because it is
        // documented as geometrically invisible to this lever.
        var (tasaConfidence, catConfidence) = await ExtractTasaCatConfidenceAsync(
            "s-c1-swap-displaced.pdf", emitGeometricConfidence: true, TestContext.Current.CancellationToken);

        tasaConfidence.ShouldBeLessThan(0.8);
        catConfidence.ShouldBeLessThan(0.8);
    }

    [Fact]
    public async Task FlagOn_CentralMarkerSwap_NegativeControl_RemainsAtCeiling()
    {
        // s-c1-swap ("27.36% sin IVA 28.86%") transposes CAT/TASA but keeps 'sin IVA' centred
        // between the two tokens exactly like a clean read — documented (C1.0a CRITICAL FINDING)
        // as geometrically invisible to any position-only signal. This PASSES by staying at 1.0;
        // that is the expected, honest blind spot, not a defect in this scorer.
        var (tasaConfidence, catConfidence) = await ExtractTasaCatConfidenceAsync(
            "s-c1-swap.pdf", emitGeometricConfidence: true, TestContext.Current.CancellationToken);

        tasaConfidence.ShouldBe(1.0);
        catConfidence.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the full clean/ambiguous partition, production wiring
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verdict_ProductionScorer_MeetsTheC1_0bSeparationBar()
    {
        var ct = TestContext.Current.CancellationToken;

        var cleanFiles = new[]
        {
            "s6211-baseline.pdf",
            "s622-realbanamex-baseline.pdf",
            "s6211-var-a.pdf",
            "s6211-var-b.pdf",
            "s6211-var-c.pdf",
        };

        var ambiguousFiles = new[]
        {
            "missing-order-marker.pdf",
            "decoy-percent.pdf",
            "s-c1-swap-displaced.pdf",
        };

        var cleanScores = new List<double>();
        foreach (var file in cleanFiles)
        {
            var (tasa, cat) = await ExtractTasaCatConfidenceAsync(file, emitGeometricConfidence: true, ct);
            cleanScores.Add(tasa);
            cleanScores.Add(cat);
        }

        var ambiguousScores = new List<double>();
        foreach (var file in ambiguousFiles)
        {
            var (tasa, cat) = await ExtractTasaCatConfidenceAsync(file, emitGeometricConfidence: true, ct);
            ambiguousScores.Add(tasa);
            ambiguousScores.Add(cat);
        }

        var minClean = cleanScores.Min();
        var maxAmbiguous = ambiguousScores.Max();
        var margin = minClean - maxAmbiguous;

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxAmbiguous.ShouldBeLessThan(0.8, "ambiguous picks must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar the C1.0b spike proved");
    }
}

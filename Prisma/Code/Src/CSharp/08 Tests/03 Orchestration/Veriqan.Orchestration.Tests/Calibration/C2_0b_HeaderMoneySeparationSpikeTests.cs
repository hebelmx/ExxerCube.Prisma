using System.IO;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

/// <summary>
/// Story <b>C2.0b</b> — the MAKE-OR-BREAK separation spike for the two RECOMPUTE-OPERAND
/// money fields (SaldoCargosRegulares, PagoParaNoGenerarIntereses). Proves (or refutes) that a
/// geometric-plausibility score exists on the synthetic corpus such that:
/// <c>min(clean) &gt;= 0.8</c>, <c>max(decoy) &lt; 0.8</c>, <c>margin &gt;= 0.15</c>, per field, on a
/// TRAIN/HOLDOUT split, surviving a &#177;3pt bounding-box perturbation.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a spike, not production code.</b> <see cref="HeaderMoneyPlausibilityScorerPrototype"/>
/// exists only to prove separation and hand C2.1 the settled mechanism/constants — it is not wired
/// into <c>PdfPigStatementFieldExtractor</c> or <c>ExtractedField</c>.
/// </para>
/// <para>
/// <b>Train/holdout discipline (mirrors C1.0b):</b> scoring constants
/// (<see cref="HeaderMoneyPlausibilityScorerPrototype.RankNotAdjacentPenalty"/>,
/// <see cref="HeaderMoneyPlausibilityScorerPrototype.CompetingAmountPenalty"/>,
/// <see cref="HeaderMoneyPlausibilityScorerPrototype.RankAdjacencyThreshold"/>) were frozen using
/// ONLY the TRAIN (dummievec) fixtures: <c>clean-nivel-pago</c>, <c>decoy-nivel-uso-amount</c>,
/// <c>decoy-pago-sin-intereses-amount</c>. The HOLDOUT (realbanamex) fixtures below —
/// <c>clean-nivel-pago-realbanamex</c>, <c>decoy-nivel-uso-amount-realbanamex</c>,
/// <c>decoy-pago-sin-intereses-amount-realbanamex</c> — were never inspected while choosing those
/// constants; the Verdict tests below are the blind gate.
/// </para>
/// </remarks>
public sealed class C2_0b_HeaderMoneySeparationSpikeTests
{
    private readonly ITestOutputHelper _output;

    public C2_0b_HeaderMoneySeparationSpikeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // -----------------------------------------------------------------------
    // Fixture path resolution — same pattern as C1_0b_SeparationSpikeTests.
    // -----------------------------------------------------------------------

    private static string? GetThisFilePath([System.Runtime.CompilerServices.CallerFilePath] string? path = null) => path;

    private static readonly string RepoRoot = ComputeRepoRoot()
        ?? throw new InvalidOperationException("C2.0b spike: could not locate repo root (CLAUDE.md marker not found).");

    private static string? ComputeRepoRoot()
    {
        var startCandidates = new[]
        {
            GetThisFilePath() is { } f ? Path.GetDirectoryName(f) : null,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var start in startCandidates)
        {
            if (start is null) continue;
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        return null;
    }

    private static string FixturePath(string fileName) =>
        Path.Combine(RepoRoot, "Prisma", "Fixtures", "PRP2", "synthetic", fileName);

    // -----------------------------------------------------------------------
    // TRAIN set (constants frozen against these three dummievec fixtures only)
    // -----------------------------------------------------------------------

    [Fact]
    public void TrainClean_CleanNivelPago_SaldoCargosRegulares_ScoresAtCeiling()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("clean-nivel-pago.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago/SaldoCargosRegulares: score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeTrue();
        result.CandidateCount.ShouldBe(1);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void TrainClean_CleanNivelPago_PagoParaNoGenerarIntereses_ScoresAtCeiling()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("clean-nivel-pago.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago/PagoParaNoGenerarIntereses: score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeTrue();
        result.CandidateCount.ShouldBe(1);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void TrainDecoy_DecoyNivelUsoAmount_SaldoCargosRegulares_ScoresBelowFloor()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("decoy-nivel-uso-amount.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount/SaldoCargosRegulares: score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeFalse();
        result.CandidateCount.ShouldBe(2);
        result.Score.ShouldBe(
            HeaderMoneyPlausibilityScorerPrototype.RankNotAdjacentPenalty * HeaderMoneyPlausibilityScorerPrototype.CompetingAmountPenalty,
            0.0001);
        result.Score.ShouldBeLessThan(0.8);
    }

    [Fact]
    public void TrainDecoy_DecoyNivelUsoAmount_PagoParaNoGenerarIntereses_StaysCorroboratingClean()
    {
        // Co-located non-vacuous check (C1.6 pattern): the SAME PDF's untouched Pago row must
        // stay clean — proves the scorer isn't just penalizing "any field on a defective PDF".
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("decoy-nivel-uso-amount.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount/PagoParaNoGenerarIntereses (co-located, untouched): score={result.Score:0.000}");

        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void TrainDecoy_DecoyPagoSinInteresesAmount_PagoParaNoGenerarIntereses_ScoresBelowFloor()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("decoy-pago-sin-intereses-amount.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-pago-sin-intereses-amount/PagoParaNoGenerarIntereses: score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeFalse();
        result.CandidateCount.ShouldBe(2);
        result.Score.ShouldBe(
            HeaderMoneyPlausibilityScorerPrototype.RankNotAdjacentPenalty * HeaderMoneyPlausibilityScorerPrototype.CompetingAmountPenalty,
            0.0001);
        result.Score.ShouldBeLessThan(0.8);
    }

    [Fact]
    public void TrainDecoy_DecoyPagoSinInteresesAmount_SaldoCargosRegulares_StaysCorroboratingClean()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("decoy-pago-sin-intereses-amount.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-pago-sin-intereses-amount/SaldoCargosRegulares (co-located, untouched): score={result.Score:0.000}");

        result.Score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // HOLDOUT set (blind — constants above were fixed before these were scored)
    // -----------------------------------------------------------------------

    [Fact]
    public void HoldoutClean_RealBanamexNivelPago_SaldoCargosRegulares_ScoresAtOrAboveFloor()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("clean-nivel-pago-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago-realbanamex/SaldoCargosRegulares (HOLDOUT): score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.Score.ShouldBeGreaterThanOrEqualTo(0.8);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void HoldoutClean_RealBanamexNivelPago_PagoParaNoGenerarIntereses_ScoresAtOrAboveFloor()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("clean-nivel-pago-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago-realbanamex/PagoParaNoGenerarIntereses (HOLDOUT): score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.Score.ShouldBeGreaterThanOrEqualTo(0.8);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void HoldoutDecoy_RealBanamexDecoyNivel_SaldoCargosRegulares_ScoresBelowFloor_BlindGate()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("decoy-nivel-uso-amount-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount-realbanamex/SaldoCargosRegulares (HOLDOUT, blind): score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeFalse();
        result.CandidateCount.ShouldBe(2);
        result.Score.ShouldBeLessThan(0.8);
    }

    [Fact]
    public void HoldoutDecoy_RealBanamexDecoyPago_PagoParaNoGenerarIntereses_ScoresBelowFloor_BlindGate()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("decoy-pago-sin-intereses-amount-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-pago-sin-intereses-amount-realbanamex/PagoParaNoGenerarIntereses (HOLDOUT, blind): score={result.Score:0.000} rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeFalse();
        result.CandidateCount.ShouldBe(2);
        result.Score.ShouldBeLessThan(0.8);
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the full clean/decoy partition, per field
    // -----------------------------------------------------------------------

    [Fact]
    public void Verdict_TrainHoldoutMargin_MeetsBar_SaldoCargosRegulares()
    {
        var clean = new (string Name, string File)[]
        {
            ("TRAIN   clean-nivel-pago",             "clean-nivel-pago.pdf"),
            ("TRAIN   decoy-pago-sin-intereses-amount (co-located, untouched)", "decoy-pago-sin-intereses-amount.pdf"),
            ("HOLDOUT clean-nivel-pago-realbanamex",  "clean-nivel-pago-realbanamex.pdf"),
        };

        var decoy = new (string Name, string File)[]
        {
            ("TRAIN   decoy-nivel-uso-amount",             "decoy-nivel-uso-amount.pdf"),
            ("HOLDOUT decoy-nivel-uso-amount-realbanamex",  "decoy-nivel-uso-amount-realbanamex.pdf"),
        };

        var cleanScores = clean.Select(c => (c.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath(c.File))).Score)).ToList();
        var decoyScores = decoy.Select(d => (d.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath(d.File))).Score)).ToList();

        _output.WriteLine("=== C2.0b SaldoCargosRegulares verdict table ===");
        _output.WriteLine("-- clean --");
        foreach (var (name, score) in cleanScores)
            _output.WriteLine($"  {name,-60} {score:0.000}");
        _output.WriteLine("-- decoy --");
        foreach (var (name, score) in decoyScores)
            _output.WriteLine($"  {name,-60} {score:0.000}");

        var minClean = cleanScores.Min(c => c.Score);
        var maxDecoy = decoyScores.Max(d => d.Score);
        var margin = minClean - maxDecoy;

        _output.WriteLine($"min(clean) = {minClean:0.000}");
        _output.WriteLine($"max(decoy) = {maxDecoy:0.000}");
        _output.WriteLine($"margin     = {margin:0.000}  (bar: >= 0.15)");

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxDecoy.ShouldBeLessThan(0.8, "decoy picks must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar");
    }

    [Fact]
    public void Verdict_TrainHoldoutMargin_MeetsBar_PagoParaNoGenerarIntereses()
    {
        var clean = new (string Name, string File)[]
        {
            ("TRAIN   clean-nivel-pago",             "clean-nivel-pago.pdf"),
            ("TRAIN   decoy-nivel-uso-amount (co-located, untouched)", "decoy-nivel-uso-amount.pdf"),
            ("HOLDOUT clean-nivel-pago-realbanamex",  "clean-nivel-pago-realbanamex.pdf"),
        };

        var decoy = new (string Name, string File)[]
        {
            ("TRAIN   decoy-pago-sin-intereses-amount",             "decoy-pago-sin-intereses-amount.pdf"),
            ("HOLDOUT decoy-pago-sin-intereses-amount-realbanamex",  "decoy-pago-sin-intereses-amount-realbanamex.pdf"),
        };

        var cleanScores = clean.Select(c => (c.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath(c.File))).Score)).ToList();
        var decoyScores = decoy.Select(d => (d.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath(d.File))).Score)).ToList();

        _output.WriteLine("=== C2.0b PagoParaNoGenerarIntereses verdict table ===");
        _output.WriteLine("-- clean --");
        foreach (var (name, score) in cleanScores)
            _output.WriteLine($"  {name,-60} {score:0.000}");
        _output.WriteLine("-- decoy --");
        foreach (var (name, score) in decoyScores)
            _output.WriteLine($"  {name,-60} {score:0.000}");

        var minClean = cleanScores.Min(c => c.Score);
        var maxDecoy = decoyScores.Max(d => d.Score);
        var margin = minClean - maxDecoy;

        _output.WriteLine($"min(clean) = {minClean:0.000}");
        _output.WriteLine($"max(decoy) = {maxDecoy:0.000}");
        _output.WriteLine($"margin     = {margin:0.000}  (bar: >= 0.15)");

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxDecoy.ShouldBeLessThan(0.8, "decoy picks must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar");
    }

    // -----------------------------------------------------------------------
    // Perturbation stress: +-3pt bounding-box jitter must not change the verdict
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("clean-nivel-pago.pdf", true)]                       // clean (train) -> must STAY >= 0.8
    [InlineData("clean-nivel-pago-realbanamex.pdf", true)]           // clean (holdout layout) -> must STAY >= 0.8
    [InlineData("decoy-nivel-uso-amount.pdf", false)]                // decoy (train) -> must STAY < 0.8
    [InlineData("decoy-nivel-uso-amount-realbanamex.pdf", false)]    // decoy (holdout layout) -> must STAY < 0.8
    public void Perturbation_SaldoCargosRegulares_PlusMinus3ptJitter_DoesNotFlipVerdict(string fileName, bool expectAboveFloor)
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath(fileName));
        AssertSurvivesJitter(fileName, "SaldoCargosRegulares", context, expectAboveFloor);
    }

    [Theory]
    [InlineData("clean-nivel-pago.pdf", true)]
    [InlineData("clean-nivel-pago-realbanamex.pdf", true)]
    [InlineData("decoy-pago-sin-intereses-amount.pdf", false)]
    [InlineData("decoy-pago-sin-intereses-amount-realbanamex.pdf", false)]
    public void Perturbation_PagoParaNoGenerarIntereses_PlusMinus3ptJitter_DoesNotFlipVerdict(string fileName, bool expectAboveFloor)
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath(fileName));
        AssertSurvivesJitter(fileName, "PagoParaNoGenerarIntereses", context, expectAboveFloor);
    }

    private void AssertSurvivesJitter(string fileName, string fieldName, HeaderMoneyBandContext context, bool expectAboveFloor)
    {
        // 50 independent trials, each jittering every token's Left/Right independently by a
        // uniform +-3pt offset (position noise, not a whole-page rigid shift) — same discipline
        // as C1.0b's perturbation test.
        var rng = new Random(20260717);
        var worstCase = expectAboveFloor ? double.MaxValue : double.MinValue;

        for (var trial = 0; trial < 50; trial++)
        {
            var jitteredBand = context.Band
                .Select(t =>
                {
                    var delta = (rng.NextDouble() * 6.0) - 3.0; // uniform [-3, +3]
                    return new SpikeToken(t.Text, t.Left + delta, t.Right + delta);
                })
                .ToList();

            var jitteredContext = new HeaderMoneyBandContext(jitteredBand, context.LabelTokenCount);
            var score = HeaderMoneyPlausibilityScorerPrototype.Score(jitteredContext).Score;
            worstCase = expectAboveFloor ? Math.Min(worstCase, score) : Math.Max(worstCase, score);
        }

        _output.WriteLine($"{fileName}/{fieldName}: worst-case score over 50 trials of +-3pt jitter = {worstCase:0.000} (expectAboveFloor={expectAboveFloor})");

        if (expectAboveFloor)
            worstCase.ShouldBeGreaterThanOrEqualTo(0.8, "a score that only survives exact fixture coordinates is memorized, not measured");
        else
            worstCase.ShouldBeLessThan(0.8, "the decoy verdict must not become a false-clean under small position noise");
    }
}

using System.IO;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

/// <summary>
/// Story <b>C2.1a</b> — the AC1 de-risk. Adversarial review of C2.0b's prototype scorer found a
/// BLOCKING false-abstain: a harmless single-digit superscript footnote marker sitting in the
/// value band (the exact real-Banamex layout <c>PdfPigStatementFieldExtractor.cs:1115</c>
/// documents — <c>"Pago para no generar intereses 2 $32,446.69"</c>) tanked a clean, correctly
/// extracted pick to 0.3575 — indistinguishable from a real decoy. This suite:
/// <list type="number">
/// <item>reproduces the bug empirically against the fixed scorer's fixture set (see the
/// <c>Repro_...</c>-named commit history / return report — the reproduction itself was run
/// against the PRE-fix scorer and is not re-asserted here, since the scorer no longer exhibits
/// the bug);</item>
/// <item>proves the fix — <see cref="HeaderMoneyPlausibilityScorerPrototype"/>'s single-digit
/// footnote-marker transparency (see its class remarks) — restores the footnote specimens to
/// the clean floor;</item>
/// <item>proves the fix is NOT gamed by a harder, money-FORMATTED decoy (unlike C2.0a's bare-digit
/// decoys) that still must score below the floor.</item>
/// </list>
/// </summary>
/// <remarks>
/// Same train/holdout discipline as <see cref="C2_0b_HeaderMoneySeparationSpikeTests"/>: the
/// footnote fix (the scorer's private <c>IsSingleDigitFootnoteMarker</c> — see its class remarks)
/// and the existing constants were NOT re-tuned after inspecting the holdout
/// (<c>-realbanamex</c>) specimens' scores; the holdout facts below are the blind gate, exactly
/// as C2.0b's were.
/// </remarks>
public sealed class C2_1a_FootnoteAndHardDecoySpikeTests
{
    private readonly ITestOutputHelper _output;

    public C2_1a_FootnoteAndHardDecoySpikeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? GetThisFilePath([System.Runtime.CompilerServices.CallerFilePath] string? path = null) => path;

    private static readonly string RepoRoot = ComputeRepoRoot()
        ?? throw new InvalidOperationException("C2.1a spike: could not locate repo root (CLAUDE.md marker not found).");

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
    // TRAIN (dummievec) — footnote clean floor.
    // -----------------------------------------------------------------------

    [Fact]
    public void TrainClean_CleanPagoFootnote_PagoParaNoGenerarIntereses_ScoresAtOrAboveFloor()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("clean-nivel-pago-footnote.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago-footnote/PagoParaNoGenerarIntereses: score={result.Score:0.000} " +
            $"rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        // AC1: with the fix, the footnote "2" is transparent -- the pick is adjacent (rank 1)
        // and the only candidate, exactly like a footnote-free clean pick.
        result.RankAdjacent.ShouldBeTrue();
        result.CandidateCount.ShouldBe(1);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void TrainClean_CleanPagoFootnote_SaldoCargosRegulares_StaysCorroboratingClean()
    {
        // Co-located non-vacuous check (C1.6/C2.0b pattern): the untouched Saldo row on the same
        // PDF must stay clean -- proves the footnote-transparency fix isn't blanket-boosting
        // every field on this PDF, only the one that actually has a footnote in its band.
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("clean-nivel-pago-footnote.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago-footnote/SaldoCargosRegulares (co-located, untouched): score={result.Score:0.000}");

        result.Score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // HOLDOUT (realbanamex) — footnote clean floor, blind gate.
    // -----------------------------------------------------------------------

    [Fact]
    public void HoldoutClean_CleanPagoFootnoteRealbanamex_PagoParaNoGenerarIntereses_ScoresAtOrAboveFloor_BlindGate()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("clean-nivel-pago-footnote-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago-footnote-realbanamex/PagoParaNoGenerarIntereses (HOLDOUT): score={result.Score:0.000} " +
            $"rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeTrue();
        result.CandidateCount.ShouldBe(1);
        result.Score.ShouldBeGreaterThanOrEqualTo(0.8);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void HoldoutClean_CleanPagoFootnoteRealbanamex_SaldoCargosRegulares_StaysCorroboratingClean()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("clean-nivel-pago-footnote-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"clean-nivel-pago-footnote-realbanamex/SaldoCargosRegulares (co-located, untouched, HOLDOUT): score={result.Score:0.000}");

        result.Score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // TRAIN (dummievec) — harder, money-FORMATTED decoy (guards AC1's #2/#3 findings:
    // the fix must not lean on "the pick isn't money-formatted").
    // -----------------------------------------------------------------------

    [Fact]
    public void TrainDecoy_MoneyFormattedDecoyNivel_SaldoCargosRegulares_ScoresBelowFloor()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("decoy-nivel-uso-amount-moneyfmt.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount-moneyfmt/SaldoCargosRegulares: score={result.Score:0.000} " +
            $"rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeFalse();
        result.CandidateCount.ShouldBe(2);
        result.Score.ShouldBe(
            HeaderMoneyPlausibilityScorerPrototype.RankNotAdjacentPenalty * HeaderMoneyPlausibilityScorerPrototype.CompetingAmountPenalty,
            0.0001);
        result.Score.ShouldBeLessThan(0.8);
    }

    [Fact]
    public void TrainDecoy_MoneyFormattedDecoyNivel_PagoParaNoGenerarIntereses_StaysCorroboratingClean()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("decoy-nivel-uso-amount-moneyfmt.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount-moneyfmt/PagoParaNoGenerarIntereses (co-located, untouched): score={result.Score:0.000}");

        result.Score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // HOLDOUT (realbanamex) — harder, money-formatted decoy, blind gate.
    // -----------------------------------------------------------------------

    [Fact]
    public void HoldoutDecoy_MoneyFormattedDecoyNivelRealbanamex_SaldoCargosRegulares_ScoresBelowFloor_BlindGate()
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath("decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount-moneyfmt-realbanamex/SaldoCargosRegulares (HOLDOUT, blind): score={result.Score:0.000} " +
            $"rankAdjacent={result.RankAdjacent} count={result.CandidateCount}");

        result.RankAdjacent.ShouldBeFalse();
        result.CandidateCount.ShouldBe(2);
        result.Score.ShouldBeLessThan(0.8);
    }

    [Fact]
    public void HoldoutDecoy_MoneyFormattedDecoyNivelRealbanamex_PagoParaNoGenerarIntereses_StaysCorroboratingClean()
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath("decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf"));
        var result = HeaderMoneyPlausibilityScorerPrototype.Score(context);

        _output.WriteLine($"decoy-nivel-uso-amount-moneyfmt-realbanamex/PagoParaNoGenerarIntereses (co-located, untouched, HOLDOUT): score={result.Score:0.000}");

        result.Score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the EXPANDED clean/decoy partition (C2.0a's original 3
    // clean + 2 decoy PLUS C2.1a's footnote clean + money-formatted decoy), per field.
    // -----------------------------------------------------------------------

    [Fact]
    public void Verdict_TrainHoldoutMargin_MeetsBar_WithFootnoteAndHardDecoy_SaldoCargosRegulares()
    {
        var clean = new (string Name, string File)[]
        {
            ("TRAIN   clean-nivel-pago",                                          "clean-nivel-pago.pdf"),
            ("TRAIN   decoy-pago-sin-intereses-amount (co-located, untouched)",   "decoy-pago-sin-intereses-amount.pdf"),
            ("TRAIN   clean-nivel-pago-footnote (co-located, untouched)",          "clean-nivel-pago-footnote.pdf"),
            ("HOLDOUT clean-nivel-pago-realbanamex",                               "clean-nivel-pago-realbanamex.pdf"),
            ("HOLDOUT clean-nivel-pago-footnote-realbanamex (co-located)",         "clean-nivel-pago-footnote-realbanamex.pdf"),
        };

        var decoy = new (string Name, string File)[]
        {
            ("TRAIN   decoy-nivel-uso-amount",                        "decoy-nivel-uso-amount.pdf"),
            ("TRAIN   decoy-nivel-uso-amount-moneyfmt",                "decoy-nivel-uso-amount-moneyfmt.pdf"),
            ("HOLDOUT decoy-nivel-uso-amount-realbanamex",             "decoy-nivel-uso-amount-realbanamex.pdf"),
            ("HOLDOUT decoy-nivel-uso-amount-moneyfmt-realbanamex",    "decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf"),
        };

        var cleanScores = clean.Select(c => (c.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath(c.File))).Score)).ToList();
        var decoyScores = decoy.Select(d => (d.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath(d.File))).Score)).ToList();

        _output.WriteLine("=== C2.1a SaldoCargosRegulares verdict table (footnote + hard decoy) ===");
        _output.WriteLine("-- clean --");
        foreach (var (name, score) in cleanScores)
            _output.WriteLine($"  {name,-70} {score:0.000}");
        _output.WriteLine("-- decoy --");
        foreach (var (name, score) in decoyScores)
            _output.WriteLine($"  {name,-70} {score:0.000}");

        var minClean = cleanScores.Min(c => c.Score);
        var maxDecoy = decoyScores.Max(d => d.Score);
        var margin = minClean - maxDecoy;

        _output.WriteLine($"min(clean) = {minClean:0.000}");
        _output.WriteLine($"max(decoy) = {maxDecoy:0.000}");
        _output.WriteLine($"margin     = {margin:0.000}  (bar: >= 0.15)");

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks (incl. the footnote specimens) must keep producing verdicts");
        maxDecoy.ShouldBeLessThan(0.8, "decoy picks (incl. the money-formatted one) must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar");
    }

    [Fact]
    public void Verdict_TrainHoldoutMargin_MeetsBar_WithFootnoteAndHardDecoy_PagoParaNoGenerarIntereses()
    {
        var clean = new (string Name, string File)[]
        {
            ("TRAIN   clean-nivel-pago",                                       "clean-nivel-pago.pdf"),
            ("TRAIN   decoy-nivel-uso-amount (co-located, untouched)",         "decoy-nivel-uso-amount.pdf"),
            ("TRAIN   decoy-nivel-uso-amount-moneyfmt (co-located, untouched)", "decoy-nivel-uso-amount-moneyfmt.pdf"),
            ("TRAIN   clean-nivel-pago-footnote",                              "clean-nivel-pago-footnote.pdf"),
            ("HOLDOUT clean-nivel-pago-realbanamex",                           "clean-nivel-pago-realbanamex.pdf"),
            ("HOLDOUT clean-nivel-pago-footnote-realbanamex",                  "clean-nivel-pago-footnote-realbanamex.pdf"),
        };

        var decoy = new (string Name, string File)[]
        {
            ("TRAIN   decoy-pago-sin-intereses-amount",             "decoy-pago-sin-intereses-amount.pdf"),
            ("HOLDOUT decoy-pago-sin-intereses-amount-realbanamex", "decoy-pago-sin-intereses-amount-realbanamex.pdf"),
        };

        var cleanScores = clean.Select(c => (c.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath(c.File))).Score)).ToList();
        var decoyScores = decoy.Select(d => (d.Name, Score: HeaderMoneyPlausibilityScorerPrototype.Score(
            HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath(d.File))).Score)).ToList();

        _output.WriteLine("=== C2.1a PagoParaNoGenerarIntereses verdict table (footnote + hard decoy) ===");
        _output.WriteLine("-- clean --");
        foreach (var (name, score) in cleanScores)
            _output.WriteLine($"  {name,-70} {score:0.000}");
        _output.WriteLine("-- decoy --");
        foreach (var (name, score) in decoyScores)
            _output.WriteLine($"  {name,-70} {score:0.000}");

        var minClean = cleanScores.Min(c => c.Score);
        var maxDecoy = decoyScores.Max(d => d.Score);
        var margin = minClean - maxDecoy;

        _output.WriteLine($"min(clean) = {minClean:0.000}");
        _output.WriteLine($"max(decoy) = {maxDecoy:0.000}");
        _output.WriteLine($"margin     = {margin:0.000}  (bar: >= 0.15)");

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks (incl. the footnote specimens) must keep producing verdicts");
        maxDecoy.ShouldBeLessThan(0.8, "decoy picks must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar");
    }

    // -----------------------------------------------------------------------
    // Perturbation stress: +-3pt bounding-box jitter must not flip the verdict, for both the
    // footnote clean specimens and the harder money-formatted decoy.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("clean-nivel-pago-footnote.pdf", true)]                       // clean footnote (train) -> must STAY >= 0.8
    [InlineData("clean-nivel-pago-footnote-realbanamex.pdf", true)]           // clean footnote (holdout) -> must STAY >= 0.8
    [InlineData("decoy-nivel-uso-amount-moneyfmt.pdf", false)]                // hard decoy (train) -> must STAY < 0.8
    [InlineData("decoy-nivel-uso-amount-moneyfmt-realbanamex.pdf", false)]    // hard decoy (holdout) -> must STAY < 0.8
    public void Perturbation_SaldoCargosRegulares_PlusMinus3ptJitter_DoesNotFlipVerdict(string fileName, bool expectAboveFloor)
    {
        var context = HeaderMoneyBandLocator.LocateSaldoCargosRegulares(FixturePath(fileName));
        AssertSurvivesJitter(fileName, "SaldoCargosRegulares", context, expectAboveFloor);
    }

    [Theory]
    [InlineData("clean-nivel-pago-footnote.pdf", true)]
    [InlineData("clean-nivel-pago-footnote-realbanamex.pdf", true)]
    public void Perturbation_PagoParaNoGenerarIntereses_FootnoteClean_PlusMinus3ptJitter_DoesNotFlipVerdict(string fileName, bool expectAboveFloor)
    {
        var context = HeaderMoneyBandLocator.LocatePagoParaNoGenerarIntereses(FixturePath(fileName));
        AssertSurvivesJitter(fileName, "PagoParaNoGenerarIntereses", context, expectAboveFloor);
    }

    private void AssertSurvivesJitter(string fileName, string fieldName, HeaderMoneyBandContext context, bool expectAboveFloor)
    {
        // Same discipline as C2.0b: 50 independent trials, each jittering every token's
        // Left/Right independently by a uniform +-3pt offset.
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

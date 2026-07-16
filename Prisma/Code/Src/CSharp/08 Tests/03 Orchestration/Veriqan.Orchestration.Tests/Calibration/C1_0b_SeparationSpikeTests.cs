using System.IO;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

/// <summary>
/// Story <b>C1.0b</b> — the MAKE-OR-BREAK separation spike. Proves (or refutes) that a
/// geometric-plausibility score exists on the synthetic corpus such that:
/// <c>min(clean) &gt;= 0.8</c>, <c>max(ambiguous) &lt; 0.8</c>, <c>margin &gt;= 0.15</c>, on a
/// TRAIN/HOLDOUT split, surviving a &#177;3pt bounding-box perturbation.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a spike, not production code.</b> <see cref="GeometricPlausibilityScorerPrototype"/>
/// exists only to prove separation and hand C1.2 the settled mechanism/constants — it is not
/// wired into <c>PdfPigStatementFieldExtractor</c> or <c>ExtractedField</c>.
/// </para>
/// <para>
/// <b>Train/holdout discipline (design decision 3):</b> scoring constants
/// (<see cref="GeometricPlausibilityScorerPrototype.SiblingAbsentPenalty"/>,
/// <see cref="GeometricPlausibilityScorerPrototype.CompetitionExcessPenalty"/>) were frozen using
/// ONLY the TRAIN fixtures (<c>s6211-baseline</c>, <c>missing-order-marker</c>,
/// <c>decoy-percent</c>). The HOLDOUT fixtures below
/// (<c>s622-realbanamex-baseline</c>, <c>s6211-var-a/b/c</c>, <c>s-c1-swap-displaced</c>) were
/// never inspected while choosing those constants — <see cref="Verdict_TrainHoldoutMargin_MeetsBar"/>
/// is the blind gate.
/// </para>
/// <para>
/// <b>Negative control:</b> <c>s-c1-swap</c> (the C1.0a original, central-marker transposition)
/// is proven and documented as GEOMETRICALLY INVISIBLE — it is excluded from the margin
/// calculation on purpose (see <see cref="NegativeControl_CentralMarkerSwap_IsGeometricallyInvisible"/>).
/// A scorer that "passed" by silently also catching this specimen would be a red flag, not a bonus
/// — it would mean the signal secretly keys on something other than the documented mechanism.
/// </para>
/// </remarks>
public sealed class C1_0b_SeparationSpikeTests
{
    private readonly ITestOutputHelper _output;

    public C1_0b_SeparationSpikeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // -----------------------------------------------------------------------
    // Fixture path resolution — walk up from this source file's compile-time
    // location (works regardless of where build artifacts land) looking for
    // the repo root marker CLAUDE.md, same pattern as SyntheticDefectVerdictE2ETests.
    // -----------------------------------------------------------------------

    private static string? GetThisFilePath([System.Runtime.CompilerServices.CallerFilePath] string? path = null) => path;

    private static readonly string RepoRoot = ComputeRepoRoot()
        ?? throw new InvalidOperationException("C1.0b spike: could not locate repo root (CLAUDE.md marker not found).");

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
    // TRAIN set (constants frozen against these three only)
    // -----------------------------------------------------------------------

    [Fact]
    public void TrainClean_S6211Baseline_ScoresAtCeiling()
    {
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath("s6211-baseline.pdf"));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"s6211-baseline: score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");

        result.SiblingAdjacentToCatPick.ShouldBeTrue();
        result.CompetingPercentTokenCount.ShouldBe(2);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void TrainAmbiguous_MissingOrderMarker_ScoresBelowFloor_ViaSiblingSignal()
    {
        // Extraction is UNCHANGED/correct here (see synth_gen.py comment) — this proves signal #1
        // (sibling adjacency) is independent of extraction correctness: a legitimately-correct
        // read still scores low because the disambiguating marker is simply absent.
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath("missing-order-marker.pdf"));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"missing-order-marker: score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");

        result.SiblingAdjacentToCatPick.ShouldBeFalse();
        result.CompetingPercentTokenCount.ShouldBe(2);
        result.Score.ShouldBe(GeometricPlausibilityScorerPrototype.SiblingAbsentPenalty);
        result.Score.ShouldBeLessThan(0.8);
    }

    [Fact]
    public void TrainAmbiguous_DecoyPercent_ScoresBelowFloor_ViaCompetitionSignal()
    {
        // Sibling marker IS correctly adjacent here — this is the counter-example that RULES OUT
        // "sibling-presence as a gate": if sibling presence waived the competition penalty, this
        // specimen would score 1.0, contradicting its own god's-eye manifest
        // (confidenceExpectations: {Cat: low, Tasa: low}, written in C1.0a).
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath("decoy-percent.pdf"));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"decoy-percent: score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");

        result.SiblingAdjacentToCatPick.ShouldBeTrue();
        result.CompetingPercentTokenCount.ShouldBe(3);
        result.Score.ShouldBe(GeometricPlausibilityScorerPrototype.CompetitionExcessPenalty);
        result.Score.ShouldBeLessThan(0.8);
    }

    // -----------------------------------------------------------------------
    // Negative control (excluded from the margin calculation by design)
    // -----------------------------------------------------------------------

    [Fact]
    public void NegativeControl_CentralMarkerSwap_IsGeometricallyInvisible()
    {
        // s-c1-swap ("27.36% sin IVA 28.86%") transposes CAT/TASA but 'sin IVA' stays centered
        // between the two tokens exactly like the clean baseline — the C1.0a CRITICAL FINDING.
        // This test PASSES by asserting the score stays at the clean ceiling (1.0), i.e. this
        // scorer CANNOT catch this specimen. That is the expected, documented outcome, not a bug —
        // it is why 's-c1-swap-displaced' (the realistic swap) exists as the separable ambiguous
        // target instead.
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath("s-c1-swap.pdf"));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"s-c1-swap (negative control): score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");
        _output.WriteLine("  ^ CONFIRMED geometrically invisible: 'sin IVA' sits between the two transposed");
        _output.WriteLine("    tokens exactly as it would for a clean read. No geometric signal (this one or any");
        _output.WriteLine("    other operating purely on token position) can separate this from a legitimate pick.");

        result.SiblingAdjacentToCatPick.ShouldBeTrue();
        result.Score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // HOLDOUT set (blind — constants above were fixed before these were scored)
    // -----------------------------------------------------------------------

    [Fact]
    public void HoldoutClean_RealBanamexBaseline_ScoresAtOrAboveFloor()
    {
        // Unseen LAYOUT (612x792 left-column real-Banamex clone, "26.10% sin IVA 19.75%") — never
        // used while choosing SiblingAbsentPenalty/CompetitionExcessPenalty.
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath("s622-realbanamex-baseline.pdf"));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"s622-realbanamex-baseline (HOLDOUT): score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");

        result.Score.ShouldBeGreaterThanOrEqualTo(0.8);
        result.Score.ShouldBe(1.0);
    }

    [Theory]
    [InlineData("s6211-var-a.pdf")]
    [InlineData("s6211-var-b.pdf")]
    [InlineData("s6211-var-c.pdf")]
    public void HoldoutClean_VariancePersonas_ScoreAtOrAboveFloor(string fileName)
    {
        // Unseen seeded value personas + a rigid whole-page Y-shift (+-1..3pt) — never used while
        // choosing the constants. Proves the signals are robust to realistic digit-value/typeset
        // variance, not just the one exact baseline fixture the constants were eyeballed against.
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath(fileName));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"{fileName} (HOLDOUT): score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");

        result.Score.ShouldBeGreaterThanOrEqualTo(0.8);
        result.Score.ShouldBe(1.0);
    }

    [Fact]
    public void HoldoutAmbiguous_MarkerDisplacedSwap_ScoresBelowFloor_BlindGate()
    {
        // THE make-or-break case: a REALISTIC swap (TASA prints first, CAT second, 'sin IVA'
        // tracks the true CAT) built and registered AFTER the scoring constants were frozen on
        // the train set. If this fails, the whole lever is unproven — scoring against a fixture
        // you tuned constants on is curve-fitting, not proof.
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath("s-c1-swap-displaced.pdf"));
        var result = GeometricPlausibilityScorerPrototype.Score(band);

        _output.WriteLine($"s-c1-swap-displaced (HOLDOUT, blind ambiguous): score={result.Score:0.000} sibling={result.SiblingAdjacentToCatPick} count={result.CompetingPercentTokenCount}");

        result.SiblingAdjacentToCatPick.ShouldBeFalse();
        result.Score.ShouldBeLessThan(0.8);
    }

    // -----------------------------------------------------------------------
    // The verdict: margin across the full clean/ambiguous partition
    // -----------------------------------------------------------------------

    [Fact]
    public void Verdict_TrainHoldoutMargin_MeetsBar()
    {
        var clean = new (string Name, string File)[]
        {
            ("TRAIN   s6211-baseline",             "s6211-baseline.pdf"),
            ("HOLDOUT s622-realbanamex-baseline",   "s622-realbanamex-baseline.pdf"),
            ("HOLDOUT s6211-var-a",                 "s6211-var-a.pdf"),
            ("HOLDOUT s6211-var-b",                 "s6211-var-b.pdf"),
            ("HOLDOUT s6211-var-c",                 "s6211-var-c.pdf"),
        };

        var ambiguous = new (string Name, string File)[]
        {
            ("TRAIN   missing-order-marker",  "missing-order-marker.pdf"),
            ("TRAIN   decoy-percent",         "decoy-percent.pdf"),
            ("HOLDOUT s-c1-swap-displaced",   "s-c1-swap-displaced.pdf"),
        };

        var negativeControl = ("s-c1-swap (EXCLUDED — geometrically invisible, see NegativeControl test)", "s-c1-swap.pdf");

        var cleanScores = clean.Select(c => (c.Name, Score: GeometricPlausibilityScorerPrototype.Score(
            SpikeBandLocator.LocateTasaCatValueBand(FixturePath(c.File))).Score)).ToList();
        var ambiguousScores = ambiguous.Select(a => (a.Name, Score: GeometricPlausibilityScorerPrototype.Score(
            SpikeBandLocator.LocateTasaCatValueBand(FixturePath(a.File))).Score)).ToList();
        var negativeControlScore = GeometricPlausibilityScorerPrototype.Score(
            SpikeBandLocator.LocateTasaCatValueBand(FixturePath(negativeControl.Item2))).Score;

        _output.WriteLine("=== C1.0b separation-spike verdict table ===");
        _output.WriteLine("-- clean --");
        foreach (var (name, score) in cleanScores)
            _output.WriteLine($"  {name,-40} {score:0.000}");
        _output.WriteLine("-- ambiguous --");
        foreach (var (name, score) in ambiguousScores)
            _output.WriteLine($"  {name,-40} {score:0.000}");
        _output.WriteLine("-- negative control (excluded) --");
        _output.WriteLine($"  {negativeControl.Item1,-40} {negativeControlScore:0.000}");

        var minClean = cleanScores.Min(c => c.Score);
        var maxAmbiguous = ambiguousScores.Max(a => a.Score);
        var margin = minClean - maxAmbiguous;

        _output.WriteLine($"min(clean)     = {minClean:0.000}");
        _output.WriteLine($"max(ambiguous) = {maxAmbiguous:0.000}");
        _output.WriteLine($"margin         = {margin:0.000}  (bar: >= 0.15)");

        minClean.ShouldBeGreaterThanOrEqualTo(0.8, "clean picks must keep producing verdicts");
        maxAmbiguous.ShouldBeLessThan(0.8, "ambiguous picks must abstain");
        margin.ShouldBeGreaterThanOrEqualTo(0.15, "margin must clear the anti-gaming bar");
    }

    // -----------------------------------------------------------------------
    // Perturbation stress: +-3pt bounding-box jitter must not change the verdict
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("s6211-baseline.pdf", true)]              // clean -> must STAY >= 0.8
    [InlineData("s622-realbanamex-baseline.pdf", true)]   // clean (holdout layout) -> must STAY >= 0.8
    [InlineData("s-c1-swap-displaced.pdf", false)]        // ambiguous -> must STAY < 0.8 (separation itself is not memorized-fragile either)
    public void Perturbation_PlusMinus3ptJitter_DoesNotFlipVerdict(string fileName, bool expectAboveFloor)
    {
        var band = SpikeBandLocator.LocateTasaCatValueBand(FixturePath(fileName));

        // 50 independent trials, each jittering every token's Left/Right independently by a
        // uniform +-3pt offset (position noise, not a whole-page rigid shift — a harder stress
        // than the var-a/b/c Y-shift fixtures, which move every token by the SAME amount and so
        // preserve all relative gaps exactly).
        var rng = new Random(20260716);
        var worstCase = expectAboveFloor ? double.MaxValue : double.MinValue;

        for (var trial = 0; trial < 50; trial++)
        {
            var jittered = band
                .Select(t =>
                {
                    var delta = (rng.NextDouble() * 6.0) - 3.0; // uniform [-3, +3]
                    return new SpikeToken(t.Text, t.Left + delta, t.Right + delta);
                })
                .ToList();

            var score = GeometricPlausibilityScorerPrototype.Score(jittered).Score;
            worstCase = expectAboveFloor ? Math.Min(worstCase, score) : Math.Max(worstCase, score);
        }

        _output.WriteLine($"{fileName}: worst-case score over 50 trials of +-3pt jitter = {worstCase:0.000} (expectAboveFloor={expectAboveFloor})");

        if (expectAboveFloor)
            worstCase.ShouldBeGreaterThanOrEqualTo(0.8, "a score that only survives exact fixture coordinates is memorized, not measured");
        else
            worstCase.ShouldBeLessThan(0.8, "the ambiguous verdict must not become a false-clean under small position noise");
    }
}

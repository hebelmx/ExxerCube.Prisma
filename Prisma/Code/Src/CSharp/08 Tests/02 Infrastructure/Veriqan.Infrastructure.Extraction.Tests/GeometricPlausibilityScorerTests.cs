using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Confidence;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for the C1.2 production <see cref="GeometricPlausibilityScorer"/> — pure,
/// hand-built <see cref="GeometricSignals"/>, no PdfPig/band logic. Mirrors the constants and
/// pass/fail shapes the C1.0b spike proved (<c>GeometricPlausibilityScorerPrototype</c>); this
/// suite exercises the production type directly, not the spike.
/// </summary>
public sealed class GeometricPlausibilityScorerTests
{
    private static readonly FieldCalibration Calibration = FieldCalibrationTable.TasaCat;

    [Fact]
    public void Score_CleanPick_BothSignalsPass_ReturnsCeiling()
    {
        var signals = new GeometricSignals(SiblingAdjacentToPick: true, CompetingTokenCount: 2);

        var score = GeometricPlausibilityScorer.Score(signals, Calibration);

        score.ShouldBe(1.0);
    }

    [Fact]
    public void Score_SiblingAbsent_AppliesSiblingPenaltyOnly()
    {
        var signals = new GeometricSignals(SiblingAdjacentToPick: false, CompetingTokenCount: 2);

        var score = GeometricPlausibilityScorer.Score(signals, Calibration);

        score.ShouldBe(0.55);
        score.ShouldBeLessThan(0.8, "an absent sibling marker must abstain-gate below the 0.8 guard floor");
    }

    [Fact]
    public void Score_CompetitionExceedsTwo_AppliesCompetitionPenaltyOnly()
    {
        var signals = new GeometricSignals(SiblingAdjacentToPick: true, CompetingTokenCount: 3);

        var score = GeometricPlausibilityScorer.Score(signals, Calibration);

        score.ShouldBe(0.65);
        score.ShouldBeLessThan(0.8, "a decoy-competed pick must abstain-gate below the 0.8 guard floor");
    }

    [Fact]
    public void Score_BothSignalsFail_MultipliesPenaltiesIndependently()
    {
        var signals = new GeometricSignals(SiblingAdjacentToPick: false, CompetingTokenCount: 3);

        var score = GeometricPlausibilityScorer.Score(signals, Calibration);

        // 0.55 * 0.65 = 0.3575 — MUST be the product, not e.g. an average ((0.55+0.65)/2 = 0.60,
        // which would still clear the 0.8 floor's complement incorrectly and, more importantly,
        // would let a clean signal dilute a damning one — the exact B2 false-confidence shape
        // the multiplicative formula exists to rule out.
        score.ShouldBe(0.3575, 0.0001);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Score_CompetitionCountAboveThree_StillAppliesSinglePenalty(int competingTokenCount)
    {
        // The penalty is a fixed multiplicative factor, not scaled by how many extra tokens
        // competed — 3, 4, or 5 competing tokens all apply the same CompetitionExcessPenalty once.
        var signals = new GeometricSignals(SiblingAdjacentToPick: true, CompetingTokenCount: competingTokenCount);

        var score = GeometricPlausibilityScorer.Score(signals, Calibration);

        score.ShouldBe(0.65);
    }

    [Fact]
    public void Score_CompetitionCountExactlyTwo_DoesNotApplyCompetitionPenalty()
    {
        // The boundary: >2 triggers the penalty, ==2 (the expected clean count) does not.
        var signals = new GeometricSignals(SiblingAdjacentToPick: true, CompetingTokenCount: 2);

        var score = GeometricPlausibilityScorer.Score(signals, Calibration);

        score.ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // IsSiblingAdjacentToPick — rank-based sibling-adjacency signal derivation
    // -----------------------------------------------------------------------

    [Fact]
    public void IsSiblingAdjacentToPick_SinIvaImmediatelyAfterPick_BeforeSibling_ReturnsTrue()
    {
        // "28.86% sin IVA 27.36%" — CAT pick at Left=0, sibling marker immediately after,
        // TASA pick (siblingPick) further right.
        var catPick = new GeometricToken("28.86%", Left: 0, Right: 10);
        var tasaPick = new GeometricToken("27.36%", Left: 40, Right: 50);
        var bandTokens = new[]
        {
            catPick,
            new GeometricToken("sin", Left: 15, Right: 20),
            new GeometricToken("IVA", Left: 22, Right: 28),
            tasaPick,
        };

        var result = GeometricPlausibilityScorer.IsSiblingAdjacentToPick(bandTokens, catPick, tasaPick);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSiblingAdjacentToPick_MarkerAbsent_ReturnsFalse()
    {
        var catPick = new GeometricToken("28.86%", Left: 0, Right: 10);
        var tasaPick = new GeometricToken("27.36%", Left: 40, Right: 50);
        var bandTokens = new[] { catPick, tasaPick };

        var result = GeometricPlausibilityScorer.IsSiblingAdjacentToPick(bandTokens, catPick, tasaPick);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSiblingAdjacentToPick_MarkerDisplacedAfterSiblingPick_ReturnsFalse()
    {
        // The realistic swap shape (s-c1-swap-displaced): "sin IVA" tracks the TRUE CAT, which
        // sits after the sibling (TASA) pick in this transposed layout — so relative to the
        // (wrongly) CAT-picked token, the marker is not the immediate rank-neighbour.
        var wrongCatPick = new GeometricToken("27.36%", Left: 0, Right: 10);
        var tasaPick = new GeometricToken("28.86%", Left: 15, Right: 25);
        var bandTokens = new[]
        {
            wrongCatPick,
            tasaPick,
            new GeometricToken("sin", Left: 30, Right: 35),
            new GeometricToken("IVA", Left: 37, Right: 43),
        };

        var result = GeometricPlausibilityScorer.IsSiblingAdjacentToPick(bandTokens, wrongCatPick, tasaPick);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsSiblingAdjacentToPick_NoSiblingPick_MarkerUnboundedToTheRight_ReturnsTrue()
    {
        // When there is no second (TASA) token at all, an adjacent "sin IVA" still counts —
        // siblingPick is null so the "must precede sibling" check is unbounded.
        var catPick = new GeometricToken("28.86%", Left: 0, Right: 10);
        var bandTokens = new[]
        {
            catPick,
            new GeometricToken("sin", Left: 15, Right: 20),
            new GeometricToken("IVA", Left: 22, Right: 28),
        };

        var result = GeometricPlausibilityScorer.IsSiblingAdjacentToPick(bandTokens, catPick, siblingPick: null);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSiblingAdjacentToPick_JitteredCoordinates_RankSurvivesSmallPositionNoise()
    {
        // Rank-based (ordinal by Left), not edge-distance-based — a +-3pt-scale jitter that
        // preserves relative ordering must not flip the verdict (the C1.0b perturbation-stress
        // finding that ruled out a raw edge-gap comparison).
        var catPick = new GeometricToken("28.86%", Left: 1.4, Right: 11.2);
        var tasaPick = new GeometricToken("27.36%", Left: 42.7, Right: 52.1);
        var bandTokens = new[]
        {
            catPick,
            new GeometricToken("sin", Left: 17.6, Right: 21.9),
            new GeometricToken("IVA", Left: 24.3, Right: 30.5),
            tasaPick,
        };

        var result = GeometricPlausibilityScorer.IsSiblingAdjacentToPick(bandTokens, catPick, tasaPick);

        result.ShouldBeTrue();
    }
}

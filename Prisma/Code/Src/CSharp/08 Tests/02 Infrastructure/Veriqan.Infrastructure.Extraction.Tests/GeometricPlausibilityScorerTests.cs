using ExxerCube.Prisma.Veriqan.Domain.Extraction;
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

    // -----------------------------------------------------------------------
    // C1.4 — RESUMEN/NIVEL money-field slice: ScoreResumen + IsValueRankAdjacentToLabel
    // -----------------------------------------------------------------------

    private static readonly ResumenFieldCalibration ResumenCalibration = FieldCalibrationTable.Resumen[FieldKind.MontoIntereses];

    [Fact]
    public void ScoreResumen_CleanPick_BothSignalsPass_ReturnsCeiling()
    {
        var signals = new ResumenGeometricSignals(LabelRankAdjacent: true, DualPassDisagreement: false);

        var score = GeometricPlausibilityScorer.ScoreResumen(signals, ResumenCalibration);

        score.ShouldBe(1.0);
    }

    [Fact]
    public void ScoreResumen_LabelNotRankAdjacent_AppliesPenaltyOnly()
    {
        var signals = new ResumenGeometricSignals(LabelRankAdjacent: false, DualPassDisagreement: false);

        var score = GeometricPlausibilityScorer.ScoreResumen(signals, ResumenCalibration);

        score.ShouldBe(0.55);
        score.ShouldBeLessThan(0.8, "a label-displaced pick must abstain-gate below the 0.8 guard floor");
    }

    [Fact]
    public void ScoreResumen_DualPassDisagreement_AppliesPenaltyOnly()
    {
        var signals = new ResumenGeometricSignals(LabelRankAdjacent: true, DualPassDisagreement: true);

        var score = GeometricPlausibilityScorer.ScoreResumen(signals, ResumenCalibration);

        score.ShouldBe(0.55);
        score.ShouldBeLessThan(0.8, "disagreeing left/right column passes must abstain-gate below the 0.8 guard floor");
    }

    [Fact]
    public void ScoreResumen_BothSignalsFail_MultipliesPenaltiesIndependently()
    {
        var signals = new ResumenGeometricSignals(LabelRankAdjacent: false, DualPassDisagreement: true);

        var score = GeometricPlausibilityScorer.ScoreResumen(signals, ResumenCalibration);

        // 0.55 * 0.55 = 0.3025 — the product, not an average (same B2 false-confidence shape
        // the multiplicative formula rules out, per the Tasa/Cat Score tests above).
        score.ShouldBe(0.3025, 0.0001);
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_PickImmediatelyAfterLabel_ReturnsTrue()
    {
        var labelEnd = new GeometricToken("anterior", Left: 20, Right: 45);
        var pick = new GeometricToken("$1,234.56", Left: 50, Right: 80);
        var bandTokens = new[] { labelEnd, pick };

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, pick, maxAllowedRankGap: 2);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_SignTokenBetweenLabelAndPick_StillWithinGap_ReturnsTrue()
    {
        // "Adeudo del periodo anterior  +  $ 1,234.56" — a sign token then "$" then the amount
        // sit between the label end and the parsed-amount pick token; rank gap = 2 (sign, then
        // "$"), within MaxLabelToPickRankGap = 2 when the pick IS the "$" (the split-dollar
        // amount starts there).
        var labelEnd = new GeometricToken("anterior", Left: 20, Right: 45);
        var sign = new GeometricToken("+", Left: 47, Right: 50);
        var dollar = new GeometricToken("$", Left: 52, Right: 56);
        var number = new GeometricToken("1,234.56", Left: 58, Right: 80);
        var bandTokens = new[] { labelEnd, sign, dollar, number };

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, dollar, maxAllowedRankGap: 2);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_UnrelatedTokensIntervene_ExceedsGap_ReturnsFalse()
    {
        // A decoy: an adjacent row's description text leaked into the band between this row's
        // label and the picked amount (the wrong-row/wrong-column geometry C1.4's calibration
        // corpus injects) — several unrelated tokens push the rank gap past the threshold.
        var labelEnd = new GeometricToken("anterior", Left: 20, Right: 45);
        var decoyWord1 = new GeometricToken("Cargos", Left: 47, Right: 60);
        var decoyWord2 = new GeometricToken("regulares", Left: 62, Right: 85);
        var pick = new GeometricToken("$999.00", Left: 90, Right: 110);
        var bandTokens = new[] { labelEnd, decoyWord1, decoyWord2, pick };

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, pick, maxAllowedRankGap: 2);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_PickAtExactlyMaxAllowedGap_ReturnsTrue()
    {
        var labelEnd = new GeometricToken("anterior", Left: 20, Right: 45);
        var a = new GeometricToken("+", Left: 47, Right: 50);
        var b = new GeometricToken("$", Left: 52, Right: 56);
        var bandTokens = new[] { labelEnd, a, b };

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, b, maxAllowedRankGap: 2);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_PickOneRankBeyondMaxAllowedGap_ReturnsFalse()
    {
        var labelEnd = new GeometricToken("anterior", Left: 20, Right: 45);
        var a = new GeometricToken("+", Left: 47, Right: 50);
        var b = new GeometricToken("8", Left: 52, Right: 55); // footnote digit
        var pick = new GeometricToken("$", Left: 57, Right: 60);
        var bandTokens = new[] { labelEnd, a, b, pick };

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, pick, maxAllowedRankGap: 2);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_PickNotFoundInBand_ReturnsFalse()
    {
        var labelEnd = new GeometricToken("anterior", Left: 20, Right: 45);
        var pick = new GeometricToken("$1,234.56", Left: 50, Right: 80);
        var bandTokens = new[] { labelEnd }; // pick absent from the band

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, pick, maxAllowedRankGap: 2);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValueRankAdjacentToLabel_JitteredCoordinates_RankSurvivesSmallPositionNoise()
    {
        var labelEnd = new GeometricToken("anterior", Left: 21.3, Right: 44.8);
        var pick = new GeometricToken("$1,234.56", Left: 49.6, Right: 81.2);
        var bandTokens = new[] { labelEnd, pick };

        var result = GeometricPlausibilityScorer.IsValueRankAdjacentToLabel(
            bandTokens, labelEnd, pick, maxAllowedRankGap: 2);

        result.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // C1.6 — DESGLOSE total-row slice: ScoreTotalRow
    // -----------------------------------------------------------------------
    // Rank-adjacency itself (IsValueRankAdjacentToLabel) is already exhaustively unit-tested
    // above (C1.4, reused verbatim by TryParseTotalRow) — these cases exercise ScoreTotalRow's
    // own multiplicative combination and its NEW signal, competition, not the rank helper again.

    private static readonly TotalRowFieldCalibration TotalRowCalibration = FieldCalibrationTable.TotalRow[FieldKind.TotalCargos];

    [Fact]
    public void ScoreTotalRow_CleanPick_BothSignalsPass_ReturnsCeiling()
    {
        var signals = new TotalRowGeometricSignals(LabelRankAdjacent: true, HasCompetingAmount: false);

        var score = GeometricPlausibilityScorer.ScoreTotalRow(signals, TotalRowCalibration);

        score.ShouldBe(1.0);
    }

    [Fact]
    public void ScoreTotalRow_LabelNotRankAdjacent_AppliesPenaltyOnly()
    {
        var signals = new TotalRowGeometricSignals(LabelRankAdjacent: false, HasCompetingAmount: false);

        var score = GeometricPlausibilityScorer.ScoreTotalRow(signals, TotalRowCalibration);

        score.ShouldBe(0.55);
        score.ShouldBeLessThan(0.8, "a label-displaced pick must abstain-gate below the 0.8 guard floor");
    }

    [Fact]
    public void ScoreTotalRow_CompetingAmount_AppliesPenaltyOnly()
    {
        var signals = new TotalRowGeometricSignals(LabelRankAdjacent: true, HasCompetingAmount: true);

        var score = GeometricPlausibilityScorer.ScoreTotalRow(signals, TotalRowCalibration);

        score.ShouldBe(0.55);
        score.ShouldBeLessThan(0.8, "a decoy-competed total-row pick must abstain-gate below the 0.8 guard floor");
    }

    [Fact]
    public void ScoreTotalRow_BothSignalsFail_MultipliesPenaltiesIndependently()
    {
        var signals = new TotalRowGeometricSignals(LabelRankAdjacent: false, HasCompetingAmount: true);

        var score = GeometricPlausibilityScorer.ScoreTotalRow(signals, TotalRowCalibration);

        // 0.55 * 0.55 = 0.3025 — the product, not an average (same B2 false-confidence shape
        // the multiplicative formula rules out throughout C1).
        score.ShouldBe(0.3025, 0.0001);
    }

    [Fact]
    public void FieldCalibrationTable_TotalRow_TotalCargosAndTotalAbonos_ShareTheSameConstants()
    {
        // Both fields are read via the identical single-pass TryParseTotalRow mechanism — a
        // future dev who intentionally differentiates one must update this test, not silently
        // drift the two apart.
        var cargos = FieldCalibrationTable.TotalRow[FieldKind.TotalCargos];
        var abonos = FieldCalibrationTable.TotalRow[FieldKind.TotalAbonos];

        cargos.ShouldBe(abonos);
    }
}

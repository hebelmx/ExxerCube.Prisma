using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

// ---------------------------------------------------------------------------
// C2.0b — MAKE-OR-BREAK separation-spike prototype scorer for the two
// recompute-operand money fields (SaldoCargosRegulares /
// PagoParaNoGenerarIntereses). THROWAWAY MEASUREMENT — proves separation
// exists on the synthetic corpus and hands C2.1 the settled signals/
// constants/mechanism; NOT wired into PdfPigStatementFieldExtractor. See
// docs/planning-artifacts/SCOPING-veriqan-c2-recompute-operand-confidence.md
// for the design authority this file implements.
// ---------------------------------------------------------------------------

/// <summary>
/// One score verdict, with the raw signal readings and human-readable reasons — so a spike
/// failure is self-explanatory without re-deriving the math.
/// </summary>
/// <param name="Score">Final multiplicative score in [0, 1].</param>
/// <param name="RankAdjacent">Signal #1 reading: is the picked candidate within
/// <see cref="HeaderMoneyPlausibilityScorerPrototype.RankAdjacencyThreshold"/> content-token
/// ranks of the row's label?</param>
/// <param name="CandidateCount">Signal #2 reading: how many amount-pattern tokens are in
/// the field's own X-window.</param>
/// <param name="Reasons">Which penalty(ies) fired, and why — empty when the pick is clean.</param>
internal sealed record HeaderMoneyScoreResult(
    double Score,
    bool RankAdjacent,
    int CandidateCount,
    IReadOnlyList<string> Reasons);

/// <summary>
/// C2.0b prototype of the geometric-plausibility scorer for the
/// SaldoCargosRegulares / PagoParaNoGenerarIntereses picks
/// (<c>ExtractNivelDeUsoField</c> / <c>ExtractPagoParaNoGenerarIntereses</c>'s
/// rightmost-in-band positional heuristic). Two signals, MULTIPLICATIVE penalty
/// (mirrors C1.0b's design decision — a red flag must be able to drag the score
/// below 0.8 alone, never get diluted by an unrelated clean signal):
///
/// <list type="number">
/// <item>
/// <b>LabelRankAdjacent</b>: is the picked candidate within
/// <see cref="RankAdjacencyThreshold"/> ORDINAL ranks of the row's label, counting
/// only "content" tokens? Bare "$" sign tokens are excluded from the rank count —
/// they are a PDF-tokenization split artifact of currency formatting (some rows
/// render "$ 32,446.69" as two tokens, others render "$32,446.69" as one; the
/// underlying label-to-value proximity is identical either way, and counting the
/// "$" as a rank would silently require a DIFFERENT threshold per field purely
/// because of typesetting, not geometry — see remarks below).
/// </item>
/// <item>
/// <b>HasCompetingAmount</b>: are there 2+ amount-pattern tokens in the field's
/// own X-window (open for SaldoCargosRegulares, maxX=300 for
/// PagoParaNoGenerarIntereses)? A decoy token always adds a second candidate.
/// </item>
/// </list>
///
/// <b>Honest caveat, found during this spike (not swept under the rug):</b> on this
/// 6-specimen corpus the two signals are PERFECTLY CORRELATED — every decoy trips
/// both, every clean pick trips neither. That is a structural consequence of how
/// both the corpus and the extractor are built: the production pick rule is
/// unconditionally "rightmost wins", and every decoy in this corpus was
/// constructed to sit further right (i.e. rank-further from the label) than the
/// true value. Given that, "picked candidate is not the nearest one" and "there
/// were 2+ candidates" are logically forced to co-occur — this spike's 6
/// specimens cannot demonstrate that the two signals carry INDEPENDENT
/// information. A future specimen with a decoy positioned closer to the label
/// than the true value (so the count fires but rank-adjacency does not) would be
/// needed to prove genuine independence. Both signals are kept anyway (matches
/// the task brief's specified design and C1.2's own per-signal multiplicative
/// pattern), but the redundancy is reported, not hidden.
/// </summary>
internal static class HeaderMoneyPlausibilityScorerPrototype
{
    /// <summary>
    /// Amount-token pattern — identical to
    /// <c>PdfPigStatementFieldExtractor.AmountPattern</c> (kept independently here;
    /// this spike must not depend on production internals).
    /// </summary>
    private static readonly Regex AmountPattern = new(@"^\$?([\d,]+(?:\.\d+)?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // -----------------------------------------------------------------------
    // Constants — FROZEN on the TRAIN set only (clean-nivel-pago,
    // decoy-nivel-uso-amount, decoy-pago-sin-intereses-amount — the dummievec
    // specimens). Never adjusted after looking at the HOLDOUT set's scores
    // (clean-nivel-pago-realbanamex, decoy-nivel-uso-amount-realbanamex,
    // decoy-pago-sin-intereses-amount-realbanamex) — see
    // C2_0b_HeaderMoneySeparationSpikeTests for the train/holdout split
    // enforcement.
    //
    // Both penalties are set far below the 0.8 floor (clean picks score exactly
    // 1.0 on this corpus — both signals pass cleanly, no partial credit needed)
    // so the margin is wide, not "just clearing" the 0.15 bar.
    // -----------------------------------------------------------------------

    /// <summary>Penalty applied when signal #1 (rank adjacency) does not fire.</summary>
    public const double RankNotAdjacentPenalty = 0.55;

    /// <summary>Penalty applied when signal #2 (competing-amount count) exceeds 1.</summary>
    public const double CompetingAmountPenalty = 0.65;

    /// <summary>
    /// Adjacency bar: the picked candidate must be within this many content-token
    /// ranks of the row's last label token. Settled at 1 on the train set — once
    /// bare "$" tokens are excluded from the rank count, this single constant
    /// separates clean from decoy identically on BOTH fields (no contradictory
    /// per-field tuning needed — see the scorer's class remarks).
    /// </summary>
    public const int RankAdjacencyThreshold = 1;

    /// <summary>
    /// Scores the money pick found in one already-isolated, already-X-windowed value
    /// row (the row <c>ExtractNivelDeUsoField</c> / <c>ExtractPagoParaNoGenerarIntereses</c>'s
    /// label-anchored scan would have selected — see <see cref="HeaderMoneyBandLocator"/>
    /// for the faithful reproduction of that search over a REAL PdfPig-tokenized page).
    /// </summary>
    public static HeaderMoneyScoreResult Score(HeaderMoneyBandContext context)
    {
        // "$" sign tokens are transparent to rank counting — see class remarks.
        var contentTokens = context.Band
            .Where(t => t.Text != "$")
            .OrderBy(t => t.Left)
            .ToList();

        var candidates = contentTokens
            .Select((token, index) => (Token: token, Index: index))
            .Where(x => AmountPattern.IsMatch(x.Token.Text))
            .ToList();

        if (candidates.Count == 0)
            return new HeaderMoneyScoreResult(0.0, false, 0, ["no amount-pattern candidates in window"]);

        // Rightmost-wins — mirrors FindAmountInBand's default (findLeftmost: false),
        // the actual production pick rule for both fields in this slice.
        var picked = candidates.OrderByDescending(x => x.Token.Left).First();

        var lastLabelIndex = context.LabelTokenCount - 1;
        var rankDistance = picked.Index - lastLabelIndex;
        var rankAdjacent = rankDistance <= RankAdjacencyThreshold;

        var hasCompeting = candidates.Count > 1;

        var score = 1.0;
        var reasons = new List<string>();

        if (!rankAdjacent)
        {
            score *= RankNotAdjacentPenalty;
            reasons.Add(
                $"picked token is {rankDistance} content-ranks from the label, expected <= {RankAdjacencyThreshold} (penalty x{RankNotAdjacentPenalty:0.00})");
        }

        if (hasCompeting)
        {
            score *= CompetingAmountPenalty;
            reasons.Add(
                $"{candidates.Count} competing amount-pattern tokens in window, expected 1 (penalty x{CompetingAmountPenalty:0.00})");
        }

        return new HeaderMoneyScoreResult(score, rankAdjacent, candidates.Count, reasons);
    }
}

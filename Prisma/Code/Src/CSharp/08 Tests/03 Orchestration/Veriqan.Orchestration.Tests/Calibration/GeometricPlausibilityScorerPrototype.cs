using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

// ---------------------------------------------------------------------------
// C1.0b — separation-spike prototype scorer (THROWAWAY MEASUREMENT, NOT the
// production scorer — that is C1.2's job). Exists to prove separation exists
// on the synthetic corpus and to hand C1.2 the settled signals/constants/
// mechanism. See docs/planning-artifacts/SCOPING-veriqan-c1-geometric-
// extraction-confidence.md ("C1 intended-solution design") and
// docs/planning-artifacts/TRACKER-veriqan-c1-geometric-confidence.md
// ("CRITICAL FINDING") for the design authority this file implements.
// ---------------------------------------------------------------------------

/// <summary>
/// A PdfPig-free snapshot of one <c>Word</c> (text + X-extent only — the two signals below
/// are purely X-order / adjacency, no Y needed once the value band is already isolated).
/// Deliberately NOT <c>UglyToad.PdfPig.Content.Word</c> — the production scorer (C1.2) is a
/// pure function of a <c>Signals</c>/token-snapshot record per the party's architecture
/// decision 2 ("no PdfPig types, no I/O"); this spike proves that seam works.
/// </summary>
/// <param name="Text">The token's exact text (e.g. "28.86%", "sin", "IVA").</param>
/// <param name="Left">Left-edge X (PdfPig space).</param>
/// <param name="Right">Right-edge X (PdfPig space).</param>
internal readonly record struct SpikeToken(string Text, double Left, double Right);

/// <summary>
/// One score verdict, with the raw signal readings and human-readable reasons — so a spike
/// failure (or the negative-control "confirmed blind spot") is self-explanatory without
/// re-deriving the math.
/// </summary>
/// <param name="Score">Final multiplicative score in [0, 1].</param>
/// <param name="SiblingAdjacentToCatPick">
/// Signal #1 reading: is a "sin" token immediately followed by an "IVA" token, positioned
/// strictly between the CAT-picked token's right edge and the TASA-picked token's left edge
/// (or unbounded to the right if no second percent token exists)?
/// </param>
/// <param name="CompetingPercentTokenCount">Signal #2 reading: how many percent tokens are in the band.</param>
/// <param name="Reasons">Which penalty(ies) fired, and why — empty when the pick is clean.</param>
internal sealed record SpikeScoreResult(
    double Score,
    bool SiblingAdjacentToCatPick,
    int CompetingPercentTokenCount,
    IReadOnlyList<string> Reasons);

/// <summary>
/// C1.0b prototype of the geometric-plausibility scorer for the Tasa/Cat pick
/// (<c>ExtractTasaAndCat</c>'s <c>pctTokens[0]</c>=CAT / <c>pctTokens[1]</c>=TASA
/// ordinal-position heuristic). Two signals, MULTIPLICATIVE penalty (party design decision 2 —
/// a weighted sum would let one clean signal dilute a damning one; any single red flag must be
/// able to drag the score below 0.8 alone):
///
/// <list type="number">
/// <item>
/// <b>Sibling / order-marker adjacency</b> (LOAD-BEARING for the swap, per the party's design
/// decision 1): CONDUSEF statements print "sin IVA" as a legal qualifier bound to CAT
/// specifically ("Costo Anual Total, sin IVA") — on every clean fixture in this corpus (both the
/// Dummie-VEC and real-Banamex profiles) the marker sits immediately after the FIRST (CAT-picked)
/// percent token. If it is absent, or displaced onto the TASA-picked token instead, the pick is
/// untrustworthy — REGARDLESS of whether extraction happens to still be numerically correct
/// (<c>missing-order-marker</c> proves the two are independent: extraction unaffected, signal
/// absent, score must still dip).
/// </item>
/// <item>
/// <b>Token-competition count</b>: more than 2 percent tokens in the value band means a decoy
/// competed for the slot. This does NOT catch the swap (a swap never changes the count — see the
/// C1.0a finding that <c>pctTokens.Count</c> is structurally always 2 for a transposition) — it
/// exists for the spurious-extra-token failure class only (<c>decoy-percent</c>).
/// </item>
/// </list>
///
/// <b>Settled design question (party's "penalty vs. gate", left open for C1.0b to decide from
/// data):</b> sibling-presence is a PENALTY FACTOR, NOT A GATE. A gate ("sibling present ⇒ skip
/// the competition penalty entirely") was considered and REJECTED: <c>decoy-percent</c>'s own
/// god's-eye manifest (<c>confidenceExpectations: {Cat: low, Tasa: low}</c>, written in C1.0a)
/// asserts the 3-competing-token read must score LOW even though "sin IVA" is correctly adjacent
/// (sibling signal fires clean). A gate would score that specimen 1.0 — contradicting its own
/// fixture's ground truth. So the two signals stay independent multiplicative factors.
/// </summary>
internal static class GeometricPlausibilityScorerPrototype
{
    /// <summary>
    /// Percent-token pattern — identical to <c>PdfPigStatementFieldExtractor.PercentPattern</c>
    /// (kept independently here; this spike must not depend on production internals).
    /// </summary>
    private static readonly Regex PercentPattern = new(@"^\d+(\.\d+)?%$", RegexOptions.Compiled);

    // -----------------------------------------------------------------------
    // Constants — FROZEN on the TRAIN set only (s6211-baseline clean,
    // missing-order-marker + decoy-percent ambiguous). Never adjusted after
    // looking at the HOLDOUT set's scores (s622-realbanamex-baseline,
    // s6211-var-a/b/c, s-c1-swap-displaced) — see
    // C1_0b_SeparationSpikeTests for the train/holdout split enforcement.
    //
    // Both penalties are set far below the 0.8 floor (clean picks score exactly
    // 1.0 — both signals pass cleanly, no partial credit needed) so the margin
    // is wide (>=0.35 on this corpus), not "just clearing" the 0.15 bar.
    // -----------------------------------------------------------------------

    /// <summary>Penalty applied when signal #1 (sibling adjacency) does not fire.</summary>
    public const double SiblingAbsentPenalty = 0.55;

    /// <summary>Penalty applied when signal #2 (competition count) exceeds 2.</summary>
    public const double CompetitionExcessPenalty = 0.65;

    /// <summary>
    /// Scores the CAT/TASA positional pick found in one already-isolated value band (the band
    /// <c>ExtractTasaAndCat</c>'s label-anchored scan would have selected — see
    /// <see cref="SpikeBandLocator"/> for the faithful reproduction of that band search over a
    /// REAL PdfPig-tokenized page).
    /// </summary>
    public static SpikeScoreResult Score(IReadOnlyList<SpikeToken> bandWords)
    {
        var pctTokens = bandWords
            .Where(w => PercentPattern.IsMatch(w.Text))
            .OrderBy(w => w.Left)
            .ToList();

        if (pctTokens.Count == 0)
            return new SpikeScoreResult(0.0, false, 0, ["no percent tokens in band"]);

        var catToken = pctTokens[0];
        SpikeToken? tasaToken = pctTokens.Count >= 2 ? pctTokens[1] : null;

        var siblingAdjacent = HasSiblingImmediatelyAfterCatPick(bandWords, catToken, tasaToken);

        var score = 1.0;
        var reasons = new List<string>();

        if (!siblingAdjacent)
        {
            score *= SiblingAbsentPenalty;
            reasons.Add(
                $"'sin IVA' not immediately adjacent to the CAT-picked token (penalty x{SiblingAbsentPenalty:0.00})");
        }

        if (pctTokens.Count > 2)
        {
            score *= CompetitionExcessPenalty;
            reasons.Add(
                $"{pctTokens.Count} competing percent tokens in band, expected 2 (penalty x{CompetitionExcessPenalty:0.00})");
        }

        return new SpikeScoreResult(score, siblingAdjacent, pctTokens.Count, reasons);
    }

    /// <summary>
    /// Signal #1: is there a "sin" word immediately followed by an "IVA" word, positioned
    /// strictly between the CAT pick's right edge and the TASA pick's left edge (or anywhere to
    /// the right of CAT, unbounded, when there is no second percent token)?
    ///
    /// <b>RANK-based, not edge-interval-based (perturbation-stress finding — see C1.0b spike
    /// verdict):</b> a first attempt compared <c>sin</c>'s Left against <c>catPick.Right</c> (an
    /// EDGE gap). That edge gap is only the space-character width (~2.2pt in this corpus) — well
    /// inside a &#177;3pt independent jitter's combined swing (up to 6pt), so it flipped under
    /// perturbation even for clean specimens. Word LEFT-coordinates, by contrast, are spaced by a
    /// whole word-width apart (12–30pt here) — comparing ORDINAL RANK by Left (not raw edge
    /// distance) is what a real layout actually guarantees to survive small measurement noise:
    /// PdfPig's own word segmentation already had to resolve "which word is this", the fragile
    /// part was re-deriving adjacency from bounding-box arithmetic instead of using the ranking
    /// PdfPig already computed. This is the version that passed the perturbation test.
    /// </summary>
    private static bool HasSiblingImmediatelyAfterCatPick(
        IReadOnlyList<SpikeToken> bandWords,
        SpikeToken catPick,
        SpikeToken? tasaPick)
    {
        var ordered = bandWords.OrderBy(w => w.Left).ToList();
        var catIndex = ordered.IndexOf(catPick);
        if (catIndex < 0 || catIndex + 2 >= ordered.Count)
            return false;

        var next = ordered[catIndex + 1];
        var nextNext = ordered[catIndex + 2];

        if (!string.Equals(next.Text, "sin", System.StringComparison.OrdinalIgnoreCase)
            || !string.Equals(nextNext.Text, "IVA", System.StringComparison.OrdinalIgnoreCase))
            return false;

        // The marker must precede the TASA pick (not trail after it) — otherwise it is
        // qualifying a DIFFERENT value than the one this extractor picked as CAT.
        return tasaPick is null || nextNext.Left < tasaPick.Value.Left;
    }
}

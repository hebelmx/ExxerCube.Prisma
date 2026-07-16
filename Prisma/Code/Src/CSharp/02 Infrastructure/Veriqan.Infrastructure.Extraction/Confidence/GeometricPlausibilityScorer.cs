using System;
using System.Collections.Generic;
using System.Linq;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Confidence;

// ---------------------------------------------------------------------------
// C1.2 — production geometric-plausibility scorer. Lifts the mechanism proven
// by the C1.0b make-or-break spike
// (Veriqan.Orchestration.Tests/Calibration/GeometricPlausibilityScorerPrototype.cs)
// verbatim: same two signals, same MULTIPLICATIVE formula, same constants. See
// docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md
// ("C1 intended-solution design", decision 2 — architecture) and
// docs/planning-artifacts/TRACKER-veriqan-c1-geometric-confidence.md
// ("C1.0b SPIKE VERDICT = GO") for the design authority this file implements.
// Deliberately a SEPARATE type from the spike prototype — the prototype stays
// as the make-or-break record and must not be deleted or repointed here.
// ---------------------------------------------------------------------------

/// <summary>
/// A pure, PdfPig-free snapshot of one word's text and horizontal extent — the only shape of
/// token data the geometric-plausibility scorer is allowed to see (party architecture decision
/// 2: "no PdfPig types, no I/O" cross the scorer seam). Callers (e.g.
/// <see cref="Veriqan.Infrastructure.Extraction.PdfPigStatementFieldExtractor"/>) project
/// <c>UglyToad.PdfPig.Content.Word</c> into this shape before calling into this namespace.
/// </summary>
/// <param name="Text">The token's exact text (e.g. "28.86%", "sin", "IVA").</param>
/// <param name="Left">Left-edge X (PdfPig space).</param>
/// <param name="Right">Right-edge X (PdfPig space).</param>
internal readonly record struct GeometricToken(string Text, double Left, double Right);

/// <summary>
/// The precomputed, field-agnostic signal readings <see cref="GeometricPlausibilityScorer.Score"/>
/// consumes. Building a <see cref="GeometricSignals"/> from raw tokens is a separate step (see
/// <see cref="GeometricPlausibilityScorer.IsSiblingAdjacentToPick"/>) so the scorer itself stays a
/// pure function of already-computed booleans/ints, per the party's architecture decision —
/// straightforward to unit-test by hand-constructing this record without any token/band logic.
/// </summary>
/// <param name="SiblingAdjacentToPick">
/// Signal #1 (LOAD-BEARING for the CAT/TASA swap): is the disambiguating "sin IVA" order-marker
/// immediately adjacent (by ordinal rank, not raw edge-distance) to the picked token, and
/// positioned before the sibling pick?
/// </param>
/// <param name="CompetingTokenCount">
/// Signal #2: how many candidate tokens (e.g. percent tokens) competed for this slot in the
/// value band. More than 2 means a decoy token competed. Structurally inert for a swap (a
/// transposition never changes the count) — it exists for the spurious-extra-token failure class.
/// </param>
internal readonly record struct GeometricSignals(bool SiblingAdjacentToPick, int CompetingTokenCount);

/// <summary>
/// Per-field calibration constants — facts about a statement template's geometry, not tenant
/// policy (party architecture decision 2: "NOT tenant config; tenant config invites 'fixing'
/// calibration by editing JSON instead of re-running the spike"). See
/// <see cref="FieldCalibrationTable"/> for the frozen values.
/// </summary>
/// <param name="SiblingAbsentPenalty">
/// Multiplicative penalty applied when <see cref="GeometricSignals.SiblingAdjacentToPick"/> is
/// <see langword="false"/>.
/// </param>
/// <param name="CompetitionExcessPenalty">
/// Multiplicative penalty applied when <see cref="GeometricSignals.CompetingTokenCount"/> exceeds 2.
/// </param>
internal sealed record FieldCalibration(double SiblingAbsentPenalty, double CompetitionExcessPenalty);

/// <summary>
/// Static table of per-field <see cref="FieldCalibration"/> constants. A field only needs an
/// entry once a call site actually emits a geometric-plausibility confidence for it (today: Tasa
/// and Cat, sharing one entry because both are read from the same value-band pick in
/// <c>ExtractTasaAndCat</c>). C1.4 will add entries for the RESUMEN/NIVEL/DESGLOSE money fields
/// once <c>ScanResumenColumn</c> is wired.
/// </summary>
internal static class FieldCalibrationTable
{
    /// <summary>
    /// Constants frozen by the C1.0b train/holdout separation spike (margin 0.35, ±3pt-jitter
    /// robust) for the Tasa/Cat percent-token pick in <c>ExtractTasaAndCat</c>. Both penalties
    /// are set far below the 0.8 guard floor — a clean pick scores exactly 1.0 (both signals
    /// pass), so the margin to the floor is wide, not "just clearing" it.
    /// </summary>
    public static readonly FieldCalibration TasaCat = new(
        SiblingAbsentPenalty: 0.55,
        CompetitionExcessPenalty: 0.65);
}

/// <summary>
/// Production geometric-plausibility scorer for positionally-extracted money/rate fields (the C1
/// lever). Pure function of a <see cref="GeometricSignals"/> and a <see cref="FieldCalibration"/>
/// — no PdfPig types, no I/O, fast and hand-constructible in unit tests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two signals, MULTIPLICATIVE penalty</b> (party architecture decision 2 — a weighted sum
/// would let one clean signal dilute a damning one into a passable average, the B2 false-
/// confidence shape repeated at the calibration layer; a multiplicative penalty lets any single
/// red flag drag the score below the 0.8 guard floor on its own):
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Sibling / order-marker adjacency</b> — LOAD-BEARING for the CAT/TASA swap. CONDUSEF
/// statements print "sin IVA" as a legal qualifier bound to CAT specifically ("Costo Anual
/// Total, sin IVA"); on every clean fixture the marker sits immediately after the CAT-picked
/// token by ordinal rank. Absent or displaced onto the other pick ⇒ untrustworthy, regardless of
/// whether extraction happens to still be numerically correct (proven independent by the
/// <c>missing-order-marker</c> specimen).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Token-competition count</b> — more than 2 candidate tokens in the band means a decoy
/// competed. Does NOT catch a swap (a transposition never changes the count — the C1.0a
/// finding); exists for the spurious-extra-token failure class only (<c>decoy-percent</c>).
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Settled (not a gate):</b> sibling-presence does not waive the competition penalty —
/// <c>decoy-percent</c>'s own god's-eye manifest requires a LOW score even though its sibling
/// marker is correctly adjacent, which a gate would score 1.0, contradicting the fixture's own
/// ground truth. The two signals stay independent multiplicative factors.
/// </para>
/// </remarks>
internal static class GeometricPlausibilityScorer
{
    /// <summary>
    /// Scores a field's positional pick from its precomputed <paramref name="signals"/> against
    /// <paramref name="calibration"/>. Clean picks (both signals pass) score exactly 1.0; any
    /// failing signal multiplies in its own penalty independently of the other.
    /// </summary>
    /// <param name="signals">The precomputed signal readings for this pick.</param>
    /// <param name="calibration">The per-field calibration constants to apply.</param>
    /// <returns>A score in [0, 1].</returns>
    public static double Score(GeometricSignals signals, FieldCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        var score = 1.0;

        if (!signals.SiblingAdjacentToPick)
            score *= calibration.SiblingAbsentPenalty;

        if (signals.CompetingTokenCount > 2)
            score *= calibration.CompetitionExcessPenalty;

        return score;
    }

    /// <summary>
    /// Signal #1: is there a "sin" token immediately followed by an "IVA" token — by ORDINAL
    /// RANK within <paramref name="bandTokens"/> sorted by <see cref="GeometricToken.Left"/>, not
    /// raw edge-distance — positioned immediately after <paramref name="pick"/> and, when
    /// <paramref name="siblingPick"/> is present, strictly before it?
    /// </summary>
    /// <remarks>
    /// <b>Rank-based, not edge-interval-based</b> — the C1.0b perturbation-stress finding. An
    /// edge-gap comparison (sibling's Left vs. pick's Right) is only the space-character width
    /// (~2.2pt in this corpus), well inside a ±3pt independent jitter's combined swing (up to
    /// 6pt), so it flips under perturbation even for clean specimens. Word LEFT-coordinates are
    /// spaced a whole word-width apart (12–30pt here) — comparing ORDINAL RANK by Left is what
    /// a real layout actually guarantees to survive small measurement noise; this is the version
    /// that passed the C1.0b ±3pt perturbation test (50 trials, both directions).
    /// </remarks>
    /// <param name="bandTokens">Every token in the already-isolated value band.</param>
    /// <param name="pick">The token this extractor picked as the field's value.</param>
    /// <param name="siblingPick">
    /// The other field's picked token in the same band (e.g. TASA's pick when scoring CAT), or
    /// <see langword="null"/> when there is none — the marker is then unbounded to the right.
    /// </param>
    public static bool IsSiblingAdjacentToPick(
        IReadOnlyList<GeometricToken> bandTokens,
        GeometricToken pick,
        GeometricToken? siblingPick)
    {
        ArgumentNullException.ThrowIfNull(bandTokens);

        var ordered = bandTokens.OrderBy(t => t.Left).ToList();
        var pickIndex = ordered.IndexOf(pick);
        if (pickIndex < 0 || pickIndex + 2 >= ordered.Count)
            return false;

        var next = ordered[pickIndex + 1];
        var nextNext = ordered[pickIndex + 2];

        if (!string.Equals(next.Text, "sin", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(nextNext.Text, "IVA", StringComparison.OrdinalIgnoreCase))
            return false;

        // The marker must precede the sibling pick (not trail after it) — otherwise it is
        // qualifying a DIFFERENT value than the one this extractor picked.
        return siblingPick is null || nextNext.Left < siblingPick.Value.Left;
    }
}

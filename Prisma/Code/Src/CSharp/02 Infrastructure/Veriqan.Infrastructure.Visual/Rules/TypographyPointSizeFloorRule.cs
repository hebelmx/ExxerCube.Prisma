using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// LAW-TYPO-MINSIZE: Verifies that body text renders at ≥ 8 pt (Arial-equivalent floor)
/// and that the <i>fecha límite de pago</i> field renders at ≥ 10 pt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — pure structural
/// inspection of the PDF text layer via CTM-accounted <see cref="TextTypographySample.PointSize"/>
/// values produced by PdfPig.
/// </para>
/// <para>
/// <b>Cardinal rule — never false-Fail:</b> when reliable size data cannot be obtained the
/// rule returns <c>InsufficientData</c> rather than Fail.
/// </para>
/// <para>
/// <b>Body-floor filter:</b> only "real word" samples (<see cref="TextTypographySample.Text"/>
/// trimmed length ≥ 2) are checked against the 8 pt floor.  Single-character glyphs
/// (superscripts, ®, footnote marks) are excluded to avoid false-Fail on decorative glyphs.
/// </para>
/// <para>
/// <b>Fecha-límite sub-check (best-effort):</b>
/// <list type="number">
///   <item>
///     Prefer the <see cref="PeriodSummary.PaymentDueDate"/> extracted field when it is in
///     <see cref="ExtractionStatus.Extracted"/> status — its locator gives the exact label
///     region; the PointSize of the typography sample <b>nearest to the right of</b> that
///     locator on the same page and vertical band is used for the size check.
///   </item>
///   <item>
///     If no extracted field is available, scan <see cref="StatementModel.TypographySamples"/>
///     for consecutive words on one line (same page, overlapping vertical band) whose
///     accent-stripped lower-cased text forms the phrase "fecha limite de pago".
///   </item>
///   <item>
///     If the field cannot be confidently located, the rule returns
///     <c>InsufficientData</c> (the ≥10 pt floor could not be verified).
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Band + X-proximity join (two-column safety):</b> when joining typography samples
/// to the PaymentDueDate locator, only samples within ±<see cref="SameLineTolerance"/>
/// vertically AND within a horizontal window of the label's <c>Left</c> coordinate are
/// considered, so that the join does not cross into an adjacent column.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="TypographyExtractionStatus.NotFound"/> (scanned/image PDF — no text layer).</item>
///   <item><see cref="StatementModel.TypographySamples"/> is empty.</item>
///   <item>No real-word body samples exist (nothing checkable against the 8 pt floor).</item>
///   <item>Body floor is OK but the fecha-límite sub-check could not be located.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class TypographyPointSizeFloorRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Regulatory floors (constants — BaselineLocked rule carries no registered tolerance).
    private const double BodyFloorPt = 8.0;
    private const double FechaLimiteFloorPt = 10.0;

    // Epsilon biases toward Pass: only a CONFIDENT breach (size strictly below floor - epsilon) Fails.
    private const double Epsilon = 0.25;

    // Vertical-band tolerance for "same line" heuristic.
    // Matched to the extractor's Y-band tolerance (~5 pt) so value glyphs sitting up to 5 pt
    // off the locator Bottom are still joined.
    private const double SameLineTolerance = 5.0;

    // Horizontal window for the Strategy-1 proximity join (two-column safety).
    // Samples must start at or to the right of the label left minus a small slack,
    // and no further than 300 pt to the right of the label.
    private const double HorizLeftSlack = 5.0;
    private const double HorizRightLimit = 300.0;

    /// <inheritdoc />
    public string CheckId => "LAW-TYPO-MINSIZE";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo / Guía de llenado — Tipografía (puntaje mínimo)";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;

        // InsufficientData: no model or no text layer present.
        if (model is null
            || model.TypographyExtractionStatus == TypographyExtractionStatus.NotFound
            || model.TypographySamples.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No typography data extracted from the PDF text layer."));
        }

        // -----------------------------------------------------------------------
        // Body-floor check: real-word samples (trimmed length >= 2) only.
        // -----------------------------------------------------------------------

        TextTypographySample? bodyOffender = null;
        var bodyViolationCount = 0;
        var realWordCount = 0;
        var minBodyPt = double.MaxValue;

        foreach (var sample in model.TypographySamples)
        {
            if (sample.Text.Trim().Length < 2)
                continue; // Skip single-char glyphs (superscripts, ®, footnote marks).

            realWordCount++;

            if (sample.PointSize < minBodyPt)
                minBodyPt = sample.PointSize;

            if (sample.PointSize < BodyFloorPt - Epsilon)
            {
                bodyViolationCount++;
                if (bodyOffender is null || sample.PointSize < bodyOffender.PointSize)
                    bodyOffender = sample;
            }
        }

        // InsufficientData: no real-word samples means we cannot evaluate the body floor.
        if (realWordCount == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No real-word body samples to evaluate the floor (all samples are single-character glyphs)."));
        }

        // Body violation takes highest precedence — report immediately.
        if (bodyOffender is not null)
        {
            var detail = bodyViolationCount > 1
                ? $"Body text floor breach: {bodyViolationCount} real-word sample(s) below {BodyFloorPt} pt. " +
                  $"Lowest offender: {bodyOffender.PointSize:F2} pt ('{bodyOffender.Text}', page {bodyOffender.PageNumber})."
                : $"Body text floor breach: '{bodyOffender.Text}' (page {bodyOffender.PageNumber}) " +
                  $"rendered at {bodyOffender.PointSize:F2} pt (floor: {BodyFloorPt} pt).";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"≥ {BodyFloorPt} pt (body text floor)",
                    observed: detail,
                    locator: bodyOffender.Locator));
        }

        // -----------------------------------------------------------------------
        // Fecha-límite sub-check (best-effort — abstain on uncertainty).
        // -----------------------------------------------------------------------

        TextTypographySample? fechaOffender = null;
        var fechaSubCheckLocated = false;
        double? fechaMinPt = null;

        // Strategy 1: use the extracted PaymentDueDate field locator when available.
        if (model.PeriodSummary?.PaymentDueDate is { Status: ExtractionStatus.Extracted } paymentField)
        {
            var labelLocator = paymentField.Locator;

            // Find the typography sample on the same page closest to (just right of) the label.
            // Two-column safety: constrain both vertically (±SameLineTolerance) AND horizontally
            // (within [labelLeft - HorizLeftSlack, labelLeft + HorizRightLimit]).
            if (labelLocator.Bottom is not null)
            {
                fechaSubCheckLocated = true;

                var labelBottom = labelLocator.Bottom.Value;
                var labelPage = labelLocator.PageNumber;
                var labelLeft = labelLocator.Left; // may be null

                TextTypographySample? candidate = null;
                double? candidateLeftDist = null; // distance from label Left (smaller = nearer)

                foreach (var sample in model.TypographySamples)
                {
                    if (sample.PageNumber != labelPage)
                        continue;
                    if (sample.Locator.Bottom is null)
                        continue;

                    // Vertical band.
                    var vertDist = Math.Abs(sample.Locator.Bottom.Value - labelBottom);
                    if (vertDist > SameLineTolerance)
                        continue;

                    // Horizontal window (only applied when the label locator has a Left value).
                    if (labelLeft is not null && sample.Locator.Left is not null)
                    {
                        var sampleLeft = sample.Locator.Left.Value;
                        var lo = labelLeft.Value - HorizLeftSlack;
                        var hi = labelLeft.Value + HorizRightLimit;
                        if (sampleLeft < lo || sampleLeft > hi)
                            continue;
                    }

                    // Pick the sample whose Left is closest to labelLeft (nearest right of label).
                    double dist;
                    if (labelLeft is not null && sample.Locator.Left is not null)
                        dist = Math.Abs(sample.Locator.Left.Value - labelLeft.Value);
                    else
                        dist = 0; // no X info — treat as nearest

                    if (candidateLeftDist is null || dist < candidateLeftDist)
                    {
                        candidateLeftDist = dist;
                        candidate = sample;
                    }
                }

                if (candidate is not null)
                {
                    fechaMinPt = candidate.PointSize;
                    if (candidate.PointSize < FechaLimiteFloorPt - Epsilon)
                        fechaOffender = candidate;
                }
            }
        }

        // Strategy 2: phrase scan — only when Strategy 1 did not locate the field.
        if (!fechaSubCheckLocated)
        {
            var phraseResult = TryLocateFechaLimitePhrase(model.TypographySamples);
            if (phraseResult is not null)
            {
                fechaSubCheckLocated = true;
                fechaMinPt = phraseResult.PointSize;
                if (phraseResult.PointSize < FechaLimiteFloorPt - Epsilon)
                    fechaOffender = phraseResult;
            }
        }

        // -----------------------------------------------------------------------
        // Verdict — body is OK at this point; now decide on fecha sub-check.
        // -----------------------------------------------------------------------

        // Fecha-límite confident violation.
        if (fechaOffender is not null)
        {
            var detail = $"'Fecha límite de pago' field rendered at {fechaOffender.PointSize:F2} pt " +
                         $"(page {fechaOffender.PageNumber}); floor: {FechaLimiteFloorPt} pt.";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"≥ {FechaLimiteFloorPt} pt (fecha límite de pago floor)",
                    observed: detail,
                    locator: fechaOffender.Locator));
        }

        // Body floor OK but fecha sub-check could not be located → abstain.
        // A clean Pass that hides an unverified mandated floor would be a false-Pass.
        if (!fechaSubCheckLocated)
        {
            var minBodyDisplay = $"{minBodyPt:F2} pt";
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: $"Body floor OK at min {minBodyDisplay}, but the ≥{FechaLimiteFloorPt} pt " +
                            "fecha-límite floor could not be verified — the label was not located " +
                            "(skipped: label not confidently located)."));
        }

        // Body OK AND fecha located and OK → Pass.
        var minBodyStr = $"{minBodyPt:F2} pt";
        var fechaStatusStr = $"fecha-límite sub-check located (min {fechaMinPt:F2} pt — OK)";

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"{realWordCount} real-word sample(s) checked; min body size: {minBodyStr}; {fechaStatusStr}."));
    }

    // -----------------------------------------------------------------------
    // Fecha-límite phrase scan helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scans <paramref name="samples"/> for consecutive words on the same page and
    /// horizontal line that spell out "fecha limite de pago" (accent-insensitive,
    /// case-insensitive).  Returns the typography sample for the matching line segment
    /// with the <b>smallest</b> <see cref="TextTypographySample.PointSize"/> (most
    /// constraining for the floor check), or <see langword="null"/> when the phrase
    /// cannot be located with confidence.
    /// </summary>
    private static TextTypographySample? TryLocateFechaLimitePhrase(
        IReadOnlyList<TextTypographySample> samples)
    {
        // Target tokens (accent-stripped, lower-cased).
        ReadOnlySpan<string> target = ["fecha", "limite", "de", "pago"];

        // Group samples by page, sorted by ascending Left within each page so we can
        // scan left-to-right for the phrase tokens.
        var byPage = new Dictionary<int, List<TextTypographySample>>();
        foreach (var s in samples)
        {
            if (!byPage.TryGetValue(s.PageNumber, out var list))
            {
                list = [];
                byPage[s.PageNumber] = list;
            }

            list.Add(s);
        }

        foreach (var pageSamples in byPage.Values)
        {
            // Sort by Bottom descending (PDF origin is bottom-left, higher Y = higher on page),
            // then by Left ascending within the same band.
            pageSamples.Sort(static (a, b) =>
            {
                var bottomA = a.Locator.Bottom ?? 0;
                var bottomB = b.Locator.Bottom ?? 0;
                var cmp = bottomB.CompareTo(bottomA); // descending by Y
                if (cmp != 0)
                    return cmp;

                var leftA = a.Locator.Left ?? 0;
                var leftB = b.Locator.Left ?? 0;
                return leftA.CompareTo(leftB); // ascending by X
            });

            // Sliding window: try to match the 4-token phrase starting at each sample.
            for (var i = 0; i <= pageSamples.Count - target.Length; i++)
            {
                if (!IsPhraseMismatch(pageSamples, i, target))
                {
                    // Matched — collect all 4 matching samples and return the smallest PointSize.
                    TextTypographySample? smallest = null;
                    for (var j = 0; j < target.Length; j++)
                    {
                        var s = pageSamples[i + j];
                        if (smallest is null || s.PointSize < smallest.PointSize)
                            smallest = s;
                    }

                    return smallest;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the samples starting at <paramref name="startIndex"/>
    /// do NOT match the 4-token <paramref name="target"/> phrase (i.e. the phrase does NOT start here).
    /// Returns <see langword="false"/> when all 4 tokens match (meaning this IS the phrase).
    /// </summary>
    private static bool IsPhraseMismatch(
        List<TextTypographySample> pageSamples,
        int startIndex,
        ReadOnlySpan<string> target)
    {
        var anchorBottom = pageSamples[startIndex].Locator.Bottom ?? 0;

        for (var k = 0; k < target.Length; k++)
        {
            var s = pageSamples[startIndex + k];

            // All tokens must be on the same horizontal line (within SameLineTolerance).
            var sBottom = s.Locator.Bottom ?? 0;
            if (Math.Abs(sBottom - anchorBottom) > SameLineTolerance)
                return true; // Not a match.

            // Token must be left-to-right (ascending Left) — already guaranteed by sort,
            // but validate that Left is not null.
            if (k > 0 && s.Locator.Left is null)
                return true;

            // Text must match after normalization.
            if (!NormalizeToken(s.Text).Equals(target[k], StringComparison.Ordinal))
                return true;
        }

        return false; // All 4 tokens matched.
    }

    /// <summary>
    /// Strips leading/trailing whitespace, converts to lower-case (InvariantCulture),
    /// then removes Unicode non-spacing marks (accents) via NFD decomposition.
    /// "Límite" → "limite", "FECHA" → "fecha".
    /// </summary>
    private static string NormalizeToken(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        var normalized = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString();
    }
}

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
///     region; the PointSize of the nearest typography sample on the same page near that
///     locator is used for the size check.
///   </item>
///   <item>
///     If no extracted field is available, scan <see cref="StatementModel.TypographySamples"/>
///     for consecutive words on one line (same page, overlapping vertical band) whose
///     accent-stripped lower-cased text forms the phrase "fecha limite de pago".
///   </item>
///   <item>
///     If the field cannot be confidently located, the sub-check is skipped (not a Fail
///     and not an InsufficientData for the whole rule).
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="TypographyExtractionStatus.NotFound"/> (scanned/image PDF — no text layer).</item>
///   <item><see cref="StatementModel.TypographySamples"/> is empty.</item>
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

    // Vertical-band tolerance for "same line" heuristic when scanning for the fecha-límite phrase.
    private const double SameLineTolerance = 3.0;

    /// <inheritdoc />
    public string CheckId => "LAW-TYPO-MINSIZE";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo — Tipografía (puntaje mínimo)";

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

        // -----------------------------------------------------------------------
        // Fecha-límite sub-check (best-effort — skipped on uncertainty).
        // -----------------------------------------------------------------------

        TextTypographySample? fechaOffender = null;
        var fechaSubCheckLocated = false;
        double? fechaMinPt = null;

        // Strategy 1: use the extracted PaymentDueDate field locator when available.
        if (model.PeriodSummary?.PaymentDueDate is { Status: ExtractionStatus.Extracted } paymentField)
        {
            fechaSubCheckLocated = true;
            var labelLocator = paymentField.Locator;

            // Find the typography sample on the same page closest to the label's vertical band.
            // We take all samples on that page within the vertical band (± SameLineTolerance of
            // the locator bottom) and pick the one with the smallest PointSize.
            if (labelLocator.Bottom is not null)
            {
                var labelBottom = labelLocator.Bottom.Value;
                var labelPage = labelLocator.PageNumber;
                double? minPt = null;
                TextTypographySample? candidate = null;

                foreach (var sample in model.TypographySamples)
                {
                    if (sample.PageNumber != labelPage)
                        continue;
                    if (sample.Locator.Bottom is null)
                        continue;

                    var vertDist = Math.Abs(sample.Locator.Bottom.Value - labelBottom);
                    if (vertDist > SameLineTolerance)
                        continue;

                    if (minPt is null || sample.PointSize < minPt)
                    {
                        minPt = sample.PointSize;
                        candidate = sample;
                    }
                }

                if (minPt is not null && candidate is not null)
                {
                    fechaMinPt = minPt;
                    if (minPt < FechaLimiteFloorPt - Epsilon)
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
        // Verdict precedence: body violation > fecha violation > Pass.
        // -----------------------------------------------------------------------

        // Body violation takes highest precedence (most violations likely here).
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

        // Fecha-límite violation.
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

        // Pass — build a summary for auditability.
        var minBodyDisplay = minBodyPt == double.MaxValue ? "n/a" : $"{minBodyPt:F2} pt";
        var fechaStatus = fechaSubCheckLocated
            ? $"fecha-límite sub-check located (min {fechaMinPt:F2} pt — OK)"
            : "fecha-límite sub-check skipped (label not confidently located)";

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"{realWordCount} real-word sample(s) checked; min body size: {minBodyDisplay}; {fechaStatus}."));
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

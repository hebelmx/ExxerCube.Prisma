using System;
using System.Collections.Generic;
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
/// LAW-TYPO-BOLD: Verifies that the Acuerdo-mandated fields are rendered in
/// <em>negrillas</em> (bold) as required by the Acuerdo Anexo — Tipografía.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — pure structural
/// inspection of the PDF text layer via <see cref="TextTypographySample.FontName"/> values
/// produced by PdfPig.
/// </para>
/// <para>
/// <b>Cardinal rule — NEVER false-Fail:</b> when the font-weight signal for a field is
/// <see cref="WeightClass.Indeterminate"/> (subset-embedded/mangled font name with no
/// recognizable weight token) the field is skipped rather than treated as a violation.
/// Only a field with a <em>confident</em> <see cref="WeightClass.NotBold"/> classification
/// triggers a <c>Fail</c>.
/// </para>
/// <para>
/// <b>Bare-family-name rule (anti-false-Fail):</b> a font name whose family is recognized
/// (e.g. bare <c>"Aptos"</c>, <c>"Arial"</c>) but carries NO explicit weight token is
/// classified <see cref="WeightClass.Indeterminate"/>, not <see cref="WeightClass.NotBold"/>.
/// A compliant pipeline may render bold via stroke-width synthesis while retaining the base
/// family name; without glyph-level stroke-width data we cannot distinguish the two cases.
/// Only an <em>explicit non-bold weight token</em> in the font name (e.g. <c>"-Regular"</c>,
/// <c>"-Light"</c>) yields a confident <see cref="WeightClass.NotBold"/>.
/// </para>
/// <para>
/// <b>Proximity join (band + X-proximity, two-column safety):</b> for each mandated field
/// whose <see cref="ExtractedField{T}.Status"/> is <see cref="ExtractionStatus.Extracted"/>
/// the rule locates <see cref="TextTypographySample"/> instances on the same page within a
/// ±<see cref="SameLineTolerance"/> vertical band of the field's
/// <see cref="FieldLocator.Bottom"/> coordinate AND within a horizontal window of the
/// label's <see cref="FieldLocator.Left"/> so that samples in adjacent columns are not
/// mistakenly joined.  When multiple samples fall in that band the field is classified as:
/// <list type="bullet">
///   <item><see cref="WeightClass.Bold"/> if ANY joined sample is bold.</item>
///   <item><see cref="WeightClass.NotBold"/> only if ALL joined samples are confidently NotBold.</item>
///   <item><see cref="WeightClass.Indeterminate"/> otherwise.</item>
/// </list>
/// </para>
/// <para>
/// <b>Coverage quorum:</b> the rule returns <c>Pass</c> only when at least
/// <see cref="QuorumThreshold"/> mandated fields were located <em>and</em> all located
/// fields are <see cref="WeightClass.Bold"/>.  Fewer located fields, or a mix of Bold and
/// Indeterminate with no NotBold, yields <c>InsufficientData</c>.
/// Note: the last-page fiscal block has no extracted locator and is not assessed here
/// (documented abstain — the proximity join cannot reach it).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="TypographyExtractionStatus.NotFound"/> (scanned/image PDF — no text layer).</item>
///   <item><see cref="StatementModel.TypographySamples"/> is empty.</item>
///   <item><see cref="StatementModel.PeriodSummary"/> is <see langword="null"/>.</item>
///   <item>Fewer than <see cref="QuorumThreshold"/> mandated fields could be located.</item>
///   <item>All located fields are Indeterminate (no confident bold or not-bold signal).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MandatedBoldFieldsRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Vertical-band tolerance for "same line" heuristic.
    // Aligned to the extractor's Y-band tolerance (~5 pt) to avoid missing value glyphs.
    private const double SameLineTolerance = 5.0;

    // Horizontal window for the proximity join (two-column safety).
    private const double HorizLeftSlack = 5.0;
    private const double HorizRightLimit = 300.0;

    // Minimum number of mandated fields that must be located (Bold or NotBold) before
    // the rule can return Pass.  Fewer → InsufficientData (quorum not met).
    private const int QuorumThreshold = 3;

    // -----------------------------------------------------------------------
    // IVecValidationRule
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public string CheckId => "LAW-TYPO-BOLD";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo — Tipografía (negrillas)";

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

        // InsufficientData: no model, no text layer, or no PeriodSummary.
        if (model is null
            || model.TypographyExtractionStatus == TypographyExtractionStatus.NotFound
            || model.TypographySamples.Count == 0
            || model.PeriodSummary is null)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No typography or period-summary data extracted from the PDF text layer."));
        }

        var summary = model.PeriodSummary;
        var samples = model.TypographySamples;

        // Enumerate each mandated field (label + ExtractedField accessor).
        // The last-page fiscal block has no extracted locator — documented abstain.
        var mandatedFields = BuildMandatedFields(summary);

        // Per-field tallies.
        var boldCount = 0;
        var notBoldCount = 0;
        var indeterminateCount = 0;
        var notLocatedCount = 0;

        // First offender (NotBold) for the Fail finding.
        string? offenderLabel = null;
        string? offenderFontNames = null;
        FieldLocator? offenderLocator = null;

        foreach (var (label, fieldLocator, fieldStatus) in mandatedFields)
        {
            // Skip fields that were not successfully extracted.
            if (fieldStatus != ExtractionStatus.Extracted)
            {
                notLocatedCount++;
                continue;
            }

            // Bottom coordinate is required for the proximity join.
            if (fieldLocator.Bottom is null)
            {
                notLocatedCount++;
                continue;
            }

            var fieldWeight = ClassifyFieldWeight(samples, fieldLocator, out var joinedFontNames);

            switch (fieldWeight)
            {
                case WeightClass.Bold:
                    boldCount++;
                    break;

                case WeightClass.NotBold:
                    notBoldCount++;
                    if (offenderLabel is null)
                    {
                        offenderLabel = label;
                        offenderFontNames = joinedFontNames;
                        offenderLocator = fieldLocator;
                    }

                    break;

                case WeightClass.Indeterminate:
                    indeterminateCount++;
                    break;

                default:
                    // WeightClass.NotLocated — no samples in band.
                    notLocatedCount++;
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Verdict
        // -----------------------------------------------------------------------

        // At least one mandated field is confidently NOT bold → Fail (highest precedence).
        if (notBoldCount > 0)
        {
            var detail = $"Field '{offenderLabel}' is not bold (font(s): {offenderFontNames}). " +
                         $"Tally — bold: {boldCount}, not-bold: {notBoldCount}, " +
                         $"indeterminate: {indeterminateCount}, not-located: {notLocatedCount}. " +
                         "Note: last-page fiscal block is not locator-addressable and was not assessed.";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: "negrillas (bold)",
                    observed: detail,
                    locator: offenderLocator ?? FieldLocator.PageHint(1)));
        }

        // Pass requires quorum: at least QuorumThreshold fields located AND all located are Bold.
        // (notBoldCount == 0 here — Fail already handled above.)
        if (boldCount >= QuorumThreshold)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Mandated-bold check passed — bold: {boldCount}, " +
                               $"indeterminate (skipped): {indeterminateCount}, " +
                               $"not-located/not-extracted: {notLocatedCount}. " +
                               "Note: last-page fiscal block is not locator-addressable and was not assessed."));
        }

        // Quorum not met, or all located were Indeterminate → InsufficientData.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: $"Quorum not met or no confident bold signal — " +
                        $"bold: {boldCount} (need ≥{QuorumThreshold}), " +
                        $"indeterminate: {indeterminateCount}, " +
                        $"not-located/not-extracted: {notLocatedCount}. " +
                        "Note: last-page fiscal block is not locator-addressable and was not assessed."));
    }

    // -----------------------------------------------------------------------
    // Field-level weight classification via proximity join
    // -----------------------------------------------------------------------

    /// <summary>
    /// Joins typography samples on the same page, within
    /// ±<see cref="SameLineTolerance"/> of the field's <paramref name="fieldLocator"/> Bottom,
    /// AND within the horizontal window of the field's Left (when available),
    /// then classifies the aggregate weight.
    /// </summary>
    /// <param name="samples">All typography samples for the document.</param>
    /// <param name="fieldLocator">Locator of the mandated field. <c>Bottom</c> must not be null.</param>
    /// <param name="joinedFontNames">
    /// Comma-separated font names of samples that contributed to a NotBold verdict (for reporting).
    /// </param>
    /// <returns>
    /// <see cref="WeightClass.Bold"/> if ANY joined sample is bold;
    /// <see cref="WeightClass.NotBold"/> if ALL joined samples are confidently NotBold;
    /// <see cref="WeightClass.Indeterminate"/> if at least one is indeterminate and none are bold;
    /// <see cref="WeightClass.NotLocated"/> if no samples fell in the band.
    /// </returns>
    private static WeightClass ClassifyFieldWeight(
        IReadOnlyList<TextTypographySample> samples,
        FieldLocator fieldLocator,
        out string joinedFontNames)
    {
        joinedFontNames = string.Empty;

        var labelBottom = fieldLocator.Bottom!.Value;
        var labelPage = fieldLocator.PageNumber;
        var labelLeft = fieldLocator.Left; // may be null

        // Collect samples in the vertical + horizontal band.
        var hasBold = false;
        var hasIndeterminate = false;
        var notBoldFonts = new List<string>();

        foreach (var sample in samples)
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

            var weight = ClassifyWeight(sample.FontName);

            switch (weight)
            {
                case WeightClass.Bold:
                    hasBold = true;
                    break;
                case WeightClass.NotBold:
                    notBoldFonts.Add(sample.FontName);
                    break;
                case WeightClass.Indeterminate:
                    hasIndeterminate = true;
                    break;
            }
        }

        if (!hasBold && notBoldFonts.Count == 0 && !hasIndeterminate)
            return WeightClass.NotLocated;

        // ANY bold → field is bold (avoids false-Fail on mixed bold/non-bold on the same line).
        if (hasBold)
            return WeightClass.Bold;

        // Indeterminate present (and no bold) → field weight is indeterminate (safe default).
        if (hasIndeterminate)
            return WeightClass.Indeterminate;

        // All samples on the band are confidently NotBold.
        joinedFontNames = string.Join(", ", notBoldFonts);
        return WeightClass.NotBold;
    }

    // -----------------------------------------------------------------------
    // Weight tri-state classifier (biased hard toward Indeterminate)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Bold weight tokens (OrdinalIgnoreCase).
    /// Semibold, Black, and Heavy are treated as bold to avoid false-Fail.
    /// Matched as substrings anywhere in the font name.
    /// </summary>
    private static readonly string[] s_boldTokens =
        ["bold", "black", "heavy", "semibold"];

    /// <summary>
    /// Confident non-bold weight tokens (OrdinalIgnoreCase).
    /// These are matched only when preceded by a <c>-</c> or <c> </c> delimiter, so that
    /// "TimesNewRoman" (family name containing "roman") is NOT classified as NotBold, while
    /// "Times-Roman" and "Aptos-Regular" are.  This avoids false-Fail on bare family names.
    /// </summary>
    private static readonly string[] s_notBoldTokens =
        ["regular", "light", "thin", "book", "roman", "medium"];

    /// <summary>
    /// Classifies the weight signal carried by a raw PDF embedded font name.
    /// </summary>
    /// <remarks>
    /// <b>Bare-family rule:</b> a recognized family name with NO weight token (e.g. bare
    /// <c>"Aptos"</c> or <c>"Arial"</c>) is classified <see cref="WeightClass.Indeterminate"/>,
    /// NOT <see cref="WeightClass.NotBold"/>.  A compliant pipeline may synthesize bold via
    /// stroke-width while retaining the base family name; without glyph-level stroke-width data
    /// we cannot distinguish the two cases.  Only an explicit non-bold token such as
    /// <c>"-Regular"</c> or <c>"-Light"</c> yields a confident <see cref="WeightClass.NotBold"/>.
    /// </remarks>
    /// <returns>
    /// <see cref="WeightClass.Bold"/> — font is confidently bold (contains a bold-weight token);
    /// <see cref="WeightClass.NotBold"/> — font carries an explicit non-bold weight token
    ///     (e.g. <c>"Aptos-Regular"</c>, <c>"Arial-Light"</c>);
    /// <see cref="WeightClass.Indeterminate"/> — signal is ambiguous: bare family name with no
    ///     weight token, mangled/unknown name, or empty string.
    /// </returns>
    internal static WeightClass ClassifyWeight(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
            return WeightClass.Indeterminate;

        // Check bold tokens first (any match → Bold, regardless of other tokens).
        foreach (var boldToken in s_boldTokens)
        {
            if (fontName.Contains(boldToken, StringComparison.OrdinalIgnoreCase))
                return WeightClass.Bold;
        }

        // Check explicit non-bold tokens.
        // A bare family name (no token) does NOT fall here — it falls through to Indeterminate.
        // Tokens are matched only when preceded by '-' or ' ' to avoid false-Fail on family names
        // that happen to contain the token string (e.g. "TimesNewRoman" contains "roman" but is
        // not a weight designator; "Times-Roman" is).
        foreach (var notBoldToken in s_notBoldTokens)
        {
            if (ContainsDelimitedToken(fontName, notBoldToken))
                return WeightClass.NotBold;
        }

        // No weight signal recognized (includes bare family names and mangled names).
        // Conservatively Indeterminate — never false-Fail.
        return WeightClass.Indeterminate;
    }

    // -----------------------------------------------------------------------
    // Token-match helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="token"/> appears in
    /// <paramref name="source"/> preceded by a <c>-</c> or <c> </c> delimiter,
    /// case-insensitively.  This prevents false-Fail on family names that happen to
    /// contain a token substring (e.g. "TimesNewRoman" contains "roman" but is not a
    /// weight designator; "Times-Roman" is preceded by <c>-</c> and matches correctly).
    /// </summary>
    private static bool ContainsDelimitedToken(string source, string token)
    {
        // Search case-insensitively.
        var idx = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            // Accept only when the token is preceded by a recognised delimiter.
            // A token at position 0 is never a valid weight suffix (it would be the family name).
            if (idx > 0 && source[idx - 1] is '-' or ' ')
                return true;

            // Advance search past this occurrence.
            idx = source.IndexOf(token, idx + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // Mandated-field enumeration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the enumerable of (human-readable label, locator, extraction status) tuples
    /// for every Acuerdo-mandated bold field that has an extracted locator.
    /// The last-page fiscal block has no locator and is intentionally excluded per spec
    /// (documented abstain — not locator-addressable via proximity join).
    /// </summary>
    private static IEnumerable<(string Label, FieldLocator Locator, ExtractionStatus Status)>
        BuildMandatedFields(PeriodSummary summary)
    {
        yield return ("fecha límite de pago",
            summary.PaymentDueDate.Locator,
            summary.PaymentDueDate.Status);

        yield return ("pago para no generar intereses",
            summary.PagoParaNoGenerarIntereses.Locator,
            summary.PagoParaNoGenerarIntereses.Status);

        yield return ("pago mínimo + compras a meses",
            summary.PagoMinimoMasMeses.Locator,
            summary.PagoMinimoMasMeses.Status);

        yield return ("adeudo del periodo anterior",
            summary.AdeudoPeriodoAnterior.Locator,
            summary.AdeudoPeriodoAnterior.Status);

        yield return ("saldo deudor total",
            summary.SaldoDeudorTotal.Locator,
            summary.SaldoDeudorTotal.Status);

        yield return ("total cargos",
            summary.TotalCargos.Locator,
            summary.TotalCargos.Status);

        yield return ("total abonos",
            summary.TotalAbonos.Locator,
            summary.TotalAbonos.Status);

        yield return ("tasa ordinaria",
            summary.Tasa.Locator,
            summary.Tasa.Status);

        yield return ("CAT",
            summary.Cat.Locator,
            summary.Cat.Status);
    }
}

// ---------------------------------------------------------------------------
// Weight tri-state enum (file-local — stays inside the Visual project)
// ---------------------------------------------------------------------------

/// <summary>
/// Three-state font-weight classifier.
/// Biased hard toward <see cref="Indeterminate"/> to prevent false-Fail.
/// </summary>
internal enum WeightClass
{
    /// <summary>Font is confidently bold (contains a bold-weight token).</summary>
    Bold,

    /// <summary>
    /// Font is confidently non-bold (carries an explicit non-bold weight token such as
    /// "-Regular", "-Light", "-Thin", "-Book", "-Roman", or "-Medium").
    /// A bare family name with no weight token is <see cref="Indeterminate"/>, not NotBold.
    /// Only a <c>NotBold</c> field that is mandated to be bold yields a <c>Fail</c>.
    /// </summary>
    NotBold,

    /// <summary>
    /// Font-weight signal is ambiguous: bare family name with no weight token, mangled/unknown
    /// name, or empty string.  Such fields are skipped — never treated as a violation.
    /// </summary>
    Indeterminate,

    /// <summary>
    /// No typography samples fell in the field's vertical band; the field cannot be assessed.
    /// Treated identically to <see cref="Indeterminate"/> for verdict purposes.
    /// </summary>
    NotLocated,
}

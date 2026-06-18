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
/// <b>Proximity join:</b> for each mandated field whose
/// <see cref="ExtractedField{T}.Status"/> is <see cref="ExtractionStatus.Extracted"/>
/// the rule locates <see cref="TextTypographySample"/> instances on the same page within a
/// ±<see cref="SameLineTolerance"/> vertical band of the field's
/// <see cref="FieldLocator.Bottom"/> coordinate.  When multiple samples fall in that band
/// the field is classified as:
/// <list type="bullet">
///   <item><see cref="WeightClass.Bold"/> if ANY joined sample is bold.</item>
///   <item><see cref="WeightClass.NotBold"/> only if ALL joined samples are confidently NotBold.</item>
///   <item><see cref="WeightClass.Indeterminate"/> otherwise.</item>
/// </list>
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="TypographyExtractionStatus.NotFound"/> (scanned/image PDF — no text layer).</item>
///   <item><see cref="StatementModel.TypographySamples"/> is empty.</item>
///   <item><see cref="StatementModel.PeriodSummary"/> is <see langword="null"/>.</item>
///   <item>No mandated field could be located (all NotExtracted or all Indeterminate).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MandatedBoldFieldsRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Vertical-band tolerance for "same line" heuristic (matches 12.1's value).
    private const double SameLineTolerance = 3.0;

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
        // The last-page fiscal block has no extracted locator — skip it per spec.
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

        // At least one mandated field is confidently NOT bold → Fail.
        if (notBoldCount > 0)
        {
            var detail = $"Field '{offenderLabel}' is not bold (font(s): {offenderFontNames}). " +
                         $"Tally — bold: {boldCount}, not-bold: {notBoldCount}, " +
                         $"indeterminate: {indeterminateCount}, not-located: {notLocatedCount}.";

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

        // At least one mandated field was located and determinately bold → Pass.
        if (boldCount > 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Mandated-bold check passed — bold: {boldCount}, " +
                               $"indeterminate (skipped): {indeterminateCount}, " +
                               $"not-located/not-extracted: {notLocatedCount}."));
        }

        // No mandated field could be determinately classified (all indeterminate or not-located).
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: $"No mandated-bold field could be confidently located or classified — " +
                        $"indeterminate: {indeterminateCount}, not-located/not-extracted: {notLocatedCount}."));
    }

    // -----------------------------------------------------------------------
    // Field-level weight classification via proximity join
    // -----------------------------------------------------------------------

    /// <summary>
    /// Joins typography samples on the same page and within
    /// ±<see cref="SameLineTolerance"/> of the field's <paramref name="fieldLocator"/> Bottom,
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

        // Collect samples in the vertical band.
        var hasBold = false;
        var hasIndeterminate = false;
        var notBoldFonts = new List<string>();

        foreach (var sample in samples)
        {
            if (sample.PageNumber != labelPage)
                continue;
            if (sample.Locator.Bottom is null)
                continue;

            var vertDist = Math.Abs(sample.Locator.Bottom.Value - labelBottom);
            if (vertDist > SameLineTolerance)
                continue;

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
    /// Known clean family names (subset-prefix stripped, style-suffix stripped) that
    /// are confidently non-bold when no bold weight token is present.
    /// </summary>
    private static readonly HashSet<string> s_knownFamilies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Aptos",
            "Arial",
            "Helvetica",
            "Calibri",
            "Times",
            "TimesNewRoman",
            "Verdana",
            "Tahoma",
        };

    /// <summary>
    /// Style suffixes stripped from embedded font names when normalizing to a family name.
    /// Ordered longest-first so that "-BoldItalic" is tried before "-Bold" / "-Italic".
    /// </summary>
    private static readonly string[] s_styleSuffixes =
        ["-BoldItalic", "-Bold", "-Italic", "-Light", "-SemiBold", "-Medium", "-Regular", "-Thin", "-Book", "-Roman"];

    /// <summary>
    /// Bold weight tokens (OrdinalIgnoreCase).
    /// Semibold, Black, and Heavy are treated as bold to avoid false-Fail.
    /// </summary>
    private static readonly string[] s_boldTokens =
        ["bold", "black", "heavy", "semibold"];

    /// <summary>
    /// Confident non-bold weight tokens (OrdinalIgnoreCase).
    /// If any of these appear in the raw font name (and no bold token does), the font is NotBold.
    /// </summary>
    private static readonly string[] s_notBoldTokens =
        ["regular", "light", "thin", "book", "roman", "medium"];

    /// <summary>
    /// Classifies the weight signal carried by a raw PDF embedded font name.
    /// </summary>
    /// <returns>
    /// <see cref="WeightClass.Bold"/> — font is confidently bold (contains a bold-weight token);
    /// <see cref="WeightClass.NotBold"/> — font is confidently non-bold (known family with clear
    /// non-bold weight token, or known family whose stripped name matches without any bold token);
    /// <see cref="WeightClass.Indeterminate"/> — signal is ambiguous or font name is mangled.
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
        foreach (var notBoldToken in s_notBoldTokens)
        {
            if (fontName.Contains(notBoldToken, StringComparison.OrdinalIgnoreCase))
                return WeightClass.NotBold;
        }

        // Normalize family (strip subset prefix + style suffix) and test against known families.
        var family = NormalizeFamily(fontName);
        if (s_knownFamilies.Contains(family))
            return WeightClass.NotBold;

        // No weight signal recognized — conservatively Indeterminate.
        return WeightClass.Indeterminate;
    }

    // -----------------------------------------------------------------------
    // Font name normalization (mirrors Cl35FontComplianceRule logic)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Strips the 6-uppercase-char '+' subset prefix and known style suffixes from a raw PDF
    /// font name to recover the bare family name.
    /// </summary>
    private static string NormalizeFamily(string fontName)
    {
        var name = fontName;

        // Strip 6-uppercase-letter + '+' subset prefix (e.g. "ABCDEF+Aptos-Bold" → "Aptos-Bold").
        if (name.Length > 7 && name[6] == '+' && IsUpperAlpha(name.AsSpan(0, 6)))
            name = name[7..];

        // Strip known style suffixes (longest first).
        foreach (var suffix in s_styleSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name;
    }

    private static bool IsUpperAlpha(ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (c is < 'A' or > 'Z')
                return false;
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Mandated-field enumeration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the enumerable of (human-readable label, locator, extraction status) tuples
    /// for every Acuerdo-mandated bold field that has an extracted locator.
    /// The last-page fiscal block has no locator and is intentionally excluded per spec.
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
    /// Font is confidently non-bold (recognized non-bold family / weight token).
    /// Only a <c>NotBold</c> field that is mandated to be bold yields a <c>Fail</c>.
    /// </summary>
    NotBold,

    /// <summary>
    /// Font-weight signal is ambiguous (mangled/unknown name with no weight token).
    /// Such fields are skipped — never treated as a violation.
    /// </summary>
    Indeterminate,

    /// <summary>
    /// No typography samples fell in the field's vertical band; the field cannot be assessed.
    /// Treated identically to <see cref="Indeterminate"/> for verdict purposes.
    /// </summary>
    NotLocated,
}

using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// CL-35: Verifies that every embedded font in the statement PDF belongs to the
/// required font family (default: <c>"Aptos"</c>, overridable via
/// <c>ValidationConstants.RequiredFontFamily</c> in the reference bundle).
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — pure structural
/// inspection of the PDF text layer; no ML inference required.
/// </para>
/// <para>
/// <b>Font normalization:</b>
/// <list type="bullet">
///   <item>6-uppercase-character subset prefix followed by '+' is stripped (e.g. "ABCDEF+Aptos-Bold" → "Aptos-Bold").</item>
///   <item>Style suffixes ("-Bold", "-Italic", "-BoldItalic", etc.) are stripped to recover the family name.</item>
///   <item>Comparison against the required family is <see cref="StringComparison.OrdinalIgnoreCase"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="FontExtractionStatus.NotFound"/> (scanned/image PDF — no text layer).</item>
///   <item><see cref="StatementModel.FontRuns"/> is empty.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl35FontComplianceRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-35";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo — Tipografía";

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
            || model.FontExtractionStatus == FontExtractionStatus.NotFound
            || model.FontRuns.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No font data extracted from the PDF text layer."));
        }

        // Required family from bundle, falling back to the corporate default.
        var requiredFamily = ctx.Bundle.ValidationConstants?.RequiredFontFamily ?? "Aptos";

        // Scan runs for the first non-compliant entry, counting all violations.
        FontUsage? offender = null;
        var offenderCount = 0;
        var evaluableRuns = 0;

        foreach (var run in model.FontRuns)
        {
            var family = NormalizeFamily(run.FontName);

            // Type0/CID composite fonts: the normalized name is empty (e.g. the CID
            // resource token has no human-readable family name after prefix stripping).
            // We cannot evaluate THIS run — skip it. We must NOT abstain on the whole
            // rule here: an empty-family run appearing AFTER a confirmed non-Aptos
            // offender would otherwise discard the genuine Fail and produce a false-PASS.
            // Abstain (below) only when EVERY run is unevaluable.
            if (string.IsNullOrEmpty(family))
                continue;

            evaluableRuns++;

            // Prefix match with a word-boundary guard so weight/optical-size variants of
            // the required family ("Aptos-Black", "Aptos Display", "Aptos-Heavy") are
            // accepted, while an unrelated family that merely shares the leading letters
            // ("AptosCustom", "Aptosish") is NOT silently accepted as compliant.
            if (!IsCompliantFamily(family, requiredFamily))
            {
                offender ??= run;
                offenderCount++;
            }
        }

        if (offender is not null)
        {
            var normalizedOffender = NormalizeFamily(offender.FontName);
            var detail = offenderCount > 1
                ? $"Font '{offender.FontName}' (family: '{normalizedOffender}') is not '{requiredFamily}'. {offenderCount} non-compliant run(s) found."
                : $"Font '{offender.FontName}' (family: '{normalizedOffender}') is not '{requiredFamily}'.";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: requiredFamily,
                    observed: detail,
                    locator: offender.Locator));
        }

        // No offender. If at least one run was evaluable, every evaluable run was compliant → Pass.
        // If NO run was evaluable (every run was a Type0/CID empty-family token) we cannot
        // determine compliance → InsufficientData (abstain rather than false-Pass).
        if (evaluableRuns == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "All font runs are Type0/CID composite fonts with no resolvable family name; compliance cannot be determined."));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"All {evaluableRuns} evaluable font run(s) use '{requiredFamily}'."));
    }

    /// <summary>
    /// True when <paramref name="family"/> is the required family or one of its
    /// weight/optical-size variants. Prefix match plus a boundary guard: the character
    /// immediately after the required-family prefix must be a separator
    /// (<c>-</c>, space, <c>_</c>) or a digit, OR the prefix must be the whole string.
    /// This accepts "Aptos", "Aptos-Black", "Aptos Display", "Aptos-Heavy" but rejects
    /// an unrelated family such as "AptosCustom" that merely shares the leading letters.
    /// </summary>
    private static bool IsCompliantFamily(string family, string requiredFamily)
    {
        if (!family.StartsWith(requiredFamily, StringComparison.OrdinalIgnoreCase))
            return false;

        if (family.Length == requiredFamily.Length)
            return true;

        var next = family[requiredFamily.Length];
        return next is '-' or ' ' or '_' || char.IsDigit(next);
    }

    // -----------------------------------------------------------------------
    // Font name normalization (mirrors extractor logic)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Style suffixes stripped from embedded font names when normalizing to a family name.
    /// Ordered longest-first so that "-BoldItalic" is tried before "-Bold" / "-Italic".
    /// </summary>
    private static readonly string[] s_styleSuffixes =
        ["-BoldItalic", "-Bold", "-Italic", "-Light", "-SemiBold", "-Medium", "-Regular", "-Thin"];

    /// <summary>
    /// Strips the subset prefix (e.g. "ABCDEF+") and style suffixes from a raw PDF font name.
    /// </summary>
    private static string NormalizeFamily(string fontName)
    {
        var name = fontName;

        // Strip 6-uppercase-letter + '+' subset prefix.
        if (name.Length > 7 && name[6] == '+' && IsUpperAlpha(name.AsSpan(0, 6)))
            name = name[7..];

        // Strip known style suffixes.
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
}

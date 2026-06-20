using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// CL-48: Verifies that the statement PDF contains no blank pages and no intra-page
/// vertical gaps exceeding 2 cm (≈ 56.7 PDF points).
/// </summary>
/// <remarks>
/// <para>
/// Two sub-conditions are evaluated (both contribute to a single FAIL finding):
/// <list type="bullet">
///   <item>
///     <b>Whole-blank page:</b> <see cref="PageInspectionFacts.HasContent"/> is
///     <see langword="false"/> AND <see cref="PageInspectionFacts.ImageCount"/> == 0.
///     Whitespace-only / invisible-glyph tokens are excluded by the extractor before
///     <c>HasContent</c> is set (Story S11), so an invisible-glyph-only page is
///     correctly reported as blank.
///   </item>
///   <item>
///     <b>Large intra-page gap:</b> <see cref="PageInspectionFacts.MaxVerticalGapPoints"/>
///     exceeds <see cref="BlankGapThresholdPoints"/> (56.7 pt ≈ 2 cm).
///     This catches pages that are not wholly blank but contain a forbidden empty band.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — structural inspection only.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="StatementModel.Pages"/> is empty (per-page extraction did not run).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl48BlankPageRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// Maximum permitted intra-page vertical gap in PDF points.
    /// Derived from the DOF numeral "sin espacio en blanco mayor a 2 cm":
    ///   2 cm × 28.35 pt/cm = 56.7 pt.
    /// </summary>
    private const double BlankGapThresholdPoints = 56.7;

    /// <inheritdoc />
    public string CheckId => "CL-48";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo — sin espacio en blanco mayor a 2 cm";

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

        if (model is null || model.Pages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No per-page inspection data available — extraction stage did not run."));
        }

        // Sub-condition 1: whole-blank pages (no text content, no images).
        var blankPages = model.Pages
            .Where(p => !p.HasContent && p.ImageCount == 0)
            .ToList();

        // Sub-condition 2: pages with an intra-page gap exceeding the 2 cm threshold.
        var gapPages = model.Pages
            .Where(p => p.MaxVerticalGapPoints > BlankGapThresholdPoints)
            .ToList();

        if (blankPages.Count == 0 && gapPages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"No blank pages or excessive vertical gaps found across {model.Pages.Count} page(s)."));
        }

        // Build a descriptive failure message covering whichever condition(s) fired.
        var parts = new List<string>();

        if (blankPages.Count > 0)
        {
            var nums = string.Join(", ", blankPages.Select(p => p.PageNumber));
            parts.Add($"{blankPages.Count} blank page(s) on page(s) {nums}");
        }

        if (gapPages.Count > 0)
        {
            var gapDetail = string.Join("; ",
                gapPages.Select(p => $"page {p.PageNumber} ({p.MaxVerticalGapPoints:F1} pt > {BlankGapThresholdPoints} pt)"));
            parts.Add($"intra-page vertical gap exceeds 2 cm: {gapDetail}");
        }

        var firstViolation = blankPages.Count > 0 ? blankPages[0] : gapPages[0];

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "No blank pages and no intra-page vertical gap > 2 cm (56.7 pt)",
                observed: string.Join("; ", parts) + ".",
                locator: firstViolation.Locator));
    }
}

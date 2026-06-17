using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-46: Mandatory-legends verification — each legend in the bundle's
/// <c>mandatoryLegends</c> section with <c>required == true</c> must be present
/// in the normalized full text of the statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Matching strategy:</b> both the legend text from the bundle and the statement
/// <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> are normalized
/// via <see cref="TextNormalizer.Normalize"/> (upper-case, accent-stripped,
/// whitespace-collapsed) before a substring check.  This makes the comparison
/// accent- and case-insensitive regardless of the <c>matchMode</c> field in the bundle
/// (the current implementation treats all required legends as a normalized contains check;
/// <c>matchMode</c> may drive stricter modes in a future story).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>No <see cref="Domain.Extraction.StatementModel"/> or its
///         <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is empty
///         (extraction stage has not run or the PDF has no text layer).</item>
///   <item>The bundle's <c>mandatoryLegends</c> section is null or contains no entries.</item>
/// </list>
/// </para>
/// <para>
/// When some required legends are missing the rule emits a <b>Critical Fail</b> listing
/// up to 5 missing legend IDs; all others present → <b>Pass</b>.
/// Non-required legends (<c>required == false</c>) are skipped silently.
/// </para>
/// </remarks>
internal sealed class Cl46MandatoryLegendsRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-46";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Gate 1: statement text must be present.
        var model = ctx.StatementModel;
        if (model is null || string.IsNullOrEmpty(model.NormalizedFullText))
            return InsufficientData("StatementModel or NormalizedFullText is not populated.");

        // Gate 2: bundle must have a mandatory-legends section.
        var legends = ctx.Bundle.MandatoryLegends;
        if (legends is null || legends.Count == 0)
            return InsufficientData("Bundle contains no mandatoryLegends reference data.");

        var statementText = model.NormalizedFullText;
        var missing = new List<string>();

        foreach (var legend in legends)
        {
            // Skip non-required entries (null defaults to required = true per schema convention).
            var isRequired = legend.Required ?? true;
            if (!isRequired)
                continue;

            var normalizedLegend = TextNormalizer.Normalize(legend.Text);
            if (string.IsNullOrEmpty(normalizedLegend))
                continue;

            if (!statementText.Contains(normalizedLegend, System.StringComparison.Ordinal))
                missing.Add(legend.LegendId);
        }

        if (missing.Count == 0)
        {
            // Count required legends for the observed message.
            var requiredCount = 0;
            foreach (var legend in legends)
            {
                if (legend.Required ?? true)
                    requiredCount++;
            }

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {requiredCount} required legend(s) found in statement text.",
                    toleranceApplied: null,
                    locator: FieldLocator.PageHint(1)));
        }

        var idList = string.Join(", ", missing.Count <= 5 ? missing : missing.GetRange(0, 5));
        var suffix = missing.Count > 5 ? $" … (+{missing.Count - 5} more)" : string.Empty;

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "All required legends present in statement text.",
                observed: $"{missing.Count} required legend(s) missing: {idList}{suffix}",
                toleranceApplied: null,
                locator: FieldLocator.PageHint(1)));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

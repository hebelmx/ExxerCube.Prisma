using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-32: Mandatory section "COMPARA TU TARJETA" must be present in the statement.
/// </summary>
/// <remarks>
/// <para>
/// This is a pure text-presence check that does not depend on reference-data from the bundle.
/// The section heading "COMPARA TU TARJETA" is a regulatory requirement on all VEC statements
/// and is therefore always checked — the rule degrades to <c>InsufficientData</c> only when
/// the statement's full text has not been extracted, never because the bundle lacks data.
/// </para>
/// <para>
/// <b>Matching:</b> the search string <c>"COMPARA TU TARJETA"</c> is normalized via
/// <see cref="TextNormalizer.Normalize"/> (upper-case, accent-stripped, whitespace-collapsed)
/// and then searched via <see cref="string.Contains(string,System.StringComparison)"/> in
/// <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/>, which was normalized
/// with the identical algorithm during extraction.  This makes the check accent- and
/// case-insensitive.
/// </para>
/// <para>
/// <b>InsufficientData path:</b> only when <see cref="Domain.Extraction.StatementModel"/>
/// is null or <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is empty
/// (the extraction stage has not run or the PDF has no text layer).
/// </para>
/// </remarks>
internal sealed class Cl32ComparaTuTarjetaRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// Normalized form of the mandatory section heading.
    /// Pre-computed so that the normalization path is not repeated per call.
    /// </summary>
    private static readonly string NormalizedSectionHeading =
        TextNormalizer.Normalize("COMPARA TU TARJETA");

    /// <inheritdoc />
    public string CheckId => "CL-32";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §11";

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
        if (model is null || string.IsNullOrEmpty(model.NormalizedFullText))
            return InsufficientData("StatementModel or NormalizedFullText is not populated.");

        var locator = FieldLocator.PageHint(1);

        if (model.NormalizedFullText.Contains(NormalizedSectionHeading, System.StringComparison.Ordinal))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Section heading '{NormalizedSectionHeading}' found in statement text.",
                    toleranceApplied: null,
                    locator: locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"Section heading '{NormalizedSectionHeading}' present in statement.",
                observed: "Section heading not found in normalized statement text.",
                toleranceApplied: null,
                locator: locator));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

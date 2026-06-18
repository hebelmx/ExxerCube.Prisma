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
/// CL-48: Verifies that the statement PDF contains no blank pages.
/// A page is considered blank when it has no textual content (<see cref="PageInspectionFacts.HasContent"/> is false)
/// AND no embedded images (<see cref="PageInspectionFacts.ImageCount"/> == 0).
/// </summary>
/// <remarks>
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

        var blankPages = model.Pages
            .Where(p => !p.HasContent && p.ImageCount == 0)
            .ToList();

        if (blankPages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"No blank pages found across {model.Pages.Count} page(s)."));
        }

        var pageNumbers = string.Join(", ", blankPages.Select(p => p.PageNumber));
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "No blank pages",
                observed: $"{blankPages.Count} blank page(s) found: page(s) {pageNumbers}.",
                locator: blankPages[0].Locator));
    }
}

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
/// CL-33: Verifies that every page of the statement contains at least one embedded image
/// (used as a proxy for logo/header presence).
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — structural inspection only.
/// </para>
/// <para>
/// <b>Image-count proxy:</b> this rule checks only that <see cref="PageInspectionFacts.ImageCount"/> ≥ 1
/// per page.  Image-content matching (escudo / watermark verification) is deferred to Story 5.4.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="StatementModel.Pages"/> is empty (per-page extraction did not run).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl33LogoPresenceRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-33";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

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

        var pagesWithoutImages = model.Pages
            .Where(p => p.ImageCount == 0)
            .ToList();

        if (pagesWithoutImages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {model.Pages.Count} page(s) contain at least one embedded image " +
                              "(image presence only — escudo content matching deferred to v2)."));
        }

        var pageNumbers = string.Join(", ", pagesWithoutImages.Select(p => p.PageNumber));
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Every page contains at least one embedded image",
                observed: $"{pagesWithoutImages.Count} page(s) have no embedded images: page(s) {pageNumbers}.",
                locator: pagesWithoutImages[0].Locator));
    }
}

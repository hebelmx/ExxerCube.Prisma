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
/// CL-34: Verifies that every page of the statement contains the card number extracted
/// from the statement header.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — structural inspection only.
/// </para>
/// <para>
/// <b>Card-number matching:</b> the card number (digits only, spaces stripped) must appear
/// in each page's text (also stripped of spaces).  Matching is case-insensitive on digits.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="StatementModel.Pages"/> is empty (per-page extraction did not run).</item>
///   <item><see cref="StatementModel.CardNumber"/> was not successfully extracted
///     (<see cref="ExtractionStatus.NotExtracted"/>).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl34CardNumberPresenceRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-34";

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

        // Guard: card number must have been extracted.
        if (model.CardNumber.Status == ExtractionStatus.NotExtracted)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "Card number was not extracted from the statement header — cannot verify per-page presence."));
        }

        var cardNumber = model.CardNumber.Value;
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "Extracted card number value is empty — cannot verify per-page presence."));
        }

        var pagesWithoutCardNumber = model.Pages
            .Where(p => !p.ContainsCardNumber)
            .ToList();

        if (pagesWithoutCardNumber.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Card number present on all {model.Pages.Count} page(s)."));
        }

        var pageNumbers = string.Join(", ", pagesWithoutCardNumber.Select(p => p.PageNumber));
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Card number present on every page",
                observed: $"{pagesWithoutCardNumber.Count} page(s) do not contain the card number: page(s) {pageNumbers}.",
                locator: pagesWithoutCardNumber[0].Locator));
    }
}

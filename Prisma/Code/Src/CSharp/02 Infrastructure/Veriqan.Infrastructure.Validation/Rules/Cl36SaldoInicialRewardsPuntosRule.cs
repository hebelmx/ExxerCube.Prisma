using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-36: Validates that the statement's rewards opening balances (points and pesos)
/// equal the prior statement's closing rewards balances.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (FR-7):</b>
/// <c>statement.RewardsOpeningPoints == priorStatement.ClosingBalances.RewardsPointsBalance</c>
/// and
/// <c>statement.RewardsOpeningPesos == priorStatement.ClosingBalances.RewardsPesosBalance</c>
/// each within their respective tolerances (points ±1.00, pesos ±$1.00 MXN).
/// </para>
/// <para>
/// <b>Product gate (scoping decision):</b> if the resolved product has
/// <c>HasRewardsProgram == false</c> (or null) this rule returns
/// <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> with reason
/// "not applicable: product has no rewards program". This avoids false FAILs for
/// non-rewards products such as BSSB, which are the fixtures in use at Story 4.3.
/// The Epic 7 rollup semantics treat InsufficientData for non-applicable checks as
/// informational, not defects.
/// </para>
/// <para>
/// <b>Extraction gate:</b> the statement's rewards opening-balance section is not extracted
/// until a future story (rewards-section extraction). When the product does have a rewards
/// program this rule returns InsufficientData with reason "rewards extraction pending".
/// </para>
/// <para>
/// <note type="todo">TODO(rewards): Replace InsufficientData with real check once
/// <c>PeriodSummary.RewardsOpeningPoints</c> / <c>RewardsOpeningPesos</c> are populated
/// by the rewards-section extractor and a rewards-bearing fixture is available.</note>
/// </para>
/// <para>
/// <b>InsufficientData paths (graceful degradation — NFR-2/6):</b>
/// <list type="bullet">
///   <item>Product has no rewards program → InsufficientData("not applicable…").</item>
///   <item>Product has rewards program but extraction is not yet done → InsufficientData("rewards extraction pending").</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl36SaldoInicialRewardsPuntosRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-36";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §18";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Gate 1: non-rewards products are not applicable — never a false FAIL
        if (ctx.ResolvedProduct.HasRewardsProgram != true)
            return InsufficientData(
                "not applicable: product has no rewards program");

        // Gate 2: rewards-section extraction is not yet implemented.
        // When the future rewards-section extractor populates
        // PeriodSummary.RewardsOpeningPoints and RewardsOpeningPesos,
        // replace this branch with the real comparison against
        // PriorStatement.ClosingBalances.RewardsPointsBalance /
        // RewardsPesosBalance using PointsTolerance / RewardsPesosToleranceMxn.
        return InsufficientData(
            "rewards extraction pending: statement rewards opening balances " +
            "are not yet extracted. CL-36 will emit Pass/Fail once the " +
            "rewards-section extractor is implemented.");
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

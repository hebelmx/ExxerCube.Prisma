using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-37: Validates the points-to-pesos exchange rate printed on the statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (FR-7):</b>
/// <c>RewardsPesos == RewardsPoints × ToleranceConfig.PointsToPesosExchangeRate</c>
/// (default exchange rate: 0.10, i.e. 10 points = $1.00 MXN), within ±1.00 point tolerance.
/// </para>
/// <para>
/// <b>Product gate and extraction gate:</b> same as <see cref="Cl36SaldoInicialRewardsPuntosRule"/>.
/// Non-rewards products → InsufficientData("not applicable: product has no rewards program").
/// Rewards products with no extraction yet → InsufficientData("rewards extraction pending").
/// </para>
/// <para>
/// <note type="todo">TODO(rewards): Replace InsufficientData with real check once
/// <c>PeriodSummary.RewardsPoints</c> and <c>PeriodSummary.RewardsPesos</c> are populated
/// and <c>ToleranceConfig.PointsToPesosExchangeRate</c> (default 0.10) is in the bundle.</note>
/// </para>
/// <para>
/// <b>InsufficientData paths (graceful degradation — NFR-2/6):</b>
/// <list type="bullet">
///   <item>Product has no rewards program → InsufficientData("not applicable…").</item>
///   <item>Product has rewards program but extraction pending → InsufficientData("rewards extraction pending").</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl37TipoCambioRewardsRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-37";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §18";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

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
        // When available, compare:
        //   rewardsPesos == rewardsPoints × (ToleranceConfig.PointsToPesosExchangeRate ?? 0.10m)
        // within PointsTolerance (±1.00).
        return InsufficientData(
            "rewards extraction pending: statement rewards points and pesos " +
            "are not yet extracted. CL-37 will emit Pass/Fail once the " +
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

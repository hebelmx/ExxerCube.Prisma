using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-39: Validates that "Saldo Total Puntos" equals the rewards-points running balance.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (FR-7):</b>
/// <c>SaldoTotalPuntos = SaldoInicial + Generados − Redimidos − Vencidos</c>
/// within ±1.00 points (<c>ToleranceConfig.PointsTolerance</c>).
/// </para>
/// <para>
/// <b>Product gate and extraction gate:</b> same as <see cref="Cl36SaldoInicialRewardsPuntosRule"/>.
/// Non-rewards products → InsufficientData("not applicable: product has no rewards program").
/// Rewards products with no extraction yet → InsufficientData("rewards extraction pending").
/// </para>
/// <para>
/// <note type="todo">TODO(rewards): Replace InsufficientData with real check once
/// <c>PeriodSummary.RewardsSaldoTotal</c>, <c>RewardsGenerados</c>, <c>RewardsRedimidos</c>,
/// and <c>RewardsVencidos</c> are populated by the rewards-section extractor.</note>
/// </para>
/// <para>
/// <b>InsufficientData paths (graceful degradation — NFR-2/6):</b>
/// <list type="bullet">
///   <item>Product has no rewards program → InsufficientData("not applicable…").</item>
///   <item>Product has rewards program but extraction pending → InsufficientData("rewards extraction pending").</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl39SaldoTotalPuntosRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-39";

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
        // When available, compute:
        //   computed = SaldoInicial + Generados − Redimidos − Vencidos
        // and compare to SaldoTotalPuntos within PointsTolerance (±1.00).
        return InsufficientData(
            "rewards extraction pending: statement rewards points detail " +
            "(SaldoInicial, Generados, Redimidos, Vencidos, SaldoTotal) " +
            "is not yet extracted. CL-39 will emit Pass/Fail once the " +
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

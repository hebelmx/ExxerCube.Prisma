using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-24: Validates that "Saldo deudor total" equals the sum of the two NIVEL-DE-USO subtotals:
/// <c>SaldoDeudorTotal = SaldoCargosRegulares + SaldoCargosAMeses</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tolerance (ADR-V3):</b> ±$0.50 MXN from
/// <c>ToleranceConfig.CurrencyToleranceMxn</c>.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>Null <c>VerificationContext.ToleranceConfig</c>.</item>
///   <item><see cref="PeriodSummary.SaldoCargosRegulares"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoCargosAMeses"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoDeudorTotal"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl24SaldoDeudorTotalRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-24";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        if (ctx.ToleranceConfig is null)
            return InsufficientData("ToleranceConfig is absent from the bundle.");

        var tolerance = ctx.ToleranceConfig.CurrencyToleranceMxn ?? 0.50m;

        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        if (ps.SaldoCargosRegulares.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoCargosRegulares is {ps.SaldoCargosRegulares.Status}.");
        if (ps.SaldoCargosAMeses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoCargosAMeses is {ps.SaldoCargosAMeses.Status}.");
        if (ps.SaldoDeudorTotal.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoDeudorTotal is {ps.SaldoDeudorTotal.Status}.");

        var computed = ps.SaldoCargosRegulares.Value + ps.SaldoCargosAMeses.Value;
        var observed = ps.SaldoDeudorTotal.Value;
        var diff = Math.Abs(computed - observed);

        var locator = ps.SaldoDeudorTotal.Locator;

        if (diff <= tolerance)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{observed:F2}",
                    toleranceApplied: tolerance,
                    locator: locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"{computed:F2}",
                observed: $"{observed:F2}",
                toleranceApplied: tolerance,
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

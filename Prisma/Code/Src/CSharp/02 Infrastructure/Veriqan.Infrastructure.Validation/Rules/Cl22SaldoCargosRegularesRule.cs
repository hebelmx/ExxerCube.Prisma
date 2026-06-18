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
/// CL-22: Validates that "Saldo cargos regulares" equals "Pago para no generar intereses"
/// within ±$0.50 MXN.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule:</b> <c>SaldoCargosRegulares == PagoParaNoGenerarIntereses</c> (within tolerance).
/// Both values appear on page 1 of the statement; the equality signals that no MSI capital
/// offsets or other adjustments changed the regular-charges balance.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3):</b> ±$0.50 MXN from
/// <c>ToleranceConfig.CurrencyToleranceMxn</c>.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>Null <c>VerificationContext.ToleranceConfig</c>.</item>
///   <item><see cref="PeriodSummary.SaldoCargosRegulares"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.PagoParaNoGenerarIntereses"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl22SaldoCargosRegularesRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-22";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §13";

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
        if (ps.PagoParaNoGenerarIntereses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagoParaNoGenerarIntereses is {ps.PagoParaNoGenerarIntereses.Status}.");

        var saldo = ps.SaldoCargosRegulares.Value;
        var pago = ps.PagoParaNoGenerarIntereses.Value;
        var diff = Math.Abs(saldo - pago);

        var locator = ps.SaldoCargosRegulares.Locator;

        if (diff <= tolerance)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{saldo:F2}",
                    toleranceApplied: tolerance,
                    locator: locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"{pago:F2}",
                observed: $"{saldo:F2}",
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

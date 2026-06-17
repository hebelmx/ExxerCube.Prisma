using System;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-20: Validates that the RESUMEN field "Pagos y abonos" equals the sum of all Credit
/// (abono) movement amounts extracted from the DESGLOSE DE OPERACIONES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Interpretation:</b> CL-20 interprets "suma de todas las operaciones de cargo y abono
/// que aparecen en el DESGLOSE" as the total of all Credit (abono) movements in the DESGLOSE.
/// While the checklist label mentions both "cargo" and "abono", the reference field
/// <c>PagosYAbonos</c> tracks payments received, which correspond to Credit movements only.
/// Charge movements are separately accounted for in CL-18 and CL-19.
/// </para>
/// <para>
/// <b>Formula:</b>
/// <c>PagosYAbonos ≈ Σ Amount of Credit (abono) movements in DESGLOSE</c>.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3):</b> ±<c>ToleranceConfig.CurrencyToleranceMxn</c> MXN.
/// </para>
/// </remarks>
internal sealed class Cl20PagosYAbonosSumaDesgloseRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-20";

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

        var movStatus = ctx.StatementModel!.MovementsStatus;
        if (movStatus != MovementsExtractionStatus.Extracted || ctx.StatementModel.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {movStatus}).");

        if (ps.PagosYAbonos.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagosYAbonos is {ps.PagosYAbonos.Status}.");

        var sumAbonos = ctx.StatementModel.Movements
            .Where(m => m.Sign == MovementSign.Credit)
            .Sum(m => m.Amount);

        var target = ps.PagosYAbonos.Value;
        var diff = Math.Abs(sumAbonos - target);
        var locator = ps.PagosYAbonos.Locator;

        if (diff <= tolerance)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{sumAbonos:F2}",
                    toleranceApplied: tolerance,
                    locator: locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"{target:F2}",
                observed: $"{sumAbonos:F2}",
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

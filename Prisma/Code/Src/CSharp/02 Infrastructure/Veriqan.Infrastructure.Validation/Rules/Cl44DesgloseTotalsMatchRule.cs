using System;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-44: Validates that the printed "Total cargos" and "Total abonos" rows at the bottom
/// of the DESGLOSE section match the sums of the extracted movement amounts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula:</b>
/// <c>|Σ Charge amounts − TotalCargos| ≤ tolerance</c>  AND
/// <c>|Σ Credit amounts − TotalAbonos| ≤ tolerance</c>.
/// </para>
/// <para>
/// Both sides (cargos and abonos) must be within tolerance; if either deviates the rule fails.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>StatementModel or PeriodSummary is null.</item>
///   <item>DESGLOSE movements not extracted.</item>
///   <item><see cref="Domain.Extraction.PeriodSummary.TotalCargos"/> is not Extracted.</item>
///   <item><see cref="Domain.Extraction.PeriodSummary.TotalAbonos"/> is not Extracted.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl44DesgloseTotalsMatchRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl44DesgloseTotalsMatchRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl44DesgloseTotalsMatchRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-44";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §22";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var resolution = _toleranceProvider.For(CheckId)
            .Resolve(ctx.ToleranceConfig?.CurrencyToleranceMxn);
        var tolerance = resolution.EffectiveValue;

        var model = ctx.StatementModel;
        if (model is null)
            return InsufficientData("StatementModel is not populated.");

        var ps = model.PeriodSummary;
        if (ps is null)
            return InsufficientData("PeriodSummary is not populated.");

        if (model.MovementsStatus != MovementsExtractionStatus.Extracted || model.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {model.MovementsStatus}).");

        if (ps.TotalCargos.Status != ExtractionStatus.Extracted)
            return InsufficientData("TotalCargos not extracted from PDF.");

        if (ps.TotalAbonos.Status != ExtractionStatus.Extracted)
            return InsufficientData("TotalAbonos not extracted from PDF.");

        var sumCargos = model.Movements
            .Where(m => m.Sign == MovementSign.Charge)
            .Sum(m => m.Amount);

        var sumAbonos = model.Movements
            .Where(m => m.Sign == MovementSign.Credit)
            .Sum(m => m.Amount);

        var printedCargos = ps.TotalCargos.Value;
        var printedAbonos = ps.TotalAbonos.Value;

        var cargosOk = Math.Abs(sumCargos - printedCargos) <= tolerance;
        var abonosOk = Math.Abs(sumAbonos - printedAbonos) <= tolerance;

        if (cargosOk && abonosOk)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Cargos {sumCargos:F2}=={printedCargos:F2}, Abonos {sumAbonos:F2}=={printedAbonos:F2}",
                    toleranceApplied: tolerance));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"Cargos={printedCargos:F2};Abonos={printedAbonos:F2}",
                observed: $"SumCargos={sumCargos:F2};SumAbonos={sumAbonos:F2}",
                toleranceApplied: tolerance));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

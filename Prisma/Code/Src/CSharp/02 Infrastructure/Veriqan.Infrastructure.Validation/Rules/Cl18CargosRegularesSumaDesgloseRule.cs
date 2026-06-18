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
/// CL-18: Validates that the RESUMEN field "Cargos regulares (no a meses)" equals the sum
/// of non-MSI charge amounts extracted from the DESGLOSE DE OPERACIONES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula:</b>
/// <c>CargosRegularesNoMeses ≈ Σ Amount of Charge movements where Description ∉ MSI pattern</c>.
/// </para>
/// <para>
/// MSI installment rows are identified by the "NNN de NNN" fragment in the description
/// (e.g. "DON COLCHON CUMBRES 005 de 012") via <see cref="MovementClassifier.IsMsi"/>.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3):</b> resolved via <see cref="ILegalToleranceProvider"/> with the
/// bundle's <c>ToleranceConfig.CurrencyToleranceMxn</c> as the optional override.
/// </para>
/// </remarks>
internal sealed class Cl18CargosRegularesSumaDesgloseRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl18CargosRegularesSumaDesgloseRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl18CargosRegularesSumaDesgloseRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-18";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §7";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var resolution = _toleranceProvider.For(CheckId)
            .Resolve(ctx.ToleranceConfig?.CurrencyToleranceMxn);
        var tolerance = resolution.EffectiveValue;

        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        var movStatus = ctx.StatementModel!.MovementsStatus;
        if (movStatus != MovementsExtractionStatus.Extracted || ctx.StatementModel.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {movStatus}).");

        if (ps.CargosRegularesNoMeses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CargosRegularesNoMeses is {ps.CargosRegularesNoMeses.Status}.");

        var sumNonMsi = ctx.StatementModel.Movements
            .Where(m => m.Sign == MovementSign.Charge && !MovementClassifier.IsMsi(m.Description))
            .Sum(m => m.Amount);

        var target = ps.CargosRegularesNoMeses.Value;
        var diff = Math.Abs(sumNonMsi - target);
        var locator = ps.CargosRegularesNoMeses.Locator;

        if (diff <= tolerance)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{sumNonMsi:F2}",
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
                observed: $"{sumNonMsi:F2}",
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

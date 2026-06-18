using System;
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
/// CL-24: Validates that "Saldo deudor total" equals the sum of the two NIVEL-DE-USO subtotals:
/// <c>SaldoDeudorTotal = SaldoCargosRegulares + SaldoCargosAMeses</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tolerance (ADR-V3):</b> resolved via <see cref="ILegalToleranceProvider"/> with the
/// bundle's <c>ToleranceConfig.CurrencyToleranceMxn</c> as the optional override.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><see cref="PeriodSummary.SaldoCargosRegulares"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoCargosAMeses"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoDeudorTotal"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl24SaldoDeudorTotalRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl24SaldoDeudorTotalRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl24SaldoDeudorTotalRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-24";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §13";

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

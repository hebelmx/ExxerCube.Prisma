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
/// CL-21: Validates the printed "Pago para no generar intereses" against the formula:
/// <c>PagoParaNoGenerarIntereses = AdeudoPeriodoAnterior + CargosRegularesNoMeses
///     + CargosComprasAMesesCapital + MontoIntereses + MontoComisiones
///     + IvaInteresesYComisiones − PagosYAbonos</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tolerance (ADR-V3):</b> resolved via <see cref="ILegalToleranceProvider"/> with the
/// bundle's <c>ToleranceConfig.CurrencyToleranceMxn</c> as the optional override.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>Any required RESUMEN subtotal field is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.PagoParaNoGenerarIntereses"/> is NotExtracted.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl21PagoParaNoGenerarInteresesRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl21PagoParaNoGenerarInteresesRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl21PagoParaNoGenerarInteresesRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-21";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §7";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Resolve tolerance (ADR-V3)
        var resolution = _toleranceProvider.For(CheckId)
            .Resolve(ctx.ToleranceConfig?.CurrencyToleranceMxn);
        var tolerance = resolution.EffectiveValue;

        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        // All RESUMEN subtotal inputs must be extracted
        if (ps.AdeudoPeriodoAnterior.Status != ExtractionStatus.Extracted)
            return InsufficientData($"AdeudoPeriodoAnterior is {ps.AdeudoPeriodoAnterior.Status}.");
        if (ps.CargosRegularesNoMeses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CargosRegularesNoMeses is {ps.CargosRegularesNoMeses.Status}.");
        if (ps.CargosComprasAMesesCapital.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CargosComprasAMesesCapital is {ps.CargosComprasAMesesCapital.Status}.");
        if (ps.MontoIntereses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"MontoIntereses is {ps.MontoIntereses.Status}.");
        if (ps.MontoComisiones.Status != ExtractionStatus.Extracted)
            return InsufficientData($"MontoComisiones is {ps.MontoComisiones.Status}.");
        if (ps.IvaInteresesYComisiones.Status != ExtractionStatus.Extracted)
            return InsufficientData($"IvaInteresesYComisiones is {ps.IvaInteresesYComisiones.Status}.");
        if (ps.PagosYAbonos.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagosYAbonos is {ps.PagosYAbonos.Status}.");

        // Target field to compare against
        if (ps.PagoParaNoGenerarIntereses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagoParaNoGenerarIntereses is {ps.PagoParaNoGenerarIntereses.Status}.");

        // CL-21 formula
        var computed =
            ps.AdeudoPeriodoAnterior.Value
            + ps.CargosRegularesNoMeses.Value
            + ps.CargosComprasAMesesCapital.Value
            + ps.MontoIntereses.Value
            + ps.MontoComisiones.Value
            + ps.IvaInteresesYComisiones.Value
            - ps.PagosYAbonos.Value;

        var observed = ps.PagoParaNoGenerarIntereses.Value;
        var diff = Math.Abs(computed - observed);

        var locator = ps.PagoParaNoGenerarIntereses.Locator;

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

using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
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
/// <b>Tolerance (ADR-V3, Story 9.6):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using <c>Resolve(null)</c> for the legal floor; tenant overrides come from
/// <c>ResolvedTenantProfile.GetEffectiveTolerance</c>.
/// </para>
/// <para>
/// <b>Dual verdict (Story 9.6):</b> <see cref="RuleFinding.LegalBaselineVerdict"/> reflects the
/// legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar.
/// </para>
/// <para>
/// <b>Confidence guard (Story 9.5 / 9.6):</b> each RESUMEN subtotal input field is checked
/// for extraction confidence. If any confidence-bearing field is below the threshold the rule
/// abstains (InsufficientData) to prevent a false verdict from a misread digit.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>Any required RESUMEN subtotal field is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item>Any confidence-bearing field is below the confidence threshold.</item>
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

        // Resolve LEGAL tolerance: always use LegalDefault (no bundle override — Story 9.6)
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

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

        // Confidence guard (Story 9.5) — check each confidence-bearing input field
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ctx.ConfidenceBelowThreshold(ps.AdeudoPeriodoAnterior, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "AdeudoPeriodoAnterior", ps.AdeudoPeriodoAnterior.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.CargosRegularesNoMeses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "CargosRegularesNoMeses", ps.CargosRegularesNoMeses.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.CargosComprasAMesesCapital, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "CargosComprasAMesesCapital", ps.CargosComprasAMesesCapital.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.MontoIntereses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "MontoIntereses", ps.MontoIntereses.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.MontoComisiones, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "MontoComisiones", ps.MontoComisiones.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.IvaInteresesYComisiones, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "IvaInteresesYComisiones", ps.IvaInteresesYComisiones.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.PagosYAbonos, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "PagosYAbonos", ps.PagosYAbonos.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.PagoParaNoGenerarIntereses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "PagoParaNoGenerarIntereses", ps.PagoParaNoGenerarIntereses.Confidence, confidenceThreshold));

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
        var legalPasses = diff <= legalTolerance;
        var tenantPasses = diff <= effectiveTolerance;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{observed:F2}",
                    toleranceApplied: effectiveTolerance,
                    locator: locator,
                    legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"{computed:F2}",
                observed: $"{observed:F2}",
                toleranceApplied: effectiveTolerance,
                locator: locator,
                legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

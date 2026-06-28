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
/// <b>Tolerance (ADR-V3, Story 9.6):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using <c>Resolve(null)</c> for the legal floor; tenant overrides come from
/// <c>ResolvedTenantProfile.GetEffectiveTolerance</c>.
/// </para>
/// <para>
/// <b>Dual verdict (Story 9.6):</b> <see cref="RuleFinding.LegalBaselineVerdict"/> reflects the
/// legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar.
/// </para>
/// <para>
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if <see cref="PeriodSummary.SaldoCargosRegulares"/>
/// or <see cref="PeriodSummary.PagoParaNoGenerarIntereses"/> confidence is below the configured
/// threshold the rule abstains (InsufficientData).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><see cref="PeriodSummary.SaldoCargosRegulares"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.PagoParaNoGenerarIntereses"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item>Either field has confidence below threshold.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl22SaldoCargosRegularesRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl22SaldoCargosRegularesRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl22SaldoCargosRegularesRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-22";

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

        // Resolve LEGAL tolerance: always use LegalDefault (no bundle override — Story 9.6)
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        if (ps.SaldoCargosRegulares.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoCargosRegulares is {ps.SaldoCargosRegulares.Status}.");
        if (ps.PagoParaNoGenerarIntereses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagoParaNoGenerarIntereses is {ps.PagoParaNoGenerarIntereses.Status}.");

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ctx.ConfidenceBelowThreshold(ps.SaldoCargosRegulares, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "SaldoCargosRegulares", ps.SaldoCargosRegulares.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.PagoParaNoGenerarIntereses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "PagoParaNoGenerarIntereses", ps.PagoParaNoGenerarIntereses.Confidence, confidenceThreshold));

        var saldo = ps.SaldoCargosRegulares.Value;
        var pago = ps.PagoParaNoGenerarIntereses.Value;
        var diff = Math.Abs(saldo - pago);

        var locator = ps.SaldoCargosRegulares.Locator;
        var legalPasses = diff <= legalTolerance;
        var tenantPasses = diff <= effectiveTolerance;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{saldo:F2}",
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
                expected: $"{pago:F2}",
                observed: $"{saldo:F2}",
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

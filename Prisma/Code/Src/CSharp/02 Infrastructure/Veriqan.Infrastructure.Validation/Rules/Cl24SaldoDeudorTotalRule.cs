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
/// CL-24: Validates that "Saldo deudor total" equals the sum of the two NIVEL-DE-USO subtotals:
/// <c>SaldoDeudorTotal = SaldoCargosRegulares + SaldoCargosAMeses</c>.
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
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if any of the three confidence-bearing fields
/// (<see cref="PeriodSummary.SaldoCargosRegulares"/>, <see cref="PeriodSummary.SaldoCargosAMeses"/>,
/// <see cref="PeriodSummary.SaldoDeudorTotal"/>) is below the confidence threshold the rule
/// abstains (InsufficientData).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><see cref="PeriodSummary.SaldoCargosRegulares"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoCargosAMeses"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoDeudorTotal"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item>Any of the three fields has confidence below threshold.</item>
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
        if (ps.SaldoCargosAMeses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoCargosAMeses is {ps.SaldoCargosAMeses.Status}.");
        if (ps.SaldoDeudorTotal.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoDeudorTotal is {ps.SaldoDeudorTotal.Status}.");

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ConfidenceGuard.BelowThreshold(ps.SaldoCargosRegulares, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "SaldoCargosRegulares", ps.SaldoCargosRegulares.Confidence, confidenceThreshold));
        if (ConfidenceGuard.BelowThreshold(ps.SaldoCargosAMeses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "SaldoCargosAMeses", ps.SaldoCargosAMeses.Confidence, confidenceThreshold));
        if (ConfidenceGuard.BelowThreshold(ps.SaldoDeudorTotal, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "SaldoDeudorTotal", ps.SaldoDeudorTotal.Confidence, confidenceThreshold));

        var computed = ps.SaldoCargosRegulares.Value + ps.SaldoCargosAMeses.Value;
        var observed = ps.SaldoDeudorTotal.Value;
        var diff = Math.Abs(computed - observed);

        var locator = ps.SaldoDeudorTotal.Locator;
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

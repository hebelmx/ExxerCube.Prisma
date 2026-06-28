using System;
using System.Linq;
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
/// <b>Tolerance (ADR-V3, Story 9.6):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using <c>Resolve(null)</c> for the legal floor; tenant overrides come from
/// <c>ResolvedTenantProfile.GetEffectiveTolerance</c>.
/// </para>
/// <para>
/// <b>Dual verdict (Story 9.6):</b> <see cref="RuleFinding.LegalBaselineVerdict"/> reflects the
/// legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar.
/// </para>
/// <para>
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if <see cref="PeriodSummary.TotalCargos"/> or
/// <see cref="PeriodSummary.TotalAbonos"/> confidence is below the configured threshold the
/// rule abstains (InsufficientData).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item>StatementModel or PeriodSummary is null.</item>
///   <item>DESGLOSE movements not extracted.</item>
///   <item><see cref="Domain.Extraction.PeriodSummary.TotalCargos"/> is not Extracted.</item>
///   <item><see cref="Domain.Extraction.PeriodSummary.TotalAbonos"/> is not Extracted.</item>
///   <item>Either footer field has confidence below threshold.</item>
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

        // Resolve LEGAL tolerance: always use LegalDefault (no bundle override — Story 9.6)
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

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

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ctx.ConfidenceBelowThreshold(ps.TotalCargos, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "TotalCargos", ps.TotalCargos.Confidence, confidenceThreshold));
        if (ctx.ConfidenceBelowThreshold(ps.TotalAbonos, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "TotalAbonos", ps.TotalAbonos.Confidence, confidenceThreshold));

        var sumCargos = model.Movements
            .Where(m => m.Sign == MovementSign.Charge)
            .Sum(m => m.Amount);

        var sumAbonos = model.Movements
            .Where(m => m.Sign == MovementSign.Credit)
            .Sum(m => m.Amount);

        var printedCargos = ps.TotalCargos.Value;
        var printedAbonos = ps.TotalAbonos.Value;

        var cargosLegalOk = Math.Abs(sumCargos - printedCargos) <= legalTolerance;
        var abonosLegalOk = Math.Abs(sumAbonos - printedAbonos) <= legalTolerance;
        var cargosTenantOk = Math.Abs(sumCargos - printedCargos) <= effectiveTolerance;
        var abonosTenantOk = Math.Abs(sumAbonos - printedAbonos) <= effectiveTolerance;

        var legalPasses = cargosLegalOk && abonosLegalOk;
        var tenantPasses = cargosTenantOk && abonosTenantOk;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Cargos {sumCargos:F2}=={printedCargos:F2}, Abonos {sumAbonos:F2}=={printedAbonos:F2}",
                    toleranceApplied: effectiveTolerance,
                    legalBaselineVerdict: legalPasses ? FindingVerdict.Pass : FindingVerdict.Fail));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"Cargos={printedCargos:F2};Abonos={printedAbonos:F2}",
                observed: $"SumCargos={sumCargos:F2};SumAbonos={sumAbonos:F2}",
                toleranceApplied: effectiveTolerance,
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

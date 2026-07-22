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
/// CL-18: Validates that the RESUMEN field "Cargos regulares (no a meses)" equals the sum
/// of non-MSI charge amounts extracted from the DESGLOSE DE OPERACIONES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula:</b>
/// <c>CargosRegularesNoMeses ≈ Σ Amount of Charge movements where Description ∉ MSI pattern
/// AND Description ∉ interest/commission/IVA pattern</c>.
/// </para>
/// <para>
/// MSI installment rows are identified by the "NNN de NNN" fragment in the description
/// (e.g. "DON COLCHON CUMBRES 005 de 012") via <see cref="MovementClassifier.IsMsi"/>.
/// </para>
/// <para>
/// <b>RC1.S4.a (real-corpus triage, class c):</b> interest, commission, and IVA-on-interest/
/// commission DESGLOSE rows (e.g. "MONTO DE INTERESES", "COMISION ANUALIDAD", "IVA POR INTERESES
/// Y/O COMISIONES") are also excluded from this sum via <see cref="MovementClassifier.IsInterestCommissionOrIva"/>
/// — the printed "Cargos regulares (no a meses)" excludes those charges because they are reported
/// on their own dedicated RESUMEN lines. Evidence: Observed−Expected ==
/// MontoIntereses+MontoComisiones+IvaInteresesYComisiones to the cent on 4 independent real
/// Banamex months (<c>docs/qa/calibration/real-corpus-triage-2026-07.md</c>). The exclusion
/// pattern is deliberately conservative — an ambiguous row (not matching a known bank-fee
/// description PREFIX) stays IN the sum (fail-honest). <b>RC1.S4.b residual fix (chunk B4):</b>
/// the exclusion now matches description PREFIXES from a known bank-fee phrase family, not bare
/// mid-string keywords — a bare <c>\bCOMISION\b</c> keyword had wrongly excluded a merchant row
/// ("COMISION ESTATAL DE AG …", Comisión Estatal de Aguas) whose name merely contains the word
/// "Comisión". See <see cref="MovementClassifier"/> for the full evidence trail.
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
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if <see cref="PeriodSummary.CargosRegularesNoMeses"/>
/// confidence is below the configured threshold the rule abstains (InsufficientData).
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

        var movStatus = ctx.StatementModel!.MovementsStatus;
        if (movStatus != MovementsExtractionStatus.Extracted || ctx.StatementModel.Movements.Count == 0)
            return InsufficientData($"DESGLOSE movements not extracted (status: {movStatus}).");

        if (ps.CargosRegularesNoMeses.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CargosRegularesNoMeses is {ps.CargosRegularesNoMeses.Status}.");

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ctx.ConfidenceBelowThreshold(ps.CargosRegularesNoMeses, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "CargosRegularesNoMeses", ps.CargosRegularesNoMeses.Confidence, confidenceThreshold));

        var sumNonMsi = ctx.StatementModel.Movements
            .Where(m => m.Sign == MovementSign.Charge
                && !MovementClassifier.IsMsi(m.Description)
                && !MovementClassifier.IsInterestCommissionOrIva(m.Description))
            .Sum(m => m.Amount);

        var target = ps.CargosRegularesNoMeses.Value;
        var diff = Math.Abs(sumNonMsi - target);
        var locator = ps.CargosRegularesNoMeses.Locator;

        var legalPasses = diff <= legalTolerance;
        var tenantPasses = diff <= effectiveTolerance;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{sumNonMsi:F2}",
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
                expected: $"{target:F2}",
                observed: $"{sumNonMsi:F2}",
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

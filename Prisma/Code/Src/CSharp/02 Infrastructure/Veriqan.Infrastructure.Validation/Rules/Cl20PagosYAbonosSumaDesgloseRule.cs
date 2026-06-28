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
/// CL-20: Validates that the RESUMEN field "Pagos y abonos" equals the sum of all Credit
/// (abono) movement amounts extracted from the DESGLOSE DE OPERACIONES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantics (owner confirmed correct — adversarial review finding closed):</b>
/// Under Mexican bank-statement conventions, <em>cargos</em> are charges (money owed by the
/// customer) and <em>abonos</em> are credits or payments received (money flowing in).
/// "Pagos y abonos" therefore refers to the abono (Credit) movements only — summing credits
/// is correct and non-redundant with the other checks:
/// <list type="bullet">
///   <item>CL-18 sums non-MSI <em>cargos</em> (charges); CL-19 sums MSI capital cargos.</item>
///   <item>CL-20 sums <em>abonos</em> (credits/payments).</item>
///   <item>CL-44 checks both charge and credit totals against the DESGLOSE footer row.</item>
///   <item>CL-21 subtracts "Pagos y abonos" in the balance formula — consistent with this rule.</item>
/// </list>
/// The checklist note's literal "cargo y abono" describes the <em>contents</em> of the DESGLOSE
/// section (it has both types), not the scope of what CL-20 sums.
/// Terminology basis: CONDUSEF <c>Acuerdo_estado_de_cuenta.pdf</c>
/// (see <c>docs/legal/regulations/Acuerdo_estado_de_cuenta.pdf</c>).
/// </para>
/// <para>
/// <b>Formula:</b>
/// <c>PagosYAbonos ≈ Σ Amount of Credit (abono) movements in DESGLOSE</c>.
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
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if <see cref="PeriodSummary.PagosYAbonos"/>
/// confidence is below the configured threshold the rule abstains (InsufficientData).
/// </para>
/// </remarks>
internal sealed class Cl20PagosYAbonosSumaDesgloseRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl20PagosYAbonosSumaDesgloseRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl20PagosYAbonosSumaDesgloseRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-20";

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

        if (ps.PagosYAbonos.Status != ExtractionStatus.Extracted)
            return InsufficientData($"PagosYAbonos is {ps.PagosYAbonos.Status}.");

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ctx.ConfidenceBelowThreshold(ps.PagosYAbonos, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "PagosYAbonos", ps.PagosYAbonos.Confidence, confidenceThreshold));

        var sumAbonos = ctx.StatementModel.Movements
            .Where(m => m.Sign == MovementSign.Credit)
            .Sum(m => m.Amount);

        var target = ps.PagosYAbonos.Value;
        var diff = Math.Abs(sumAbonos - target);
        var locator = ps.PagosYAbonos.Locator;

        var legalPasses = diff <= legalTolerance;
        var tenantPasses = diff <= effectiveTolerance;

        if (tenantPasses)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{sumAbonos:F2}",
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
                observed: $"{sumAbonos:F2}",
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

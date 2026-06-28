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
/// CL-17: Validates that the statement's "Adeudo del periodo anterior" matches the prior
/// statement's "Pago para no generar intereses" within ±$0.50 MXN.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (FR-7):</b>
/// <c>PeriodSummary.AdeudoPeriodoAnterior == PriorStatement.ClosingBalances.PagoParaNoGenerarIntereses</c>
/// within the resolved <c>CurrencyToleranceMxn</c> (legal default ±$0.50 MXN).
/// </para>
/// <para>
/// <b>Prior statement resolution:</b> <see cref="VerificationContext.PriorStatement"/> is used
/// directly (populated by the bundle binder from <c>VecReferenceBundle.PriorStatements</c> using
/// the resolved account ref). If the binder did not find a prior statement for this account the
/// property is <see langword="null"/> and the rule emits <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3, Story 9.6):</b> resolved via <see cref="ILegalToleranceProvider"/>
/// using <c>Resolve(null)</c> for the legal floor; tenant overrides come from
/// <c>ResolvedTenantProfile.GetEffectiveTolerance</c>.
/// </para>
/// <para>
/// <b>Dual verdict (Story 9.6):</b> <see cref="RuleFinding.LegalBaselineVerdict"/> reflects the
/// legal floor; <see cref="RuleFinding.Verdict"/> reflects the tenant-effective bar. When no tenant
/// profile is supplied both verdicts are equal.
/// </para>
/// <para>
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if <see cref="PeriodSummary.AdeudoPeriodoAnterior"/>
/// confidence is below the configured threshold the rule abstains (InsufficientData).
/// </para>
/// <para>
/// <b>InsufficientData paths (graceful degradation — NFR-2/6):</b>
/// <list type="bullet">
///   <item><see cref="Domain.Extraction.PeriodSummary.AdeudoPeriodoAnterior"/> is
///     <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="Domain.Extraction.PeriodSummary.AdeudoPeriodoAnterior"/> confidence is
///     below threshold (confidence guard, Story 9.5).</item>
///   <item><see cref="VerificationContext.PriorStatement"/> is <see langword="null"/>
///     (no prior statement for this account in the bundle).</item>
///   <item><see cref="Domain.ReferenceData.ClosingBalances.PagoParaNoGenerarIntereses"/>
///     is <see langword="null"/> inside the prior statement.</item>
/// </list>
/// None of these paths produce a false FAIL; the rule degrades honestly.
/// </para>
/// </remarks>
internal sealed class Cl17AdeudoPeriodoAnteriorRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl17AdeudoPeriodoAnteriorRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl17AdeudoPeriodoAnteriorRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-17";

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

        // Statement-side: AdeudoPeriodoAnterior must be extracted
        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        if (ps.AdeudoPeriodoAnterior.Status != ExtractionStatus.Extracted)
            return InsufficientData(
                $"AdeudoPeriodoAnterior is {ps.AdeudoPeriodoAnterior.Status}; cannot run cross-period check.");

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ctx.ConfidenceBelowThreshold(ps.AdeudoPeriodoAnterior, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "AdeudoPeriodoAnterior", ps.AdeudoPeriodoAnterior.Confidence, confidenceThreshold));

        // Reference-data side: prior statement must be present (binder resolved it, or null)
        if (ctx.PriorStatement is null)
            return InsufficientData(
                "No prior statement found in the bundle for this account. " +
                "CL-17 requires the prior period's closing balances.");

        var priorPago = ctx.PriorStatement.ClosingBalances?.PagoParaNoGenerarIntereses;
        if (priorPago is null)
            return InsufficientData(
                "Prior statement ClosingBalances.PagoParaNoGenerarIntereses is null; " +
                "cannot run cross-period comparison.");

        // Compare with tolerance — compute both verdicts
        var observed = ps.AdeudoPeriodoAnterior.Value;
        var expected = priorPago.Value;
        var diff = Math.Abs(observed - expected);

        var locator = ps.AdeudoPeriodoAnterior.Locator;
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
                expected: $"{expected:F2}",
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

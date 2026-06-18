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
/// CL-25: Validates that "Crédito disponible" equals the credit line minus the total outstanding balance:
/// <c>CreditoDisponible = creditLine − SaldoDeudorTotal</c>.
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
/// <b>Confidence guard (Story 9.5 / 9.6):</b> if <see cref="PeriodSummary.CreditoDisponible"/>
/// or <see cref="PeriodSummary.SaldoDeudorTotal"/> confidence is below the configured threshold
/// the rule abstains (InsufficientData).
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><see cref="PeriodSummary.CreditoDisponible"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item><see cref="PeriodSummary.SaldoDeudorTotal"/> is <see cref="ExtractionStatus.NotExtracted"/>.</item>
///   <item>Either field has confidence below threshold.</item>
///   <item>No credit line found for the resolved product in the bundle.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl25CreditoDisponibleRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Cl25CreditoDisponibleRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Cl25CreditoDisponibleRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "CL-25";

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

        if (ps.CreditoDisponible.Status != ExtractionStatus.Extracted)
            return InsufficientData($"CreditoDisponible is {ps.CreditoDisponible.Status}.");
        if (ps.SaldoDeudorTotal.Status != ExtractionStatus.Extracted)
            return InsufficientData($"SaldoDeudorTotal is {ps.SaldoDeudorTotal.Status}.");

        // Confidence guard (Story 9.5)
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (ConfidenceGuard.BelowThreshold(ps.CreditoDisponible, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "CreditoDisponible", ps.CreditoDisponible.Confidence, confidenceThreshold));
        if (ConfidenceGuard.BelowThreshold(ps.SaldoDeudorTotal, confidenceThreshold))
            return InsufficientData(ConfidenceGuard.Reason(
                "SaldoDeudorTotal", ps.SaldoDeudorTotal.Confidence, confidenceThreshold));

        var creditLine = ResolveCreditLine(ctx);
        if (creditLine is null)
            return InsufficientData("Credit line not found in bundle for the resolved product.");

        var computed = creditLine.Value - ps.SaldoDeudorTotal.Value;
        var observed = ps.CreditoDisponible.Value;
        var diff = Math.Abs(computed - observed);

        var locator = ps.CreditoDisponible.Locator;
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

    private static decimal? ResolveCreditLine(VerificationContext ctx)
    {
        if (ctx.Bundle.ClientAccounts is null)
            return null;

        var productId = ctx.ResolvedProduct.ProductId;

        foreach (var client in ctx.Bundle.ClientAccounts)
        {
            if (client.Accounts is null)
                continue;

            foreach (var account in client.Accounts)
            {
                if (string.Equals(account.ProductId, productId, StringComparison.OrdinalIgnoreCase)
                    && account.CreditLine is not null)
                {
                    return account.CreditLine;
                }
            }
        }

        return null;
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

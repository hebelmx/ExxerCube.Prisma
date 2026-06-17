using System;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
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
/// within <c>ToleranceConfig.CurrencyToleranceMxn</c> (default ±$0.50 MXN).
/// </para>
/// <para>
/// <b>Prior statement resolution:</b> <see cref="VerificationContext.PriorStatement"/> is used
/// directly (populated by the bundle binder from <c>VecReferenceBundle.PriorStatements</c> using
/// the resolved account ref). If the binder did not find a prior statement for this account the
/// property is <see langword="null"/> and the rule emits <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>.
/// </para>
/// <para>
/// <b>Tolerance (ADR-V3):</b> ±$0.50 MXN from <c>ToleranceConfig.CurrencyToleranceMxn</c>.
/// </para>
/// <para>
/// <b>InsufficientData paths (graceful degradation — NFR-2/6):</b>
/// <list type="bullet">
///   <item>Null <see cref="VerificationContext.ToleranceConfig"/>.</item>
///   <item><see cref="Domain.Extraction.PeriodSummary.AdeudoPeriodoAnterior"/> is
///     <see cref="ExtractionStatus.NotExtracted"/>.</item>
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

    /// <inheritdoc />
    public string CheckId => "CL-17";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Tolerance required (ADR-V3) — absent bundle section → InsufficientData, not Fail
        if (ctx.ToleranceConfig is null)
            return InsufficientData("ToleranceConfig is absent from the bundle.");

        var tolerance = ctx.ToleranceConfig.CurrencyToleranceMxn ?? 0.50m;

        // Statement-side: AdeudoPeriodoAnterior must be extracted
        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return InsufficientData("StatementModel or PeriodSummary is not populated.");

        if (ps.AdeudoPeriodoAnterior.Status != ExtractionStatus.Extracted)
            return InsufficientData(
                $"AdeudoPeriodoAnterior is {ps.AdeudoPeriodoAnterior.Status}; cannot run cross-period check.");

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

        // Compare with tolerance
        var observed = ps.AdeudoPeriodoAnterior.Value;
        var expected = priorPago.Value;
        var diff = Math.Abs(observed - expected);

        var locator = ps.AdeudoPeriodoAnterior.Locator;

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
                expected: $"{expected:F2}",
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

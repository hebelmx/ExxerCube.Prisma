using System;
using System.Collections.Generic;
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
/// LAW-§6-SIMULACION: Validates the §6 "¿Cuánto pagarías?" payment-simulation table by
/// rerunning the Acuerdo §6 / Banxico Circular 13/2011 revolving-balance recursion for
/// the three standard payment scenarios (k ∈ {1, 2, 5} × pago mínimo) and confirming
/// the computed months-to-pay and total ordinary interest match the printed §6 columns
/// within the typed tolerance.
/// </summary>
/// <remarks>
/// <para>
/// <b>§6-table extraction carry-forward:</b> Story 11.1 extracted §8/§19/§20/§16 into
/// <see cref="StatementModel.FinancialTables"/> but did NOT build a §6 extractor or corpus.
/// Consequently <c>ctx.StatementModel.FinancialTables.FirstOrDefault(t =&gt; t.SectionNumber == 6)</c>
/// will be <see langword="null"/> in all real statements today. The rule returns
/// <see cref="FindingVerdict.InsufficientData"/> in that case (cannot verify what it cannot
/// read). The recursion engine and comparison logic are implemented now and are fully proved
/// by synthetic tests; they will activate automatically once a §6-table extractor is built.
/// </para>
/// <para>
/// <b>Recursion (Banxico Circular 13/2011 pago-mínimo method):</b>
/// Starting balance B₀ = <c>PagoParaNoGenerarIntereses</c> (the full balance to amortise),
/// monthly ordinary rate <c>r = Tasa / 12</c> (Tasa is stored as a fraction, e.g. 0.2736 for
/// 27.36%), Mexican IVA constant = 0.16 on interest. For a fixed monthly payment
/// <c>P = k × PagoMinimo</c>:
/// <code>
///   each month:
///     interes   = B × r
///     iva       = interes × 0.16
///     B_next    = B + interes + iva − P
///     accum    += interes          ← ordinary (pre-IVA) interest only
///     month++
///   stop when B_next ≤ 0 (final payment is partial, last month still counted)
/// </code>
/// </para>
/// <para>
/// <b>Column reported in §6:</b> the "intereses ordinarios" column in §6 reports the
/// accumulated <em>pre-IVA</em> ordinary interest (the <c>interes</c> sum, not the
/// IVA-inclusive total). IVA is shown separately in the "IVA de intereses" column if
/// present. This rule compares against the pre-IVA ordinary interest total.
/// </para>
/// <para>
/// <b>Months tolerance:</b> month counts are exact integers; the rule allows ±1 month
/// tolerance to absorb the boundary rounding on the final partial-payment month and minor
/// balance differences. Interest totals use the standard CurrencyMxn tolerance (0.50 MXN).
/// </para>
/// <para>
/// <b>Non-amortising guard:</b> when <c>P ≤ interes + iva</c> in month 1, the balance
/// never decreases (payment does not cover even the first month's charges). This is
/// mathematically infinite; the rule caps iterations at 600 months and returns
/// <see cref="FindingVerdict.InsufficientData"/> for that scenario (never loops forever,
/// never false-Fails).
/// </para>
/// <para>
/// <b>NA / "Este periodo" trivial cases:</b> when B₀ ≤ 0 or the balance is paid within
/// the current period (B₀ ≤ P), the statement prints "Este periodo" or "N/A" in the §6
/// columns. The computed result would be ≤ 1 month; the rule matches the printed
/// NA/trivial representation rather than forcing a numeric comparison.
/// </para>
/// <para>
/// <b>IVA assumption:</b> Mexican IVA is 16% (0.16) as of Banxico Circular 13/2011 and
/// subsequent confirmations. There is no separate IVA-rate field in the extracted model;
/// the constant 0.16 is hard-coded and documented here. If the applicable IVA rate changes
/// in a future period, update <see cref="IvaRate"/> and re-verify the corpus.
/// </para>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail would halt a bank's billing run.
/// The rule MUST return <see cref="FindingVerdict.InsufficientData"/> on any missing,
/// low-confidence, or indeterminable input — never guess, never false-Fail.
/// </para>
/// </remarks>
internal sealed class Section6PaymentSimulationRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section6Number = 6;

    // Mexican IVA constant as of Banxico Circular 13/2011 and current law.
    // Hard-coded because no IVA-rate field exists in the extracted model.
    // ⚠️ Update + reverify corpus if IVA changes from 16%.
    internal const decimal IvaRate = 0.16m;

    // §6 scenarios: multipliers k ∈ {1, 2, 5} applied to PagoMinimo.
    private static readonly int[] ScenarioMultipliers = [1, 2, 5];

    // §6 column indices within each scenario row (after the label cell):
    // [0] = months to pay; [1] = total ordinary interest.
    private const int ColMonths = 0;
    private const int ColInterest = 1;
    private const int RequiredValueCellCount = 2;

    // Months tolerance: ±1 month to absorb final-period boundary rounding.
    // ⚠️ Engineering judgement; confirm with compliance if a corpus shows wider drift.
    internal const int MonthsTolerance = 1;

    // Cap on recursion iterations to guard against non-amortising scenarios.
    // 600 months = 50 years; any scenario that doesn't pay off in 50 years is indeterminable.
    private const int MaxIterations = 600;

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Section6PaymentSimulationRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Section6PaymentSimulationRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "LAW-§6-SIMULACION";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §6";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Resolve LEGAL tolerance: always use LegalDefault.
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance.
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        // Guard: StatementModel must be present.
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Guard: §6 table must be present and fully extracted.
        // In production today (Story 11.1 scope did not include a §6 extractor) this table
        // will be absent → InsufficientData. See class-level docs for the carry-forward note.
        var table6 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section6Number);

        if (table6 is null)
            return InsufficientData(
                "§6 table is absent from FinancialTables. " +
                "The §6-table extractor has not yet been built (Story 11.1 carry-forward). " +
                "This rule will activate automatically once extraction is available.");

        if (table6.Status == TableExtractionStatus.SectionNotFound)
            return InsufficientData("§6 table is absent (SectionNotFound).");

        if (table6.Status != TableExtractionStatus.Extracted)
            return InsufficientData($"§6 table extraction status is {table6.Status} — not Extracted.");

        // Guard: need at least 3 rows (one per scenario k=1,2,5).
        if (table6.Rows.Count < ScenarioMultipliers.Length)
            return InsufficientData(
                $"§6 table has {table6.Rows.Count} row(s); " +
                $"expected at least {ScenarioMultipliers.Length} (one per scenario k∈{{1,2,5}}).");

        // Resolve confidence threshold.
        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        // Guard: recursion inputs from PeriodSummary.
        var periodSummary = ctx.StatementModel.PeriodSummary;
        if (periodSummary is null)
            return InsufficientData("PeriodSummary is not populated (recursion inputs unavailable).");

        var pagoMinimoField = periodSummary.PagoMinimo;
        var tasaField = periodSummary.Tasa;
        var b0Field = periodSummary.PagoParaNoGenerarIntereses;

        if (pagoMinimoField.Status != ExtractionStatus.Extracted)
            return InsufficientData(
                $"PagoMinimo not extracted (status={pagoMinimoField.Status}); cannot run recursion.");

        if (pagoMinimoField.Confidence < confidenceThreshold)
            return InsufficientData(
                $"PagoMinimo confidence {pagoMinimoField.Confidence:F2} < required {confidenceThreshold:F2}.");

        if (tasaField.Status != ExtractionStatus.Extracted)
            return InsufficientData(
                $"Tasa not extracted (status={tasaField.Status}); cannot run recursion.");

        if (tasaField.Confidence < confidenceThreshold)
            return InsufficientData(
                $"Tasa confidence {tasaField.Confidence:F2} < required {confidenceThreshold:F2}.");

        if (b0Field.Status != ExtractionStatus.Extracted)
            return InsufficientData(
                $"PagoParaNoGenerarIntereses not extracted (status={b0Field.Status}); cannot run recursion.");

        if (b0Field.Confidence < confidenceThreshold)
            return InsufficientData(
                $"PagoParaNoGenerarIntereses confidence {b0Field.Confidence:F2} < required {confidenceThreshold:F2}.");

        var pagoMinimo = pagoMinimoField.Value!;
        var tasa = tasaField.Value!;
        var b0 = b0Field.Value!;

        // Tasa should already be a fraction (e.g. 0.2736). Guard against obviously wrong values.
        if (tasa <= 0m || tasa > 10m)
            return InsufficientData($"Tasa value {tasa} is out of the plausible fraction range (0, 10].");

        if (pagoMinimo <= 0m)
            return InsufficientData($"PagoMinimo value {pagoMinimo} must be positive.");

        var locator = table6.Locator;

        // -----------------------------------------------------------------------
        // Run the recursion for each scenario k ∈ {1, 2, 5} and compare to §6.
        // -----------------------------------------------------------------------

        for (var scenarioIdx = 0; scenarioIdx < ScenarioMultipliers.Length; scenarioIdx++)
        {
            var k = ScenarioMultipliers[scenarioIdx];
            var payment = k * pagoMinimo;
            var row = table6.Rows[scenarioIdx];

            // Validate row has at least 2 value cells (months + interest).
            if (row.Values.Count < RequiredValueCellCount)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) has {row.Values.Count} value cell(s); " +
                    $"expected at least {RequiredValueCellCount}.");

            var monthsCell = row.Values[ColMonths];
            var interestCell = row.Values[ColInterest];

            // Handle NA / "Este periodo" trivial cases:
            // When B₀ ≤ 0 or B₀ ≤ payment, the balance pays off this period.
            // The statement prints "Este periodo" or NA. We skip the numeric comparison.
            if (b0 <= 0m || b0 <= payment)
            {
                // Expect the printed cells to be NA or trivial (kind NotApplicable or value ≤ 1).
                // If the bank has printed a non-trivial number here, that is unusual but we
                // cannot confidently Fail without a corpus — abstain rather than false-Fail.
                if (monthsCell.Kind == CellKind.NotApplicable ||
                    interestCell.Kind == CellKind.NotApplicable)
                {
                    continue; // NA cells for a trivial scenario → consistent, skip.
                }
                // Non-NA printed values when balance is trivial → uncertain state; abstain.
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}): B₀={b0} ≤ payment={payment} (trivial/NA case) " +
                    $"but §6 cell is non-NA. Cannot verify without corpus.");
            }

            // Run the recursion.
            var simulation = PaymentSimulation.Run(b0, tasa, payment);

            if (!simulation.IsAmortising)
                return InsufficientData(
                    $"§6 scenario k={k}: payment {payment:F2} ≤ first-month charges " +
                    $"(balance never decreases — non-amortising). Cannot verify §6 printed value.");

            // Validate that the printed cells have usable values.
            if (monthsCell.Kind == CellKind.NotApplicable)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) months cell is NA but scenario is amortising.");

            if (monthsCell.ParsedValue is null)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) months cell has no parsed value (raw: \"{monthsCell.RawText}\").");

            if (monthsCell.Confidence < confidenceThreshold)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) months cell confidence {monthsCell.Confidence:F2} " +
                    $"< required {confidenceThreshold:F2}.");

            if (interestCell.Kind == CellKind.NotApplicable)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) interest cell is NA but scenario is amortising.");

            if (interestCell.ParsedValue is null)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) interest cell has no parsed value (raw: \"{interestCell.RawText}\").");

            if (interestCell.Confidence < confidenceThreshold)
                return InsufficientData(
                    $"§6 row[{scenarioIdx}] (k={k}) interest cell confidence {interestCell.Confidence:F2} " +
                    $"< required {confidenceThreshold:F2}.");

            var printedMonths = (int)Math.Round(monthsCell.ParsedValue.Value);
            var printedInterest = interestCell.ParsedValue.Value;

            var monthsDiff = Math.Abs(simulation.Months - printedMonths);
            var interestDiff = Math.Abs(simulation.TotalOrdinaryInterest - printedInterest);

            var legalMonthsPasses   = monthsDiff <= MonthsTolerance;
            var tenantMonthsPasses  = monthsDiff <= MonthsTolerance; // months tolerance is fixed ±1, not tenant-adjustable
            var legalInterestPasses = interestDiff <= legalTolerance;
            var tenantInterestPasses = interestDiff <= effectiveTolerance;

            if (!tenantMonthsPasses || !tenantInterestPasses)
            {
                var reason = !tenantMonthsPasses
                    ? $"§6 row[{scenarioIdx}] k={k}: computed months={simulation.Months}, " +
                      $"printed months={printedMonths}, diff={monthsDiff} > tolerance={MonthsTolerance}"
                    : $"§6 row[{scenarioIdx}] k={k}: computed interest={simulation.TotalOrdinaryInterest:F2}, " +
                      $"printed interest={printedInterest:F2}, diff={interestDiff:F4} > tolerance={effectiveTolerance:F2}";

                var legalBaselinePasses = legalMonthsPasses && legalInterestPasses;

                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Fail(
                        checkId: CheckId,
                        technique: Technique,
                        severity: FindingSeverity.Critical,
                        engineVersion: Version,
                        expected: $"Acuerdo §6 / Banxico Circular 13/2011 simulation",
                        observed: reason,
                        toleranceApplied: effectiveTolerance,
                        locator: interestCell.Locator ?? locator,
                        legalBaselineVerdict: legalBaselinePasses ? FindingVerdict.Pass : FindingVerdict.Fail));
            }
        }

        // All scenarios verified.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"{ScenarioMultipliers.Length} §6 scenarios verified (k∈{{1,2,5}})",
                toleranceApplied: effectiveTolerance,
                locator: locator,
                legalBaselineVerdict: FindingVerdict.Pass));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

/// <summary>
/// Pure functional recursion engine for the §6 / Banxico Circular 13/2011 payment simulation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recursion formula per month:</b>
/// <code>
///   interes   = B × r                (r = Tasa / 12, monthly ordinary rate)
///   iva       = interes × 0.16       (Mexican IVA constant — see Section6PaymentSimulationRule.IvaRate)
///   B_next    = B + interes + iva − P
///   accum    += interes              (pre-IVA ordinary interest only)
///   month++
///   stop when B_next ≤ 0
/// </code>
/// </para>
/// <para>
/// The <see cref="SimulationResult.TotalOrdinaryInterest"/> accumulates <em>pre-IVA</em> interest only,
/// matching the "intereses ordinarios" column the §6 table prints (IVA is shown separately).
/// </para>
/// <para>
/// Non-amortising scenarios (payment ≤ first-month interes + iva) are detected in
/// <see cref="SimulationResult.IsAmortising"/>; callers must check this flag before using
/// <see cref="SimulationResult.Months"/> or <see cref="SimulationResult.TotalOrdinaryInterest"/>.
/// </para>
/// </remarks>
internal static class PaymentSimulation
{
    /// <summary>
    /// Runs the revolving-balance recursion and returns the result.
    /// </summary>
    /// <param name="b0">Starting balance (PagoParaNoGenerarIntereses), must be &gt; 0.</param>
    /// <param name="annualRate">Annual ordinary rate as a decimal fraction (e.g. 0.2736).</param>
    /// <param name="monthlyPayment">Fixed monthly payment amount (k × PagoMinimo).</param>
    /// <returns>
    /// A <see cref="SimulationResult"/> with <see cref="SimulationResult.IsAmortising"/> = false
    /// when the payment does not cover the first month's charges (non-amortising infinite case).
    /// </returns>
    internal static SimulationResult Run(decimal b0, decimal annualRate, decimal monthlyPayment)
    {
        var r = annualRate / 12m;

        // First-month charges to detect non-amortising case.
        var firstInteres = b0 * r;
        var firstIva = firstInteres * Section6PaymentSimulationRule.IvaRate;
        if (monthlyPayment <= firstInteres + firstIva)
        {
            return new SimulationResult(IsAmortising: false, Months: 0, TotalOrdinaryInterest: 0m);
        }

        var b = b0;
        var totalOrdinaryInterest = 0m;
        var months = 0;

        while (b > 0m && months < MaxIterations)
        {
            var interes = b * r;
            var iva = interes * Section6PaymentSimulationRule.IvaRate;
            var bNext = b + interes + iva - monthlyPayment;

            totalOrdinaryInterest += interes;
            months++;

            if (bNext <= 0m)
                break; // balance paid off this month (final partial payment)

            b = bNext;
        }

        // If we hit the cap without paying off, report as non-amortising.
        if (months >= MaxIterations && b > 0m)
        {
            return new SimulationResult(IsAmortising: false, Months: 0, TotalOrdinaryInterest: 0m);
        }

        return new SimulationResult(IsAmortising: true, Months: months, TotalOrdinaryInterest: totalOrdinaryInterest);
    }

    private const int MaxIterations = 600;
}

/// <summary>Result of a single <see cref="PaymentSimulation.Run"/> call.</summary>
/// <param name="IsAmortising">
/// <see langword="true"/> when the payment covers at least the first month's charges and
/// the balance eventually reaches zero. <see langword="false"/> when the payment is too
/// small (non-amortising / cap reached).
/// </param>
/// <param name="Months">
/// Number of months until the balance reaches zero. Valid only when
/// <see cref="IsAmortising"/> is <see langword="true"/>.
/// </param>
/// <param name="TotalOrdinaryInterest">
/// Accumulated pre-IVA ordinary interest over all months. Valid only when
/// <see cref="IsAmortising"/> is <see langword="true"/>.
/// </param>
internal sealed record SimulationResult(bool IsAmortising, int Months, decimal TotalOrdinaryInterest);

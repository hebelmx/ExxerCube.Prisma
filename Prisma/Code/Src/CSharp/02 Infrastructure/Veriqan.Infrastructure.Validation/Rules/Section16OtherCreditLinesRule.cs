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
/// LAW-§16-OTRASLINEAS: Validates per-row arithmetic for §16
/// "Información de otras líneas de crédito" — a conditional section that is ONLY
/// present when the cardholder has active other credit lines on the same statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §16 mandates that
/// when an account has other credit lines, each row must carry arithmetically consistent
/// figures:
/// <list type="bullet">
///   <item>Check 1 — Interest vs rate/días: Intereses(v) ≈ SaldoPendiente(iv) × (Tasa(ix)/360) × días,
///         within CurrencyMxn tolerance. Days come from PeriodSummary.DayCount; if not available
///         this sub-check is skipped (not failed).</item>
///   <item>Check 2 — IVA vs interest: IVA(vi) ≈ Intereses(v) × 0.16, within tolerance.</item>
///   <item>Check 3 — Totals-tie cross-check against §19 "otras líneas" row (best-effort):
///         sum of §16 Intereses rows ≈ §19 row labeled "otras líneas de crédito" Monto, within tolerance.
///         Skipped (abstain) when either side is absent or low-confidence.</item>
/// </list>
/// </para>
/// <para>
/// <b>§16 column semantic order (9 value cells per row, 0-indexed):</b>
/// <list type="bullet">
///   <item>[0] Fecha</item>
///   <item>[1] Descripción</item>
///   <item>[2] Monto original</item>
///   <item>[3] Saldo pendiente — base for interest formula (Check 1)</item>
///   <item>[4] Intereses del periodo — computed interest (Check 1 target, Check 2 input)</item>
///   <item>[5] IVA de intereses — IVA on interest (Check 2 target)</item>
///   <item>[6] Pago requerido</item>
///   <item>[7] Núm. de pago</item>
///   <item>[8] Tasa de interés aplicable — annual rate (Check 1 input)</item>
/// </list>
/// </para>
/// <para>
/// <b>Conditional-section behaviour (cardinal rule — preventive gate):</b>
/// <list type="bullet">
///   <item>
///     §16 <see cref="TableExtractionStatus.SectionNotFound"/> → <b>Pass</b> with an
///     "N/A — section not applicable" note. The section is legitimately absent
///     when the cardholder has no other credit lines; this is the normal path for
///     all current fixtures. Never Fail for a legitimately absent conditional section.
///   </item>
///   <item>
///     §16 <see cref="TableExtractionStatus.Indeterminate"/> or
///     <see cref="TableExtractionStatus.NoRowsParsed"/> → <b>InsufficientData</b>.
///     The section was detected but could not be reliably read.
///   </item>
///   <item>
///     §16 <see cref="TableExtractionStatus.Extracted"/> → per-column arithmetic checks.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Aggregate verdict when §16 is Extracted:</b>
/// <list type="bullet">
///   <item>Any confident mismatch beyond tolerance → Fail citing §16.</item>
///   <item>No row / sub-check could be evaluated → InsufficientData.</item>
///   <item>All evaluated checks pass → Pass.</item>
/// </list>
/// </para>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail would halt a bank's billing run.
/// The rule MUST return <see cref="FindingVerdict.InsufficientData"/> on any missing
/// or low-confidence cell — never guess, never false-Fail.
/// </para>
/// <para>
/// <b>Production accuracy note:</b> §16 column-mapping accuracy is corpus-gated —
/// no real §16 fixture is available to calibrate column positions. Production accuracy
/// is unverified until a real §16 statement corpus is obtained.
/// </para>
/// </remarks>
internal sealed class Section16OtherCreditLinesRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section16Number = 16;
    private const int Section19Number = 19;

    // §16 column indices within each row's Values list (9-column table)
    // (i) Fecha, (ii) Descripción, (iii) Monto original, (iv) Saldo pendiente,
    // (v) Intereses del periodo, (vi) IVA de intereses, (vii) Pago requerido,
    // (viii) Núm. de pago, (ix) Tasa de interés aplicable
    //
    // CORPUS-VERIFY: All indices below are derived from the CONDUSEF §16 spec and
    // synthetic fixtures only — no real §16 statement corpus exists to calibrate
    // column positions. Verify against a real §16 statement before relying on these.
    private const int ColSaldoPendiente = 3;   // (iv) CORPUS-VERIFY
    private const int ColIntereses = 4;        // (v)  CORPUS-VERIFY
    private const int ColIva = 5;              // (vi) CORPUS-VERIFY
    private const int ColTasa = 8;             // (ix) CORPUS-VERIFY
    private const int RequiredValueCellCount = 9; // CORPUS-VERIFY

    private const decimal IvaRate = 0.16m;
    private const decimal DaysPerYear = 360m;

    // Rate normalization thresholds — reuse §19's approach
    private const decimal RatePercentageThreshold = 1.5m;

    // Fragment to identify the "otras líneas" row in the §19 table
    private const string OtrasLineasFragment = "otras líneas";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Section16OtherCreditLinesRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Section16OtherCreditLinesRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "LAW-§16-OTRASLINEAS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §16";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Guard: tolerance must be registered before attempting any resolution.
        if (!_toleranceProvider.Has(CheckId))
            return InsufficientData($"No legal tolerance registered for {CheckId} — cannot evaluate.");

        // Resolve LEGAL tolerance
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;

        // Resolve TENANT-EFFECTIVE tolerance
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        // Guard: StatementModel must be present.
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Locate the §16 FinancialTable.
        var table16 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section16Number);

        // ----------------------------------------------------------------
        // Conditional-section gate:
        // SectionNotFound means the cardholder has no other credit lines —
        // this is NOT a defect. Return Pass with N/A note.
        // ----------------------------------------------------------------
        if (table16 is null || table16.Status == TableExtractionStatus.SectionNotFound)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "§16 Información de otras líneas de crédito is not applicable — " +
                              "section not found (cardholder has no other credit lines on this statement). " +
                              "N/A — conditional section legitimately absent."));
        }

        // Section was detected but extraction was not reliable.
        if (table16.Status != TableExtractionStatus.Extracted)
        {
            return InsufficientData(
                $"§16 table extraction status is {table16.Status} — not Extracted; " +
                "cannot verify per-row arithmetic.");
        }

        if (table16.Rows.Count == 0)
        {
            return InsufficientData("§16 table has no rows.");
        }

        // ----------------------------------------------------------------
        // Guard 1 — Table-level column-count check (CORPUS-VERIFY)
        // The §16 column-index map is calibrated for exactly 9 value cells per row.
        // If the parsed table has a different column count the index map is unverified
        // against this layout and we must abstain rather than risk reading adjacent
        // columns as if they were the intended ones.
        // ----------------------------------------------------------------
        var observedColumnCount = table16.Rows.Max(r => r.Values.Count);
        if (observedColumnCount != RequiredValueCellCount)
        {
            return InsufficientData(
                $"§16 column count {observedColumnCount} does not match expected " +
                $"{RequiredValueCellCount}-column map — abstaining pending corpus calibration.");
        }

        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        // Determine días from PeriodSummary (needed for Check 1).
        // If absent, Check 1 (interest vs rate/días) is skipped — not failed.
        var dias = TryGetDays(ctx);

        var locator = table16.Locator;

        // ----------------------------------------------------------------
        // Per-row pass: Check 1 (Intereses vs rate/días) + Check 2 (IVA vs Intereses)
        // ----------------------------------------------------------------

        var rowsChecked = 0;
        var firstFailReason = (string?)null;
        var firstFailLocator = (FieldLocator?)null;
        var firstFailLegalPasses = true;
        var section16InteresTotal = 0m;
        var hasCheckableInteresRows = false;

        foreach (var row in table16.Rows)
        {
            // Guard 2 — Per-row index bounds check (CORPUS-VERIFY)
            // Guard 1 catches the common case (uniform wrong column count across all rows).
            // Guard 2 is the safety net for individual rows that are shorter than the table
            // maximum, so we skip the row rather than reading from an adjacent unintended column.
            if (row.Values.Count < RequiredValueCellCount
                || ColSaldoPendiente >= row.Values.Count
                || ColIntereses      >= row.Values.Count
                || ColIva            >= row.Values.Count
                || ColTasa           >= row.Values.Count)
            {
                // Abstain on this row only — do not Fail and do not abort the rule.
                continue;
            }

            var saldoCell    = row.Values[ColSaldoPendiente];
            var interesCell  = row.Values[ColIntereses];
            var ivaCell      = row.Values[ColIva];
            var tasaCell     = row.Values[ColTasa];

            // If all four needed cells are NA → row is not applicable, skip silently.
            if (IsNaOrEmpty(saldoCell) && IsNaOrEmpty(interesCell) &&
                IsNaOrEmpty(ivaCell)   && IsNaOrEmpty(tasaCell))
            {
                continue;
            }

            // IVA cell and Intereses cell are the minimum needed for Check 2.
            // If either is NA, skip this row entirely (partial NA — avoid false Fail).
            if (IsNaOrEmpty(interesCell) || IsNaOrEmpty(ivaCell))
                continue;

            // Check confidence on the two cells needed for Check 2.
            if (!HasUsableValue(interesCell, confidenceThreshold))
                return InsufficientData(
                    $"§16 row '{row.Label.RawText}': Intereses cell has missing or " +
                    $"low-confidence value (confidence={interesCell.Confidence:F2}, threshold={confidenceThreshold:F2}).");

            if (!HasUsableValue(ivaCell, confidenceThreshold))
                return InsufficientData(
                    $"§16 row '{row.Label.RawText}': IVA cell has missing or " +
                    $"low-confidence value (confidence={ivaCell.Confidence:F2}, threshold={confidenceThreshold:F2}).");

            var interesReported = interesCell.ParsedValue!.Value;
            var ivaReported     = ivaCell.ParsedValue!.Value;

            // ---- Check 1: Intereses ≈ SaldoPendiente × (Tasa/360) × días ----
            // Only attempted when: días is known AND saldo + tasa cells are usable.
            if (dias.HasValue
                && !IsNaOrEmpty(saldoCell) && HasUsableValue(saldoCell, confidenceThreshold)
                && !IsNaOrEmpty(tasaCell)  && HasUsableValue(tasaCell,  confidenceThreshold))
            {
                var tasaNormalized = NormalizeRateToFraction(tasaCell);
                if (tasaNormalized is null)
                {
                    // Ambiguous or negative rate — abstain on this sub-check for this row only.
                    // Continue to Check 2 (do NOT return InsufficientData for the whole rule).
                }
                else
                {
                    var saldo = saldoCell.ParsedValue!.Value;
                    var expectedInteres = saldo * (tasaNormalized.Value / DaysPerYear) * dias.Value;
                    var diff1 = Math.Abs(expectedInteres - interesReported);
                    rowsChecked++;

                    var legalPasses1  = diff1 <= legalTolerance;
                    var tenantPasses1 = diff1 <= effectiveTolerance;

                    if (!tenantPasses1 && firstFailReason is null)
                    {
                        firstFailReason =
                            $"§16 row '{row.Label.RawText}' Check 1 (Intereses vs rate/días): " +
                            $"SaldoPendiente={saldo:F2}, Tasa(fraction)={tasaNormalized.Value:F6}, " +
                            $"días={dias.Value:F0}, expected={expectedInteres:F2}, " +
                            $"reported={interesReported:F2}, diff={diff1:F4} > tolerance={effectiveTolerance:F2}";
                        firstFailLocator     = interesCell.Locator;
                        firstFailLegalPasses = legalPasses1;
                    }
                }
            }

            // ---- Check 2: IVA ≈ Intereses × 0.16 ----
            {
                var expectedIva = Math.Abs(interesReported) * IvaRate;
                var diff2 = Math.Abs(expectedIva - Math.Abs(ivaReported));
                rowsChecked++;

                var legalPasses2  = diff2 <= legalTolerance;
                var tenantPasses2 = diff2 <= effectiveTolerance;

                if (!tenantPasses2 && firstFailReason is null)
                {
                    firstFailReason =
                        $"§16 row '{row.Label.RawText}' Check 2 (IVA vs Intereses): " +
                        $"IVA expected={expectedIva:F2}, reported={ivaReported:F2}, " +
                        $"diff={diff2:F4} > tolerance={effectiveTolerance:F2} " +
                        $"(formula: |Intereses|={Math.Abs(interesReported):F2} × {IvaRate})";
                    firstFailLocator     = ivaCell.Locator;
                    firstFailLegalPasses = legalPasses2;
                }
            }

            // Accumulate total §16 intereses for cross-check with §19.
            section16InteresTotal += Math.Abs(interesReported);
            hasCheckableInteresRows = true;
        }

        // ----------------------------------------------------------------
        // Check 3: Totals-tie cross-check against §19 "otras líneas" row
        // Best-effort: skip (abstain) when either side is absent or unclear.
        // ----------------------------------------------------------------
        if (firstFailReason is null && hasCheckableInteresRows)
        {
            var crossCheck = TryOtrasLineasCrossCheck(
                ctx, section16InteresTotal, effectiveTolerance, legalTolerance);
            if (crossCheck.Fails)
            {
                firstFailReason      = crossCheck.Reason!;
                firstFailLocator     = crossCheck.Locator;
                firstFailLegalPasses = crossCheck.LegalPasses;
            }
        }

        // ----------------------------------------------------------------
        // Aggregate verdict
        // ----------------------------------------------------------------
        if (firstFailReason is not null)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: string.Empty,
                    observed: firstFailReason,
                    toleranceApplied: effectiveTolerance,
                    locator: firstFailLocator ?? locator,
                    legalBaselineVerdict: firstFailLegalPasses
                        ? FindingVerdict.Pass
                        : FindingVerdict.Fail));
        }

        if (rowsChecked == 0)
        {
            return InsufficientData(
                "§16 table has no rows with sufficient data to verify " +
                "(all rows are NA/empty, low-confidence, have too few columns, or needed cells are absent). " +
                "NOTE: production §16 column-mapping accuracy is corpus-gated — no real §16 fixture available.");
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"{rowsChecked} check(s) verified across §16 rows. " +
                          "NOTE: production §16 column-mapping accuracy is corpus-gated — no real §16 fixture available.",
                toleranceApplied: effectiveTolerance,
                locator: locator,
                legalBaselineVerdict: FindingVerdict.Pass));
    }

    // -----------------------------------------------------------------------
    // Check 3: §19 "otras líneas" totals-tie cross-check
    // -----------------------------------------------------------------------

    private readonly struct CrossCheckOutcome
    {
        public bool Fails { get; init; }
        public string? Reason { get; init; }
        public FieldLocator? Locator { get; init; }
        public bool LegalPasses { get; init; }

        public static CrossCheckOutcome Skip() => default; // Fails = false
    }

    private CrossCheckOutcome TryOtrasLineasCrossCheck(
        VerificationContext ctx,
        decimal section16InteresTotal,
        decimal effectiveTolerance,
        decimal legalTolerance)
    {
        // Locate §19 table
        var table19 = ctx.StatementModel?.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section19Number);

        if (table19 is null || table19.Status != TableExtractionStatus.Extracted)
            return CrossCheckOutcome.Skip();

        // Find the §19 row that corresponds to "otras líneas de crédito"
        var otrasLineasRow = table19.Rows.FirstOrDefault(
            r => r.Label.RawText.Contains(OtrasLineasFragment, StringComparison.OrdinalIgnoreCase));

        if (otrasLineasRow is null)
            return CrossCheckOutcome.Skip();

        // §19 column [3] = Monto de intereses (see Section19InterestPerRowRule)
        const int ColSection19Monto = 3;

        if (otrasLineasRow.Values.Count <= ColSection19Monto)
            return CrossCheckOutcome.Skip();

        var montoCell = otrasLineasRow.Values[ColSection19Monto];

        if (IsNaOrEmpty(montoCell))
            return CrossCheckOutcome.Skip();

        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        if (!HasUsableValue(montoCell, confidenceThreshold))
            return CrossCheckOutcome.Skip(); // low confidence — abstain on this cross-check

        var section19Monto = Math.Abs(montoCell.ParsedValue!.Value);
        var diff = Math.Abs(section16InteresTotal - section19Monto);

        if (diff <= effectiveTolerance)
            return CrossCheckOutcome.Skip(); // passes

        var legalPasses = diff <= legalTolerance;

        return new CrossCheckOutcome
        {
            Fails = true,
            Reason =
                $"§16 Check 3 (totals-tie vs §19 otras-líneas row): " +
                $"§16 total Intereses={section16InteresTotal:F2} vs " +
                $"§19 '{otrasLineasRow.Label.RawText}' Monto={section19Monto:F2}, " +
                $"diff={diff:F4} > tolerance={effectiveTolerance:F2}",
            Locator = montoCell.Locator,
            LegalPasses = legalPasses
        };
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Attempts to derive the statement period day-count from <see cref="PeriodSummary"/>.
    /// Returns null when the day-count cannot be reliably determined; in that case
    /// Check 1 (Intereses vs rate/días) is skipped for all rows — never Fail.
    /// </summary>
    private static decimal? TryGetDays(VerificationContext ctx)
    {
        var ps = ctx.StatementModel?.PeriodSummary;
        if (ps is null)
            return null;

        // Prefer DayCount.ComputedSpanDays (authoritative computed value).
        if (ps.DayCount.ComputedSpanDays is > 0)
            return (decimal)ps.DayCount.ComputedSpanDays.Value;

        // Fall back to printed day count when extraction succeeded.
        if (ps.DayCountPrinted.Status == ExtractionStatus.Extracted
            && ps.DayCountPrinted.Value > 0)
        {
            return (decimal)ps.DayCountPrinted.Value;
        }

        return null;
    }

    /// <summary>Returns true when the cell is not applicable or empty (bank declared N/A).</summary>
    private static bool IsNaOrEmpty(TableCell cell) =>
        cell.Kind is CellKind.NotApplicable or CellKind.Empty;

    /// <summary>
    /// Returns true when the cell has a parseable numeric value at or above the
    /// confidence threshold.
    /// </summary>
    private static bool HasUsableValue(TableCell cell, double threshold) =>
        cell.ParsedValue is not null && cell.Confidence >= threshold;

    /// <summary>
    /// Normalizes the rate cell to a decimal fraction (e.g. 27.36 → 0.2736).
    /// Uses the same logic as <c>Section19InterestPerRowRule.NormalizeRateToFraction</c>.
    /// </summary>
    private static decimal? NormalizeRateToFraction(TableCell tasaCell)
    {
        var rawRate = tasaCell.ParsedValue!.Value;

        if (rawRate < 0m)
            return null; // invalid — caller abstains on sub-check

        if (rawRate == 0m)
            return 0m;

        if (tasaCell.Kind == CellKind.Rate)
        {
            // Domain contract: Rate cells carry a decimal fraction.
            // Exception: magnitude > 1.5 unambiguously signals a percentage number.
            return rawRate > RatePercentageThreshold ? rawRate / 100m : rawRate;
        }

        // Non-Rate cell: use RawText '%' signal, then magnitude.
        if (tasaCell.RawText.Contains('%', StringComparison.Ordinal))
            return rawRate / 100m;

        if (rawRate <= 1.0m)
            return rawRate; // clearly a fraction

        if (rawRate > RatePercentageThreshold)
            return rawRate / 100m; // almost certainly a percentage number

        // Ambiguous zone (1.0, 1.5] with no '%' — cannot determine scale; caller skips sub-check.
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

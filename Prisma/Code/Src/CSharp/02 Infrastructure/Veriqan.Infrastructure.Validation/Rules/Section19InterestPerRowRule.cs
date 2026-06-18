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
/// LAW-§19-INTERES: Validates each row of the §19 "Saldo sobre el que se calcularon
/// los intereses del periodo" table by recomputing the per-row interest amount and
/// verifying that the "Ordinarios" row rate equals §10's annual rate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (Acuerdo §19, per row):</b>
/// <c>MontoBankReported ≈ SaldoBase × (TasaAnualFraction / 360) × Días</c>
/// </para>
/// <para>
/// <b>Column semantic order (§19, 4 value cells per row):</b>
/// </para>
/// <list type="bullet">
///   <item>[0] SaldoBase — the balance on which interest was computed.</item>
///   <item>[1] Núm. de días — number of days in the charging period.</item>
///   <item>[2] Tasa de interés anual — annual rate.  May be a percentage number
///             (e.g. 27.36) or a fraction (e.g. 0.2736) depending on the 11.1 extractor.
///             The rule normalizes to fraction before the formula.</item>
///   <item>[3] Monto de intereses — the reported interest amount.</item>
/// </list>
/// <para>
/// <b>Rate normalization:</b> if the parsed rate value &gt; 1.5 it is treated as a
/// percentage number (e.g. 27.36) and divided by 100 before use. Values ≤ 1.5 are
/// treated as fractions (e.g. 0.2736). A negative raw rate → InsufficientData for that row.
/// </para>
/// <para>
/// <b>§10 cross-check:</b> the "Ordinarios" row's annual rate must equal
/// <c>PeriodSummary.Tasa</c> (both normalized to fraction) within a small epsilon
/// (0.0005 in fraction space ≈ 0.05 percentage-point). If either side is absent or
/// low-confidence this cross-check is silently skipped (abstain, not Fail).
/// </para>
/// <para>
/// <b>Aggregate verdict:</b>
/// <list type="bullet">
///   <item>Any row fails recompute beyond tolerance OR §10 cross-check fails → Fail.</item>
///   <item>No row could be checked (all NA / low-confidence) → InsufficientData.</item>
///   <item>Otherwise → Pass.</item>
/// </list>
/// </para>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail would halt a bank's billing run.
/// The rule MUST return <see cref="FindingVerdict.InsufficientData"/> on any missing or
/// low-confidence cell — never guess, never false-Fail.
/// </para>
/// </remarks>
internal sealed class Section19InterestPerRowRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section19Number = 19;
    private const int RequiredValueCellCount = 4;

    // Column indices within each §19 row's Values list.
    private const int ColSaldoBase = 0;
    private const int ColDias = 1;
    private const int ColTasa = 2;
    private const int ColMonto = 3;

    // Rate normalization: values above this threshold are almost certainly percentage numbers.
    // Used as a fallback when neither RawText '%' nor CellKind provides a signal.
    private const decimal RatePercentageThreshold = 1.5m;

    // Lower bound of the "ambiguous zone": values in (AmbiguousLow, RatePercentageThreshold]
    // with no '%' in RawText are genuinely ambiguous (could be 1.2% or 1.2 = 120%).
    // The rule returns null (InsufficientData) for this range.
    private const decimal AmbiguousLow = 1.0m;

    // Epsilon for the §10 rate cross-check (fraction space, ≈ 0.05 pct-pt).
    private const decimal RateEpsilon = 0.0005m;

    // Label fragment that identifies the "Ordinarios" interest-type row.
    private const string OrdinarioFragment = "Ordinarios";

    private readonly ILegalToleranceProvider _toleranceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="Section19InterestPerRowRule"/>.
    /// </summary>
    /// <param name="toleranceProvider">The legal tolerance provider.</param>
    public Section19InterestPerRowRule(ILegalToleranceProvider toleranceProvider)
    {
        _toleranceProvider = toleranceProvider
            ?? throw new ArgumentNullException(nameof(toleranceProvider));
    }

    /// <inheritdoc />
    public string CheckId => "LAW-§19-INTERES";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §19";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.TenantTightenableOnly;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Guard: tolerance must be registered before attempting to resolve.
        if (!_toleranceProvider.Has(CheckId))
            return InsufficientData($"No legal tolerance registered for {CheckId} — cannot evaluate.");

        // Resolve tolerances
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        // Guard: StatementModel must be present
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Guard: §19 table must be present and fully extracted
        var table19 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section19Number);

        if (table19 is null || table19.Status == TableExtractionStatus.SectionNotFound)
            return InsufficientData("§19 table is absent (SectionNotFound).");

        if (table19.Status != TableExtractionStatus.Extracted)
            return InsufficientData($"§19 table extraction status is {table19.Status} — not Extracted.");

        if (table19.Rows.Count == 0)
            return InsufficientData("§19 table has no rows.");

        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        var locator = table19.Locator;

        // ----------------------------------------------------------------
        // Per-row recompute pass
        // ----------------------------------------------------------------

        var rowsChecked = 0;
        var firstFailReason = (string?)null;
        var firstFailLocator = (FieldLocator?)null;
        var firstFailLegalPasses = true;

        foreach (var row in table19.Rows)
        {
            if (row.Values.Count < RequiredValueCellCount)
                continue; // malformed row — skip silently

            var saldoCell = row.Values[ColSaldoBase];
            var diasCell  = row.Values[ColDias];
            var tasaCell  = row.Values[ColTasa];
            var montoCell = row.Values[ColMonto];

            // All 4 cells NA → this interest type is not applicable for this statement.
            // Skip without counting it as a checked row.
            if (IsNaOrEmpty(saldoCell) && IsNaOrEmpty(diasCell) &&
                IsNaOrEmpty(tasaCell)  && IsNaOrEmpty(montoCell))
            {
                continue;
            }

            // Partially NA: one or more cells are NA while others are not.
            // The bank has declared the row mixed-applicable — skip the row
            // (treating partial NA the same as full NA is intentional to avoid false-Fail).
            if (IsNaOrEmpty(saldoCell) || IsNaOrEmpty(diasCell) ||
                IsNaOrEmpty(tasaCell)  || IsNaOrEmpty(montoCell))
            {
                continue;
            }

            // A cell is structurally present but has no usable value or low confidence.
            // We have some cells with data — this is a partial extraction failure.
            // Abstain (InsufficientData) for the whole rule — do not Fail.
            if (!HasUsableValue(saldoCell, confidenceThreshold) ||
                !HasUsableValue(diasCell, confidenceThreshold)  ||
                !HasUsableValue(tasaCell, confidenceThreshold)  ||
                !HasUsableValue(montoCell, confidenceThreshold))
            {
                return InsufficientData(
                    $"§19 row '{row.Label.RawText}' has a cell with missing or " +
                    $"low-confidence value (confidence threshold={confidenceThreshold:F2}).");
            }

            // All four cells are usable — recompute.
            var saldoBase    = saldoCell.ParsedValue!.Value;
            var dias         = diasCell.ParsedValue!.Value;
            var montoReported = montoCell.ParsedValue!.Value;
            var tasaRaw      = tasaCell.ParsedValue!.Value;

            // Normalize tasa to fraction space using cell metadata (RawText / Kind)
            // to determine the scale, rather than the magnitude heuristic alone.
            var tasaFraction = NormalizeRateToFraction(tasaCell);
            if (tasaFraction is null)
            {
                // Negative, ambiguous, or otherwise unresolvable rate — abstain.
                return InsufficientData(
                    $"§19 row '{row.Label.RawText}' has an unresolvable tasa value {tasaRaw} " +
                    $"(raw: \"{tasaCell.RawText}\") — cannot determine percentage vs fraction scale; abstaining.");
            }

            var expected = saldoBase * (tasaFraction.Value / 360m) * dias;
            var diff = Math.Abs(expected - montoReported);
            rowsChecked++;

            var legalPasses  = diff <= legalTolerance;
            var tenantPasses = diff <= effectiveTolerance;

            if (!tenantPasses && firstFailReason is null)
            {
                firstFailReason =
                    $"§19 row '{row.Label.RawText}': " +
                    $"expected={expected:F2}, reported={montoReported:F2}, diff={diff:F4} " +
                    $"> tolerance={effectiveTolerance:F2}";
                firstFailLocator  = montoCell.Locator;
                firstFailLegalPasses = legalPasses;
            }
        }

        // ----------------------------------------------------------------
        // §10 cross-check: "Ordinarios" annual rate must match PeriodSummary.Tasa
        // ----------------------------------------------------------------
        if (firstFailReason is null) // only run if no row-recompute Fail yet
        {
            var crossCheck = TryOrdinariosCrossCheck(table19, ctx, confidenceThreshold);
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
                "§19 table has no rows with sufficient data to verify " +
                "(all rows are NA/empty, low-confidence, or partially missing).");
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"{rowsChecked} row(s) verified",
                toleranceApplied: effectiveTolerance,
                locator: locator,
                legalBaselineVerdict: FindingVerdict.Pass));
    }

    // -----------------------------------------------------------------------
    // §10 cross-check helper
    // -----------------------------------------------------------------------

    private readonly struct CrossCheckOutcome
    {
        public bool Fails { get; init; }
        public string? Reason { get; init; }
        public FieldLocator? Locator { get; init; }
        public bool LegalPasses { get; init; }

        public static CrossCheckOutcome Skip() => default; // Fails = false
    }

    private CrossCheckOutcome TryOrdinariosCrossCheck(
        FinancialTable table19,
        VerificationContext ctx,
        double confidenceThreshold)
    {
        // Find the Ordinarios row
        var ordinarios = table19.Rows.FirstOrDefault(
            r => r.Label.RawText.Contains(OrdinarioFragment, StringComparison.OrdinalIgnoreCase));

        if (ordinarios is null || ordinarios.Values.Count < RequiredValueCellCount)
            return CrossCheckOutcome.Skip();

        var tasaCell = ordinarios.Values[ColTasa];

        // Skip if NA, empty, or low-confidence
        if (IsNaOrEmpty(tasaCell) || !HasUsableValue(tasaCell, confidenceThreshold))
            return CrossCheckOutcome.Skip();

        var tasaFraction = NormalizeRateToFraction(tasaCell);
        if (tasaFraction is null)
            return CrossCheckOutcome.Skip(); // negative or ambiguous rate — abstain

        // Check PeriodSummary.Tasa (stored as fraction, e.g. 0.2736 for 27.36%)
        var periodTasa = ctx.StatementModel?.PeriodSummary?.Tasa;
        if (periodTasa is null || periodTasa.Status != ExtractionStatus.Extracted)
            return CrossCheckOutcome.Skip();

        var periodTasaFraction = periodTasa.Value;
        var rateDiff = Math.Abs(tasaFraction.Value - periodTasaFraction);

        if (rateDiff <= RateEpsilon)
            return CrossCheckOutcome.Skip(); // cross-check passes

        // Cross-check fails
        return new CrossCheckOutcome
        {
            Fails = true,
            Reason =
                $"§10 cross-check: Ordinarios tasa={tasaFraction.Value:F6} " +
                $"(normalized from raw=\"{tasaCell.RawText}\") vs §10 tasa={periodTasaFraction:F6}, " +
                $"diff={rateDiff:F6} > epsilon={RateEpsilon}",
            Locator = tasaCell.Locator,
            LegalPasses = false
        };
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

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
    /// Scale is determined from the cell's <see cref="TableCell.Kind"/>,
    /// <see cref="TableCell.RawText"/>, and magnitude, in priority order:
    /// <list type="bullet">
    ///   <item>
    ///     <b>CellKind.Rate, value ≤ AmbiguousLow (1.0)</b> → domain contract guarantees
    ///     the value is already a decimal fraction; use directly.
    ///     The <c>RawText</c> may carry a <c>%</c> sign (e.g. "27.36%") but the extractor
    ///     has already normalised; do NOT divide again.
    ///   </item>
    ///   <item>
    ///     <b>CellKind.Rate, value &gt; RatePercentageThreshold (1.5)</b> → the extractor
    ///     tagged the cell as Rate but stored the percentage number (e.g. 27.36 instead of
    ///     0.2736); magnitude unambiguously signals a percentage — divide by 100.
    ///   </item>
    ///   <item>
    ///     <b>CellKind.Rate, value in (AmbiguousLow, RatePercentageThreshold]</b> → value
    ///     could be 1.2 = 120% rate (unlikely but possible for some special-rate products),
    ///     or 1.2% as a fraction from a mal-normalised extractor. Given domain contract, treat
    ///     as a fraction and use directly.
    ///   </item>
    ///   <item>
    ///     <b>Kind != CellKind.Rate AND <c>RawText</c> contains <c>'%'</c></b> →
    ///     the extracted text is a percentage number not yet divided; divide by 100.
    ///   </item>
    ///   <item>
    ///     <b>Kind != CellKind.Rate, no <c>'%'</c>, value ≤ AmbiguousLow</b> → fraction.
    ///   </item>
    ///   <item>
    ///     <b>Kind != CellKind.Rate, no <c>'%'</c>, value &gt; RatePercentageThreshold</b>
    ///     → percentage number; divide by 100.
    ///   </item>
    ///   <item>
    ///     <b>Kind != CellKind.Rate, no <c>'%'</c>, ambiguous zone (1.0, 1.5]</b> →
    ///     returns <see langword="null"/> (caller returns InsufficientData).
    ///   </item>
    ///   <item>
    ///     Negative rate → returns <see langword="null"/> (invalid; caller abstains).
    ///   </item>
    /// </list>
    /// </summary>
    private static decimal? NormalizeRateToFraction(TableCell tasaCell)
    {
        var rawRate = tasaCell.ParsedValue!.Value; // caller guarantees ParsedValue is not null

        if (rawRate < 0m)
            return null; // invalid — caller abstains

        if (rawRate == 0m)
            return 0m;

        if (tasaCell.Kind == CellKind.Rate)
        {
            // Domain contract: Rate cells carry the value as a decimal fraction.
            // Exception: magnitude > 1.5 unambiguously reveals that the extractor stored
            // the percentage number rather than dividing — normalise defensively.
            if (rawRate > RatePercentageThreshold)
                return rawRate / 100m;

            // Value ≤ 1.5: either a confirmed fraction (≤ 1.0) or the ambiguous zone
            // (1.0, 1.5] — for Rate cells the domain contract applies, trust it.
            return rawRate;
        }

        // Non-Rate cell: determine scale from RawText first, then magnitude.

        // Explicit percentage signal from the printed text.
        if (tasaCell.RawText.Contains('%', StringComparison.Ordinal))
            return rawRate / 100m;

        // No '%' present — use the value range to decide.
        if (rawRate <= AmbiguousLow)
            return rawRate; // clearly a fraction (e.g. 0.2736)

        if (rawRate > RatePercentageThreshold)
            return rawRate / 100m; // almost certainly a percentage number (e.g. 27.36)

        // Non-Rate cell, ambiguous zone (1.0, 1.5]: no '%', could be 1.2% or 1.2 = 120%.
        // Return null so the caller abstains rather than guessing.
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

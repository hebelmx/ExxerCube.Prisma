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
/// figures: IVA ≈ Interés × 0.16 (Mexican IVA rate), and interest is consistent with
/// the reported rate/days when those cells are present.
/// </para>
/// <para>
/// <b>Conditional-section behaviour (cardinal rule — preventive gate):</b>
/// <list type="bullet">
///   <item>
///     §16 <see cref="TableExtractionStatus.SectionNotFound"/> → <b>Pass</b> with an
///     "N/A — section not applicable" note.  The section is legitimately absent
///     when the cardholder has no other credit lines; this is the normal path for
///     all current fixtures.  Never Fail for a legitimately absent conditional section.
///   </item>
///   <item>
///     §16 <see cref="TableExtractionStatus.Indeterminate"/> or
///     <see cref="TableExtractionStatus.NoRowsParsed"/> → <b>InsufficientData</b>.
///     The section was detected but could not be reliably read.
///   </item>
///   <item>
///     §16 <see cref="TableExtractionStatus.Extracted"/> → per-row arithmetic
///     verification (see below).
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Per-row arithmetic (when §16 is Extracted):</b>
/// The §16 table structure VARIES BY PRODUCT.  The rule is defensive:
/// <list type="bullet">
///   <item>
///     Rows with ≥ 2 Amount cells where the second can be interpreted as IVA
///     on the first (Interest): verify <c>|IVA − Interés × 0.16| ≤ tolerance</c>.
///   </item>
///   <item>
///     Any row whose needed cells are missing, NA, or low-confidence →
///     skip the row (contributing InsufficientData only if no row can be checked).
///   </item>
///   <item>
///     A confident mismatch beyond tolerance → <b>Fail</b> citing §16.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Aggregate verdict:</b>
/// <list type="bullet">
///   <item>Any row fails → Fail (first failure reported).</item>
///   <item>No row could be checked → InsufficientData.</item>
///   <item>All checkable rows reconcile → Pass.</item>
/// </list>
/// </para>
/// <para>
/// <b>Preventive gate semantics:</b> a false Fail would halt a bank's billing run.
/// The rule MUST return <see cref="FindingVerdict.InsufficientData"/> on any missing
/// or low-confidence cell — never guess, never false-Fail.
/// </para>
/// </remarks>
internal sealed class Section16OtherCreditLinesRule : IVecValidationRule
{
    private const string Version = "1.0.0";
    private const int Section16Number = 16;

    /// <summary>Mexican IVA rate (16%).</summary>
    private const decimal IvaRate = 0.16m;

    /// <summary>
    /// Minimum number of Amount-kind value cells required to attempt an IVA check.
    /// Col[0] = Interés, Col[1] = IVA.
    /// </summary>
    private const int MinValueCellsForIvaCheck = 2;

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

        // Guard: StatementModel must be present.
        if (ctx.StatementModel is null)
            return InsufficientData("StatementModel is not populated.");

        // Locate the §16 FinancialTable.
        var table16 = ctx.StatementModel.FinancialTables
            .FirstOrDefault(t => t.SectionNumber == Section16Number);

        // ----------------------------------------------------------------
        // Conditional-section gate:
        // SectionNotFound means the cardholder has no other credit lines —
        // this is NOT a defect. Return Pass with N/A note (matches E10
        // conditional-rule pattern for §23, §25 when trigger is absent).
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

        // Extracted but no rows — cannot check anything.
        if (table16.Rows.Count == 0)
            return InsufficientData("§16 table has no rows after extraction.");

        // Resolve tolerances.
        var legalTolerance = _toleranceProvider.For(CheckId).Resolve(null).EffectiveValue;
        var effectiveTolerance = ctx.TenantProfile?.GetEffectiveTolerance(CheckId, legalTolerance)
            ?? legalTolerance;

        var confidenceThreshold = ctx.TenantProfile?.MinFieldConfidence
            ?? TenantProfile.LegalMinFieldConfidenceDefault;

        var locator = table16.Locator;

        // ----------------------------------------------------------------
        // Per-row arithmetic pass
        // ----------------------------------------------------------------

        var rowsChecked = 0;
        var firstFailReason = (string?)null;
        var firstFailLocator = (FieldLocator?)null;
        var firstFailLegalPasses = true;

        foreach (var row in table16.Rows)
        {
            // §16 structure varies by product — only attempt a check when
            // we have at least 2 Amount-kind value cells (Interés + IVA).
            var amountCells = row.Values
                .Where(c => c.Kind == CellKind.Amount)
                .ToList();

            if (amountCells.Count < MinValueCellsForIvaCheck)
                continue; // row shape doesn't support IVA check — skip silently

            var interesCell = amountCells[0];
            var ivaCell     = amountCells[1];

            // All-NA row: the bank declared this row not applicable.  Skip.
            if (IsNaOrEmpty(interesCell) && IsNaOrEmpty(ivaCell))
                continue;

            // One cell NA, the other not: partial-NA → abstain (don't Fail).
            if (IsNaOrEmpty(interesCell) || IsNaOrEmpty(ivaCell))
                continue;

            // Low-confidence: abstain for this row — contribute InsufficientData
            // only if no other row can be checked.
            if (!HasUsableValue(interesCell, confidenceThreshold))
            {
                return InsufficientData(
                    $"§16 row '{row.Label.RawText}': Interés cell has missing or " +
                    $"low-confidence value (confidence={interesCell.Confidence:F2}, " +
                    $"threshold={confidenceThreshold:F2}).");
            }

            if (!HasUsableValue(ivaCell, confidenceThreshold))
            {
                return InsufficientData(
                    $"§16 row '{row.Label.RawText}': IVA cell has missing or " +
                    $"low-confidence value (confidence={ivaCell.Confidence:F2}, " +
                    $"threshold={confidenceThreshold:F2}).");
            }

            // Both cells are confident and parseable — verify IVA ≈ Interés × 0.16.
            var interes      = interesCell.ParsedValue!.Value;
            var ivaReported  = ivaCell.ParsedValue!.Value;
            var ivaExpected  = Math.Abs(interes) * IvaRate; // use absolute value (interest may be signed)
            var diff         = Math.Abs(ivaExpected - Math.Abs(ivaReported));

            rowsChecked++;

            var legalPasses  = diff <= legalTolerance;
            var tenantPasses = diff <= effectiveTolerance;

            if (!tenantPasses && firstFailReason is null)
            {
                firstFailReason =
                    $"§16 row '{row.Label.RawText}': " +
                    $"IVA expected={ivaExpected:F2} (Interés={interes:F2} × 0.16), " +
                    $"reported={ivaReported:F2}, diff={diff:F4} > tolerance={effectiveTolerance:F2}";
                firstFailLocator  = ivaCell.Locator;
                firstFailLegalPasses = legalPasses;
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
                "§16 table was extracted but no rows had sufficient data to verify " +
                "(all rows are NA/empty, low-confidence, or have too few Amount cells).");
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"{rowsChecked} row(s) verified: IVA-vs-Interés arithmetic reconciles within tolerance.",
                toleranceApplied: effectiveTolerance,
                locator: locator,
                legalBaselineVerdict: FindingVerdict.Pass));
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

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

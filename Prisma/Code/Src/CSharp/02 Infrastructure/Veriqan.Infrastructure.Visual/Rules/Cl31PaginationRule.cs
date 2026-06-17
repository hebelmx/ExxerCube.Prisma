using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// CL-31: Verifies that the pagination labels ("N de M") are consistent across all labeled pages
/// and that the declared total (M) matches the actual document page count.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — structural inspection only.
/// </para>
/// <para>
/// <b>Pass conditions:</b>
/// <list type="bullet">
///   <item>All labeled pages agree on the same total (M).</item>
///   <item>M equals <see cref="StatementModel.PageCount"/>.</item>
///   <item>The current-page numbers on labeled pages form a unique sequence with no duplicates or gaps relative to their count.</item>
/// </list>
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="StatementModel.Pages"/> is empty (per-page extraction did not run).</item>
///   <item>No pages carry a pagination label (pattern "N de M" not found on any page).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Cl31PaginationRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-31";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;

        if (model is null || model.Pages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No per-page inspection data available — extraction stage did not run."));
        }

        // Collect pages that have pagination labels.
        var labeledPages = model.Pages
            .Where(p => p.PaginationCurrent.HasValue && p.PaginationTotal.HasValue)
            .ToList();

        if (labeledPages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No pagination labels ('N de M') found on any page."));
        }

        // Check: all labeled pages agree on the same total.
        var distinctTotals = labeledPages
            .Select(p => p.PaginationTotal!.Value)
            .Distinct()
            .ToList();

        if (distinctTotals.Count > 1)
        {
            var totalsList = string.Join(", ", distinctTotals);
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: "All pagination labels agree on the same total (M)",
                    observed: $"Conflicting totals found: {totalsList}.",
                    locator: labeledPages[0].Locator));
        }

        var declaredTotal = distinctTotals[0];

        // Check: declared total matches actual page count.
        if (declaredTotal != model.PageCount)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"Declared total M={declaredTotal} equals document page count {model.PageCount}",
                    observed: $"Declared M={declaredTotal} does not match document page count {model.PageCount}.",
                    locator: labeledPages[0].Locator));
        }

        // Check: current-page numbers are unique (no duplicates).
        var currents = labeledPages.Select(p => p.PaginationCurrent!.Value).ToList();
        var duplicates = currents
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            var dupList = string.Join(", ", duplicates);
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: "No duplicate current-page numbers in pagination labels",
                    observed: $"Duplicate pagination current values found: {dupList}.",
                    locator: labeledPages[0].Locator));
        }

        // Check: labeled current-page numbers form a contiguous sequence (no gaps).
        // Convention: the check applies to the set of labeled pages only.  If not every
        // page carries a label the span [min..max] must still be gap-free among the labeled
        // subset, because a missing label is already surfaced by the total-mismatch check
        // above.  Example: {1, 3, 4} de 4 → Max−Min+1 (4) ≠ Count (3) → gap at 2 → FAIL.
        var minCurrent = currents.Min();
        var maxCurrent = currents.Max();
        var expectedSpan = maxCurrent - minCurrent + 1;
        if (expectedSpan != currents.Count)
        {
            var currentsList = string.Join(", ", currents.OrderBy(c => c));
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"Labeled current-page numbers form a contiguous sequence (no gaps) between {minCurrent} and {maxCurrent}",
                    observed: $"Gap detected in labeled pagination sequence: found [{currentsList}] but expected {expectedSpan} contiguous value(s).",
                    locator: labeledPages[0].Locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Pass(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                observed: $"Pagination consistent: {labeledPages.Count} labeled page(s), all agree M={declaredTotal}, matches document page count, sequence contiguous."));
    }
}

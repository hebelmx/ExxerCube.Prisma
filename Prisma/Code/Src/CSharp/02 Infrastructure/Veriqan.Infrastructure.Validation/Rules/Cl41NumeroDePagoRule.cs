using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-41: Validates that the "Número de pago" (payment sequence counter) for each
/// MSI installment increments by exactly 1 each month.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule (FR-7):</b> For each open installment purchase (except future-dated purchases):
/// <c>CurrentNumeroDePago == PriorNumeroDePago + 1</c>.
/// Example: a 12-month plan in month 5 of 12 in the prior period should show 6 of 12 in the
/// current statement.
/// </para>
/// <para>
/// <b>Status:</b> This rule always returns
/// <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> because the statement's
/// COMPRAS-A-MESES installment table rows are not yet extracted.
/// The formula is documented here so the intent is preserved; the rule will emit real
/// PASS/FAIL verdicts once a dedicated extraction story adds installment-row extraction.
/// Note: Story 4.4 delivered DESGLOSE movement extraction, not COMPRAS-A-MESES table
/// extraction — this work item is currently unscheduled.
/// </para>
/// <note type="todo">TODO (unscheduled future work): Replace InsufficientData with real
/// per-installment NumeroDePago increment check once <c>PeriodSummary.InstallmentRows</c>
/// is populated by a future extraction story (currently unscheduled; Story 4.4 did not
/// cover this). Future-dated purchases (NumeroDePago == 0 or a sentinel future flag)
/// must be excluded from this check.</note>
/// <para>
/// <b>Reference data shape:</b>
/// <c>VerificationContext.PriorStatement?.Installments</c> — each <c>InstallmentEntry</c>
/// carries NumeroDePago and TotalPagos for the prior period.
/// </para>
/// </remarks>
internal sealed class Cl41NumeroDePagoRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-41";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §13";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // TODO (unscheduled future work): The statement's COMPRAS-A-MESES installment table rows
        // are not yet extracted. Story 4.4 delivered DESGLOSE movement extraction — not
        // COMPRAS-A-MESES table extraction. When a future story populates
        // PeriodSummary.InstallmentRows, replace this with:
        //   For each currentRow in ps.InstallmentRows (excluding future-dated entries):
        //     var prior = ctx.PriorStatement?.Installments
        //                     .SingleOrDefault(i => i.PurchaseId == currentRow.PurchaseId);
        //     if (prior is null || prior.NumeroDePago is null) → InsufficientData for that purchase
        //     expected = prior.NumeroDePago.Value + 1
        //     Compare currentRow.NumeroDePago vs expected (exact integer match, no tolerance).
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "COMPRAS-A-MESES installment-table extraction is not yet implemented (unscheduled future work). " +
                        "CL-41 requires per-installment NumeroDePago rows not yet available."));
    }
}

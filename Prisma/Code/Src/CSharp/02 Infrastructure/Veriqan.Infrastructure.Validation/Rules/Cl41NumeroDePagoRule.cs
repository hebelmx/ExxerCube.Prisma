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
/// <b>Status (Story 4.3):</b> This rule always returns
/// <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> because the statement's
/// COMPRAS-A-MESES installment table rows are not extracted until Story 4.4.
/// The formula is documented here so the intent is preserved; the rule will emit real
/// PASS/FAIL verdicts once Story 4.4 adds installment-row extraction.
/// </para>
/// <note type="todo">TODO(4.4): Replace InsufficientData with real per-installment
/// NumeroDePago increment check once <c>PeriodSummary.InstallmentRows</c> is populated
/// by Story 4.4. Future-dated purchases (NumeroDePago == 0 or a sentinel future flag)
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
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // TODO(4.4): The statement's COMPRAS-A-MESES installment table rows are not extracted yet.
        // When Story 4.4 populates PeriodSummary.InstallmentRows, replace this with:
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
                reason: "COMPRAS-A-MESES installment table extraction is pending (Story 4.4). " +
                        "CL-41 requires per-installment NumeroDePago rows not yet available."));
    }
}

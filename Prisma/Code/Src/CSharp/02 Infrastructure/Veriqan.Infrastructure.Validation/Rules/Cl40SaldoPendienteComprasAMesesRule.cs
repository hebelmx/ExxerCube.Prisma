using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-40: Validates "Saldo pendiente" for each open MSI (compras a meses sin intereses) purchase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula (FR-7):</b> For each open installment purchase:
/// <c>CurrentSaldoPendiente = PriorSaldoPendiente − PagoRequerido</c>
/// where <c>PriorSaldoPendiente</c> and <c>PagoRequerido</c> come from
/// <c>Bundle.PriorStatements[accountRef].Installments[purchaseId]</c>.
/// </para>
/// <para>
/// <b>Status:</b> This rule always returns
/// <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> because the statement's
/// COMPRAS-A-MESES installment table rows are not yet extracted.
/// The formula and reference-data path are documented here so the intent is preserved;
/// the rule will emit real PASS/FAIL verdicts once a dedicated extraction story adds
/// installment-row extraction. Note: Story 4.4 delivered DESGLOSE movement extraction,
/// not COMPRAS-A-MESES table extraction — this work item is currently unscheduled.
/// </para>
/// <note type="todo">TODO (unscheduled future work): Replace InsufficientData with real
/// per-installment comparison once <c>PeriodSummary.InstallmentRows</c> is populated by
/// a future extraction story (currently unscheduled; Story 4.4 did not cover this).
/// Each row carries PurchaseId, SaldoPendiente and NumeroDePago for matching against
/// <c>PriorStatement.Installments</c>.</note>
/// <para>
/// <b>Reference data shape:</b>
/// <c>VerificationContext.PriorStatement?.Installments</c> — each <c>InstallmentEntry</c> has
/// PurchaseId, SaldoPendiente, PagoRequerido, NumeroDePago, TotalPagos.
/// </para>
/// </remarks>
internal sealed class Cl40SaldoPendienteComprasAMesesRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-40";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §13";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // TODO (unscheduled future work): The statement's COMPRAS-A-MESES installment table rows
        // are not yet extracted. Story 4.4 delivered DESGLOSE movement extraction — not
        // COMPRAS-A-MESES table extraction. When a future story populates
        // PeriodSummary.InstallmentRows, replace this with:
        //   For each currentRow in ps.InstallmentRows:
        //     var prior = ctx.PriorStatement?.Installments
        //                     .SingleOrDefault(i => i.PurchaseId == currentRow.PurchaseId);
        //     if (prior is null) → InsufficientData for that purchase
        //     var computedSaldo = prior.SaldoPendiente - prior.PagoRequerido;
        //     Compare currentRow.SaldoPendiente vs computedSaldo within CurrencyToleranceMxn.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "COMPRAS-A-MESES installment-table extraction is not yet implemented (unscheduled future work). " +
                        "CL-40 requires per-installment SaldoPendiente rows not yet available."));
    }
}

using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-23: Validates that "Saldo cargos a meses" equals the sum of "Saldo pendiente" amounts
/// from the COMPRAS A MESES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula:</b>
/// <c>SaldoCargosAMeses = Σ SaldoPendiente of rows in COMPRAS-A-MESES table</c>.
/// </para>
/// <para>
/// <b>Status:</b> Always returns <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>
/// because the COMPRAS-A-MESES installment-table extraction is not yet implemented.
/// </para>
/// <note type="todo">TODO (unscheduled future work): Replace InsufficientData with a real
/// COMPRAS-A-MESES sum once <c>PeriodSummary.ComprasAMeses</c> (installment rows with
/// SaldoPendiente) is populated by a dedicated extraction story. Note: Story 4.4 delivered
/// DESGLOSE movement extraction, not COMPRAS-A-MESES table extraction; this work item
/// has no assigned story and is currently unscheduled.</note>
/// </remarks>
internal sealed class Cl23SaldoCargosAMesesSumaComprasRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-23";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // TODO (unscheduled future work): COMPRAS-A-MESES installment-table rows are not yet extracted.
        // Story 4.4 delivered DESGLOSE movement extraction — not COMPRAS-A-MESES table extraction.
        // When a future story populates PeriodSummary.ComprasAMeses, replace this with:
        //   var computed = ps.ComprasAMeses.Sum(r => r.SaldoPendiente);
        //   Compare to ps.SaldoCargosAMeses within tolerance.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "COMPRAS-A-MESES installment-table extraction is not yet implemented (unscheduled future work). " +
                        "CL-23 requires individual saldo-pendiente row detail not yet available."));
    }
}

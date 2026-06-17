using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-20: Validates that "Pagos y abonos" equals the sum of all charge/credit amounts in
/// the DESGLOSE DE OPERACIONES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula:</b> <c>PagosYAbonos = Σ amount of all credit rows in DESGLOSE table</c>.
/// </para>
/// <para>
/// <b>Status (Story 4.2):</b> Always returns <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>
/// because DESGLOSE table row extraction is not yet implemented.
/// </para>
/// <note type="todo">TODO(4.4): Replace InsufficientData path with real DESGLOSE sum comparison
/// once <c>PeriodSummary.DesgloseOperaciones</c> (all credit/charge rows) is populated by Story 4.4.</note>
/// </remarks>
internal sealed class Cl20PagosYAbonosSumaDesgloseRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-20";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // TODO(4.4): DESGLOSE table rows are not yet extracted.
        // When Story 4.4 populates PeriodSummary.DesgloseOperaciones, replace this with:
        //   var creditRows = ps.DesgloseOperaciones.Where(r => r.IsCredit);
        //   var computed = creditRows.Sum(r => r.Amount);
        //   Compare to ps.PagosYAbonos within tolerance.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "DESGLOSE table extraction is pending (Story 4.4). " +
                        "CL-20 requires all charge/credit row detail not yet available."));
    }
}

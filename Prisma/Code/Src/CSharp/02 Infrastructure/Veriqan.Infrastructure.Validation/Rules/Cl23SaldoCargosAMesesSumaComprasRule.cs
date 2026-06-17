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
/// <b>Status (Story 4.2):</b> Always returns <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>
/// because the COMPRAS-A-MESES table extraction is not yet implemented.
/// </para>
/// <note type="todo">TODO(4.4): Replace InsufficientData path with real COMPRAS-A-MESES sum
/// once <c>PeriodSummary.ComprasAMeses</c> (installment rows with SaldoPendiente) is
/// populated by Story 4.4.</note>
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

        // TODO(4.4): COMPRAS-A-MESES table rows are not yet extracted.
        // When Story 4.4 populates PeriodSummary.ComprasAMeses, replace this with:
        //   var computed = ps.ComprasAMeses.Sum(r => r.SaldoPendiente);
        //   Compare to ps.SaldoCargosAMeses within tolerance.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "COMPRAS-A-MESES table extraction is pending (Story 4.4). " +
                        "CL-23 requires individual saldo-pendiente row detail not yet available."));
    }
}

using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-18: Validates that "Cargos regulares" equals the sum of non-MSI amounts in the
/// DESGLOSE DE OPERACIONES table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formula:</b> <c>CargosRegulares = Σ amount of non-MSI rows in DESGLOSE table</c>.
/// </para>
/// <para>
/// <b>Status (Story 4.2):</b> This rule always returns <see cref="Domain.Enums.FindingVerdict.InsufficientData"/>
/// because the DESGLOSE table row extraction is not yet implemented.  The formula is encoded
/// here in the XML documentation so the intent is recorded; the rule will emit real
/// PASS/FAIL verdicts once Story 4.4 adds DESGLOSE extraction.
/// </para>
/// <note type="todo">TODO(4.4): Replace InsufficientData path with real DESGLOSE sum comparison
/// once <c>PeriodSummary.DesgloseOperaciones</c> (non-MSI rows) is populated by Story 4.4.</note>
/// </remarks>
internal sealed class Cl18CargosRegularesSumaDesgloseRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-18";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // TODO(4.4): DESGLOSE table rows are not yet extracted.
        // When Story 4.4 populates PeriodSummary.DesgloseOperaciones, replace this with:
        //   var nonMsiRows = ps.DesgloseOperaciones.Where(r => !r.IsMsi);
        //   var computed = nonMsiRows.Sum(r => r.Amount);
        //   Compare to ps.CargosRegularesNoMeses (or similar field).
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: "DESGLOSE table extraction is pending (Story 4.4). " +
                        "CL-18 requires non-MSI row detail not yet available."));
    }
}

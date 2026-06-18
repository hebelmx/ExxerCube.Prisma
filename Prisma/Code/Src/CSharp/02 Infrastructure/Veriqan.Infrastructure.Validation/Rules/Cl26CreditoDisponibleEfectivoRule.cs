using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-26: Validates that "Crédito disponible para disposiciones de efectivo" equals
/// "Crédito disponible" within ±$0.50 MXN.
/// </summary>
/// <remarks>
/// <para>
/// Both values appear in the NIVEL-DE-USO block of the statement.
/// Per the VEC checklist, the efectivo line must replicate the total crédito disponible.
/// </para>
/// <para>
/// <b>Current status: always InsufficientData (owner ruling — adversarial review finding).</b>
/// The statement extractor captures only a single <see cref="PeriodSummary.CreditoDisponible"/>
/// field (the first occurrence wins). The "crédito disponible para disposiciones de efectivo"
/// line is a distinct, separately printed value that is not yet extracted into its own field.
/// Comparing <c>CreditoDisponible</c> to itself is a tautology — it can never Fail — and
/// therefore emits a misleading Pass. This rule is changed to return InsufficientData until
/// a dedicated <c>CreditoDisponibleEfectivo</c> field is added to the extractor and the
/// two values can be genuinely compared.
/// </para>
/// <note type="todo">
/// TODO (unscheduled): Add a dedicated <c>PeriodSummary.CreditoDisponibleEfectivo</c>
/// extraction field that captures the "crédito disponible para disposiciones de efectivo"
/// line separately from <c>CreditoDisponible</c>. Once both fields are populated,
/// update this rule to compare them within <c>ToleranceConfig.CurrencyToleranceMxn</c>.
/// The fixture shows the efectivo line at Y≈128 and the total disponible at Y≈139.
/// </note>
/// </remarks>
internal sealed class Cl26CreditoDisponibleEfectivoRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "CL-26";

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

        // The separate "crédito disponible para disposiciones de efectivo" value is not yet
        // extracted into its own field. Comparing CreditoDisponible to itself would be a
        // tautology (always Pass, never Fail) — see class-level XML doc for owner ruling.
        return InsufficientData(
            "The separate 'crédito disponible para disposiciones de efectivo' value is not " +
            "extracted — cannot validate equality; field extraction is a future (unscheduled) enhancement.");
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

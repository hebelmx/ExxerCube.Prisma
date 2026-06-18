using System;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§13-TRANSFERENCIA: Verifies that, when §13 <i>Nivel de uso</i> is present and applicable,
/// the <i>crédito disponible para transferencia de saldo de otras tarjetas</i> field is shown.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo relativo al formato de estado de cuenta estandarizado
/// de tarjeta de crédito para personas físicas</i> (DOF 29-Dec-2022, mandatory since 17-Oct-2024),
/// §13 <i>Nivel de uso</i>. Among the "nivel de uso" data points, the Acuerdo mandates explicit
/// disclosure of the credit available for balance transfers from other cards when that credit line
/// is offered by the issuer.
/// </para>
/// <para>
/// <b>"When applicable" determination:</b> §13 is treated as applicable when
/// <see cref="DetectedSection.IsApplicable"/> is true in the sections map (i.e. the section
/// detection pass determined the section should be present). When §13 is present in the PDF,
/// the rule additionally checks for the transferencia field label. This avoids false-Fails for
/// issuers that do not offer balance-transfer credit lines — in those cases §13 itself would
/// be absent or not applicable.
/// </para>
/// <para>
/// <b>Classification:</b> <see cref="RuleClassification.BaselineLocked"/> — the law fixes which
/// fields must appear in §13; a tenant profile may NOT relax this rule.
/// </para>
/// <para>
/// <b>Abstain paths (InsufficientData — NEVER false-Fail):</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="StatementModel.Sections"/> is empty (detection pass did not run).</item>
///   <item>§13 is not applicable.</item>
///   <item>§13 is applicable but not present (absence is handled by <c>MandatorySectionsPresenceRule</c>).</item>
///   <item><see cref="StatementModel.NormalizedFullText"/> is empty — text layer unreadable.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Section13TransferenciaCompletenessRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    private const int Section13Number = 13;

    // -----------------------------------------------------------------------
    // Mandated field label constant.
    // The Acuerdo §13 requires "crédito disponible para transferencia de saldo
    // de otras tarjetas". After normalization (upper-case, accent-strip):
    // -----------------------------------------------------------------------

    /// <summary>
    /// Normalized label for "crédito disponible para transferencia de saldo de otras tarjetas".
    /// </summary>
    internal const string LabelTransferencia = "TRANSFERENCIA DE SALDO";

    /// <inheritdoc />
    public string CheckId => "LAW-§13-TRANSFERENCIA";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §13";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        // Guard: statement model must be present.
        var model = ctx.StatementModel;
        if (model is null)
            return InsufficientData("StatementModel is not populated; section-detection pass did not run.");

        // Guard: Sections must be populated (non-empty = detection pass ran).
        var sections = model.Sections;
        if (sections.Count == 0)
            return InsufficientData(
                "Sections list is empty — either the extraction predates Story 10.1 " +
                "or the PDF has no text layer.");

        // Locate §13 in the sections map.
        var section13 = sections.FirstOrDefault(s => s.SectionNumber == Section13Number);

        // If §13 is not applicable → not-applicable, never Fail.
        if (section13 is null || !section13.IsApplicable)
            return InsufficientData(
                "§13 is not applicable: the section was not expected for this statement.");

        // If §13 is applicable but not present → abstain (MandatorySectionsPresenceRule flags absence).
        if (!section13.IsPresent)
            return InsufficientData(
                "§13 is applicable but not present in the PDF; " +
                "MandatorySectionsPresenceRule covers the absence finding.");

        // Guard: NormalizedFullText must be non-empty.
        var text = model.NormalizedFullText;
        if (string.IsNullOrWhiteSpace(text))
            return InsufficientData(
                "NormalizedFullText is empty — PDF text layer is unreadable; cannot check §13 transferencia field.");

        // Check for the transferencia label.
        // NormalizedFullText is already upper-cased and accent-stripped (VecTextNormalizer contract).
        // LabelTransferencia is stored in normalized form.
        if (text.Contains(LabelTransferencia, StringComparison.Ordinal))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"§13 transferencia de saldo field present ('{LabelTransferencia}' found)"));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: LabelTransferencia,
                observed: $"missing: '{LabelTransferencia}' not found in §13 Nivel de uso section"));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-SEC-PRESENCE: Verifies that every mandatory CONDUSEF <i>Acuerdo</i> section (§1–§28)
/// is present in the statement PDF (Story 10.1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> the CONDUSEF <i>Acuerdo relativo al formato de estado de cuenta
/// estandarizado de tarjeta de crédito para personas físicas</i> (DOF 29-Dec-2022,
/// mandatory since 17-Oct-2024) defines 28 mandatory sections, §1 through §28.  Every
/// section must appear in the statement unless it is conditional (§16, §23, §25) and its
/// trigger condition is not present.
/// </para>
/// <para>
/// <b>Classification:</b> <see cref="RuleClassification.BaselineLocked"/> — the law fixes
/// which sections are mandatory; a tenant profile may NOT relax this rule.
/// </para>
/// <para>
/// <b>Abstain paths (InsufficientData — never false-Fail):</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="Domain.Extraction.StatementModel.Sections"/> is empty — the section-detection
///     pass did not run (e.g. earlier extraction pipeline version) or the text layer is unreadable.</item>
/// </list>
/// A false Fail here would wrongly halt a billing run; when in doubt, the rule abstains.
/// </para>
/// <para>
/// <b>Verdict logic:</b>
/// <list type="bullet">
///   <item>Conditional sections (<see cref="DetectedSection.IsApplicable"/> == false) are
///     NEVER counted missing regardless of <see cref="DetectedSection.IsPresent"/>.</item>
///   <item>All other sections must have <see cref="DetectedSection.IsPresent"/> == true.</item>
///   <item>Fail: enumerates missing §-numerals in <see cref="RuleFinding.Observed"/>
///     (e.g. "missing: §3, §8, §19") and passes the full list as
///     <see cref="RuleFinding.Expected"/>.</item>
///   <item>Pass: all mandatory-applicable sections are present.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MandatorySectionsPresenceRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-SEC-PRESENCE";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §1–§28";

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

        // Collect missing mandatory-applicable sections.
        // Conditional sections (IsApplicable == false) are excluded from the missing list.
        // Indeterminate sections (DetectionStatus == Indeterminate) are also excluded:
        // they represent sections that cannot be confirmed from the text layer (e.g. §1 Logo
        // is a visual image). Counting them as missing would produce false-Fails.
        // Only Absent + applicable sections count as genuinely missing.
        var missing = sections
            .Where(s => s.IsApplicable
                        && !s.IsPresent
                        && s.DetectionStatus != SectionDetectionStatus.Indeterminate)
            .OrderBy(s => s.SectionNumber)
            .ToList();

        if (missing.Count == 0)
        {
            // All mandatory-applicable sections are present.
            var presentNumerals = string.Join(", ",
                sections
                    .Where(s => s.IsApplicable)
                    .OrderBy(s => s.SectionNumber)
                    .Select(s => $"§{s.SectionNumber}"));

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"all mandatory sections present ({presentNumerals})"));
        }

        // Fail — enumerate the missing §-numerals.
        var missingNumerals = string.Join(", ", missing.Select(s => $"§{s.SectionNumber}"));
        var allMandatoryNumerals = string.Join(", ",
            sections
                .Where(s => s.IsApplicable)
                .OrderBy(s => s.SectionNumber)
                .Select(s => $"§{s.SectionNumber}"));

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: allMandatoryNumerals,
                observed: $"missing: {missingNumerals}"));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

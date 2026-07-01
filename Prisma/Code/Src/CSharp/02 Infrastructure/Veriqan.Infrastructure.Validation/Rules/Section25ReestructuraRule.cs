using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§25-REESTRUCTURA: When the statement indicates a restructured debt,
/// §25 <i>Reestructura de tu deuda</i> must be present.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §25 mandates that the
/// restructured-debt disclosure section must appear whenever the cardholder's account has
/// an active debt restructuring arrangement.
/// </para>
/// <para>
/// <b>Trigger detection (R2, heading-based only):</b> applicability is determined exclusively
/// by <see cref="DetectedSection.IsApplicable"/> on the §25 entry — set by the heading-band
/// detection pass (Story 10.1 / R1) when a REESTRUCTURA heading is detected in the document.
/// The keyword-body-text fallback has been retired (R2) because it matched keywords appearing
/// in body sections other than §25 (e.g. a restructure reference in §7), which caused false-Fails.
/// When <see cref="DetectedSection.IsApplicable"/> is <c>false</c> the rule returns a
/// <b>Pass</b> with an "not applicable" observation — it never emits Fail for an untriggered
/// conditional section.
/// </para>
/// <para>
/// <b>Accepted limitation (AC 10.5, owner ruling 2026-06-30 — "trigger = heading"):</b>
/// because the restructure trigger is heading-derived, an account that is genuinely
/// restructured but <em>omits the §25 heading entirely</em> is treated as not-applicable and
/// cannot be flagged. Detecting a restructure independently of the §25 heading (e.g. from
/// evidence elsewhere in the statement) is a separate extraction spike, deliberately deferred;
/// the abstain-safe posture (never false-Fail) is preserved in the meantime.
/// </para>
/// <para>
/// <b>Verdict logic (when triggered):</b>
/// <list type="bullet">
///   <item>§25 present (<see cref="DetectedSection.IsPresent"/> == <c>true</c>) → <b>Pass</b>.</item>
///   <item>§25 absent → <b>Fail</b> Critical.</item>
/// </list>
/// </para>
/// <para>
/// <b>Abstain paths (InsufficientData — never false-Fail):</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="Domain.Extraction.StatementModel.Sections"/> is empty.</item>
///   <item>§25 is not found in the Sections list at all.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Section25ReestructuraRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-§25-REESTRUCTURA";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §25";

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

        // Guard: Sections list must be populated (detection pass ran).
        var sections = model.Sections;
        if (sections.Count == 0)
            return InsufficientData(
                "Sections list is empty — either the extraction predates Story 10.1 " +
                "or the PDF has no text layer.");

        // Locate the §25 DetectedSection entry.
        DetectedSection? section25 = null;
        foreach (var s in sections)
        {
            if (s.SectionNumber == 25)
            {
                section25 = s;
                break;
            }
        }

        // If §25 was not found in the sections list at all, we cannot evaluate.
        if (section25 is null)
            return InsufficientData(
                "§25 was not found in the Sections list — section-detection pass may be incomplete.");

        // Trigger is now determined solely by the heading-based section-detection pass (R1).
        // The keyword-fallback (TextContainsRestructureTrigger) has been retired:
        // R1 sets IsApplicable=true when a REESTRUCTURA heading is detected in the document,
        // which is the reliable source of truth. A keyword in the document body (not a heading)
        // is insufficient evidence and caused false-Fails.
        if (!section25.IsApplicable)
        {
            // No restructure trigger detected by the heading-scan → rule is not applicable.
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "§25 Reestructura de tu deuda is not applicable — restructure heading not detected (IsApplicable=false)."));
        }

        // Trigger is active: §25 must be present.
        if (section25.IsPresent)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "§25 Reestructura de tu deuda is present as required (restructure trigger active).",
                    locator: section25.Locator));
        }

        // §25 missing despite restructure trigger being active.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "§25 Reestructura de tu deuda must be present when a debt restructuring is active.",
                observed: "§25 is absent but the restructure trigger was detected in the statement.",
                toleranceApplied: null,
                locator: section25.Locator));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

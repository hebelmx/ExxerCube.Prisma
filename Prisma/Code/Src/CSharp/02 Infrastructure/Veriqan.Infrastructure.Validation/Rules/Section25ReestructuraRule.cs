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
/// <b>Trigger detection:</b> the restructure trigger is detected in two complementary ways,
/// in order of precedence:
/// <list type="number">
///   <item>
///     <see cref="DetectedSection.IsApplicable"/> on the §25 entry is <c>true</c> — the
///     section-detection pass (Story 10.1) already resolved applicability from layout/text.
///   </item>
///   <item>
///     Fallback: a restructure-related keyword is found in
///     <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> (e.g.
///     <c>REESTRUCTURA</c> or <c>REESTRUCTURACION</c>).
///   </item>
/// </list>
/// When <b>neither</b> trigger is present the rule returns a <b>Pass</b> with an
/// "not applicable" observation — it never emits Fail for an untriggered conditional section.
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
///   <item><see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is empty
///         (cannot evaluate the keyword-based fallback trigger).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class Section25ReestructuraRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Restructure-trigger keywords (normalized: upper-case, accent-stripped).
    // "REESTRUCTURACION" covers "reestructuración" and "reestructuracion".
    // "REESTRUCTURA" covers the shorter form used as a section heading.
    internal const string TriggerReestructura = "REESTRUCTURA";
    internal const string TriggerReestructuracion = "REESTRUCTURACION";

    private static readonly string[] TriggerKeywords =
    [
        TriggerReestructuracion,   // check longer form first to avoid false substring match
        TriggerReestructura,
    ];

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

        // Guard: need text to evaluate the keyword-based trigger fallback.
        var text = model.NormalizedFullText;
        if (string.IsNullOrEmpty(text))
            return InsufficientData(
                "NormalizedFullText is empty — cannot evaluate the §25 restructure trigger.");

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

        // Determine whether the restructure trigger is active.
        // Priority 1: the section-detection pass resolved IsApplicable.
        // Priority 2: keyword-based fallback in the normalized text.
        var triggerActive = section25.IsApplicable || TextContainsRestructureTrigger(text);

        if (!triggerActive)
        {
            // No restructure trigger → rule is not applicable → Pass (N/A).
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "§25 Reestructura de tu deuda is not applicable — no restructure trigger detected."));
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

    private static bool TextContainsRestructureTrigger(string normalizedText)
    {
        foreach (var keyword in TriggerKeywords)
        {
            if (normalizedText.Contains(keyword, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

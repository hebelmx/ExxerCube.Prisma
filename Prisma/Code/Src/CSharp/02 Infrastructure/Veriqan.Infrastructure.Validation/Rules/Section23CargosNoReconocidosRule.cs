using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§23-STATUS: When §23 <i>Cargos no reconocidos</i> is present and applicable,
/// verifies that the section contains at least one of the mandated CONDUSEF status tokens.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §23 mandates that every
/// unrecognized-charge row must carry a status drawn from the following closed enumeration:
/// <list type="bullet">
///   <item><c>pendiente-en-revisión</c> (normalized: <c>PENDIENTE EN REVISION</c>)</item>
///   <item><c>concluida-procedente</c>  (normalized: <c>CONCLUIDA PROCEDENTE</c>)</item>
///   <item><c>concluida-improcedente</c>(normalized: <c>CONCLUIDA IMPROCEDENTE</c>)</item>
/// </list>
/// </para>
/// <para>
/// <b>Trigger and applicability:</b> the rule consults
/// <see cref="DetectedSection.IsApplicable"/> on the §23 entry.  When the section's
/// trigger condition is absent from the statement
/// (<see cref="DetectedSection.IsApplicable"/> == <c>false</c>) the rule returns a
/// <b>Pass</b> with an "not applicable" observation — it never emits Fail for a
/// non-triggered conditional section.
/// </para>
/// <para>
/// <b>Abstain paths (InsufficientData — never false-Fail):</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="Domain.Extraction.StatementModel.Sections"/> is empty.</item>
///   <item><see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is empty
///         when §23 is applicable — we cannot inspect row statuses without text.</item>
///   <item>§23 is applicable and present but individual row statuses cannot be parsed
///         from the text (no status token found anywhere in the section) — in this case
///         the rule emits Fail only if it can reliably determine that charge rows exist
///         without any valid status token; otherwise it abstains.</item>
/// </list>
/// </para>
/// <para>
/// <b>Verdict logic (when §23 is applicable and present):</b>
/// If any of the three valid status tokens is found in
/// <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> → <b>Pass</b>.
/// If none is found (and text is non-empty) → <b>Fail</b> Critical.
/// If the section is present but text is empty → <b>InsufficientData</b>.
/// </para>
/// </remarks>
internal sealed class Section23CargosNoReconocidosRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Mandated CONDUSEF status tokens (normalized: upper-case, accent-stripped).
    // Source: Acuerdo §23 closed-enum specification.
    internal const string StatusPendienteEnRevision = "PENDIENTE EN REVISION";
    internal const string StatusConcluidaProcedente = "CONCLUIDA PROCEDENTE";
    internal const string StatusConcluidaImprocedente = "CONCLUIDA IMPROCEDENTE";

    private static readonly string[] ValidStatusTokens =
    [
        StatusPendienteEnRevision,
        StatusConcluidaProcedente,
        StatusConcluidaImprocedente,
    ];

    /// <inheritdoc />
    public string CheckId => "LAW-§23-STATUS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §23";

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

        // Locate the §23 DetectedSection entry.
        DetectedSection? section23 = null;
        foreach (var s in sections)
        {
            if (s.SectionNumber == 23)
            {
                section23 = s;
                break;
            }
        }

        // If §23 was not found in the sections list at all, we cannot evaluate.
        if (section23 is null)
            return InsufficientData(
                "§23 was not found in the Sections list — section-detection pass may be incomplete.");

        // Conditional section: trigger not present → not applicable → Pass (N/A).
        if (!section23.IsApplicable)
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "§23 Cargos no reconocidos is not applicable — trigger condition absent."));

        // If §23 is Indeterminate (no text anchor), abstain — do NOT Fail.
        if (section23.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§23 detection status is Indeterminate — section has no reliable text anchor; " +
                "cannot scope status-token check.");

        // Match strictly within §23's own section text — never fall back to the whole
        // document. A whole-doc scan would re-introduce the false-Pass the review flagged:
        // a resolved-charge status token (e.g. CONCLUIDA PROCEDENTE) printed in §22 would
        // satisfy this check even when §23's own rows carry no valid status. If §23 is
        // present but its section text could not be sliced, abstain (never false-block).
        var sectionText = section23.SectionText;
        if (string.IsNullOrEmpty(sectionText))
            return InsufficientData(
                "§23 is applicable and present but its section text could not be sliced " +
                "(empty SectionText); abstaining rather than scanning the whole document.");

        // Search for any valid status token within §23's own section text (scoped).
        // Finding at least one valid token is a necessary (not sufficient) condition;
        // absence is a reliable indicator of a non-compliant §23 section.
        foreach (var token in ValidStatusTokens)
        {
            if (sectionText.Contains(token, System.StringComparison.Ordinal))
            {
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Pass(
                        checkId: CheckId,
                        technique: Technique,
                        engineVersion: Version,
                        observed: $"§23 present and at least one valid status token found in §23 section text: \"{token}\"."));
            }
        }

        // No valid status token found in §23's section text.
        var tokenList = string.Join(", ", ValidStatusTokens);
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"At least one of the mandated status tokens: {tokenList}",
                observed: "§23 is present and applicable but none of the mandated charge-row status tokens were found in the statement text.",
                toleranceApplied: null,
                locator: section23.Locator));
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

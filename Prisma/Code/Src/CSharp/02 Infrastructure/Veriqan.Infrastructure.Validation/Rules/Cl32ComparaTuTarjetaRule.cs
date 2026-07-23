using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// CL-32: Mandatory section "COMPARA TU TARJETA" must be present in the statement.
/// </summary>
/// <remarks>
/// <para>
/// This is a pure text-presence check that does not depend on reference-data from the bundle.
/// The section heading "COMPARA TU TARJETA" is a regulatory requirement on all VEC statements
/// and is therefore always checked — the rule degrades to <c>InsufficientData</c> only when
/// the statement's full text has not been extracted, never because the bundle lacks data.
/// </para>
/// <para>
/// <b>Matching (RC1.S6 — reworked to consult §11 detection first):</b> §11 "Compara tu
/// tarjeta" is one of the 28 CONDUSEF <i>Acuerdo</i> sections tracked in
/// <see cref="Domain.Extraction.StatementModel.Sections"/>. On real-corpus statements the
/// heading is raster-image-rendered, so a raw text-layer <c>Contains</c> check never finds it —
/// but the §-anchor OCR escalation ladder (<c>SectionAnchorOcrEscalationStage</c>) can. This rule
/// therefore looks up <see cref="Domain.Extraction.DetectedSection.SectionNumber"/> == 11 first
/// (picking up BOTH a text-layer heading-band match AND an OCR-escalated one — whichever the
/// unified <c>Sections</c> list carries) and uses its
/// <see cref="Domain.Extraction.DetectedSection.IsPresent"/> value directly. Only when
/// <c>Sections</c> is empty (extraction predates Story 10.1, or the pass did not run) does it
/// fall back to the original raw-text path: the search string <c>"COMPARA TU TARJETA"</c>
/// normalized via <see cref="TextNormalizer.Normalize"/> (upper-case, accent-stripped,
/// whitespace-collapsed) and searched via
/// <see cref="string.Contains(string,System.StringComparison)"/> in
/// <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/>.
/// </para>
/// <para>
/// <b>InsufficientData path:</b> only when <see cref="Domain.Extraction.StatementModel"/>
/// is null or <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is empty
/// (the extraction stage has not run or the PDF has no text layer).
/// </para>
/// </remarks>
internal sealed class Cl32ComparaTuTarjetaRule : IVecValidationRule
{
    private const int Section11Number = 11;
    private const string Version = "1.0.0";

    /// <summary>
    /// Normalized form of the mandatory section heading.
    /// Pre-computed so that the normalization path is not repeated per call.
    /// </summary>
    private static readonly string NormalizedSectionHeading =
        TextNormalizer.Normalize("COMPARA TU TARJETA");

    /// <inheritdoc />
    public string CheckId => "CL-32";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §11";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;
        if (model is null || string.IsNullOrEmpty(model.NormalizedFullText))
            return InsufficientData("StatementModel or NormalizedFullText is not populated.");

        var section11 = model.Sections.Count > 0 ? FindSection11(model.Sections) : null;

        if (section11 is not null && section11.DetectionStatus != SectionDetectionStatus.Indeterminate)
        {
            var sectionLocator = section11.IsPresent ? section11.Locator : FieldLocator.PageHint(1);

            if (section11.IsPresent)
            {
                return Result<RuleFinding>.WithSuccess(
                    RuleFinding.Pass(
                        checkId: CheckId,
                        technique: Technique,
                        engineVersion: Version,
                        observed: $"Section §11 ({section11.Name}) detected " +
                                  $"({(section11.Source == SectionDetectionSource.Ocr ? "OCR escalation" : "text layer")}).",
                        toleranceApplied: null,
                        locator: sectionLocator));
            }

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"Section heading '{NormalizedSectionHeading}' present in statement.",
                    observed: "Section §11 (Compara tu tarjeta) was not detected in the text layer or via OCR escalation.",
                    toleranceApplied: null,
                    locator: sectionLocator));
        }

        // Fallback: Sections is empty (extraction predates Story 10.1) or §11's status is
        // Indeterminate — defer to the original raw-text search.
        var locator = FieldLocator.PageHint(1);

        if (model.NormalizedFullText.Contains(NormalizedSectionHeading, System.StringComparison.Ordinal))
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Section heading '{NormalizedSectionHeading}' found in statement text.",
                    toleranceApplied: null,
                    locator: locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"Section heading '{NormalizedSectionHeading}' present in statement.",
                observed: "Section heading not found in normalized statement text.",
                toleranceApplied: null,
                locator: locator));
    }

    private static DetectedSection? FindSection11(System.Collections.Generic.IReadOnlyList<DetectedSection> sections)
    {
        foreach (var s in sections)
            if (s.SectionNumber == Section11Number)
                return s;

        return null;
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

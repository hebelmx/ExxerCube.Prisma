using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§27-GLOSARIO: Verifies that the fifteen mandatory verbatim "Glosario de términos
/// y abreviaturas" definitions are present in section 27 of the statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §27 mandates fifteen
/// glossary definitions (a–o) that must appear verbatim (within the tolerance threshold)
/// in section 27 of every credit-card statement.
/// </para>
/// <para>
/// All fifteen term texts are in <see cref="CondusefVerbatimCatalog.Section27Terms"/>.
/// Matching is tolerant via <see cref="VerbatimBlockMatcher"/> / <see cref="VecTextMatcher"/>.
/// </para>
/// <para>
/// <b>Abstain (InsufficientData) when §27 is absent or detection has not run.</b>
/// Never false-Fail: defer section-presence decisions to <c>MandatorySectionsPresenceRule</c>.
/// Also abstains when §27 was detected via the §-anchor OCR escalation ladder
/// (<see cref="SectionDetectionSource.Ocr"/>) and the terms are not found in the text layer —
/// the section region is raster-rendered, so a text-layer miss does not prove content absence
/// (RC1-residuals adversarial-review fix, 2026-07-23). A positive text-layer match still Passes
/// regardless of presence source.
/// </para>
/// </remarks>
internal sealed class Section27GlosarioRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-§27-GLOSARIO";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §27";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    private static readonly IReadOnlyList<(string Id, string NormalizedText)> NormalizedBlocks =
        BuildNormalized();

    private static IReadOnlyList<(string Id, string NormalizedText)> BuildNormalized()
    {
        var list = new List<(string, string)>(CondusefVerbatimCatalog.Section27Terms.Count);
        foreach (var (id, text) in CondusefVerbatimCatalog.Section27Terms)
            list.Add((id, VecTextMatcher.Normalize(text)));

        return list;
    }

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;
        if (model is null || string.IsNullOrEmpty(model.NormalizedFullText))
            return InsufficientData("StatementModel or NormalizedFullText is not populated.");

        if (model.Sections.Count == 0)
            return InsufficientData(
                "Section-detection pass has not run (Sections list is empty).");

        var section27 = FindSection(model.Sections, 27);
        if (section27 is null || !section27.IsApplicable || !section27.IsPresent)
            return InsufficientData(
                "Section §27 (GLOSARIO DE TERMINOS Y ABREVIATURAS) was not detected in the " +
                "document; deferring to MandatorySectionsPresenceRule.");

        // Abstain for Indeterminate sections.
        if (section27.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§27 detection status is Indeterminate — cannot scope glosario check.");

        // Match against the whole document instead of SectionText to guard against
        // two-column PDF layouts where the reading-order band scan may truncate
        // SectionText (false-Fail on a compliant statement). Each glossary definition
        // is a long, unique, legally-fixed string — the false-Pass risk from a spurious
        // occurrence elsewhere in the document is not realistic.
        var threshold = ResolveThreshold(ctx);

        var failing = VerbatimBlockMatcher.FindFailingBlocks(model.NormalizedFullText, NormalizedBlocks, threshold);

        if (failing.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {NormalizedBlocks.Count} §27 glosario terms (a–o) found in document (threshold={threshold:F3}).",
                    toleranceApplied: (decimal)threshold,
                    locator: section27.Locator));
        }

        var detail = BuildFailDetail(failing);

        // RC1-residuals adversarial-review fix: when §27's presence was established by the OCR
        // escalation ladder (raster-rendered heading, no text-layer heading band), the section
        // region itself is a rendered image — a text-layer scan of NormalizedFullText proves
        // nothing about the region's actual content, only that the glossary terms are not in the
        // selectable text layer. Content-level OCR verification does not exist yet, so a
        // text-layer miss under these conditions cannot be treated as a confident absence.
        // Abstain instead of false-Failing a statement whose terms are legible on the rendered page.
        if (section27.Source == SectionDetectionSource.Ocr)
            return InsufficientData(
                "§27 (GLOSARIO DE TERMINOS Y ABREVIATURAS) was detected via OCR escalation " +
                "(raster-rendered section, no text-layer heading band); a text-layer scan cannot " +
                $"prove the mandated glossary terms are absent from the rendered region. " +
                $"Missing/low-similarity in text layer: {detail}.");

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"All {NormalizedBlocks.Count} §27 verbatim glosario terms present.",
                observed: $"Missing/low-similarity §27 term(s): {detail}",
                toleranceApplied: (decimal)threshold,
                locator: section27.Locator));
    }

    private static double ResolveThreshold(VerificationContext ctx)
        => ctx.TenantProfile?.VerbatimSimilarityThreshold ?? CondusefVerbatimCatalog.DefaultSimilarityThreshold;

    private static DetectedSection? FindSection(
        System.Collections.Generic.IReadOnlyList<DetectedSection> sections,
        int number)
    {
        foreach (var s in sections)
            if (s.SectionNumber == number)
                return s;

        return null;
    }

    private static string BuildFailDetail(List<(string BlockId, double Score)> failing)
    {
        var parts = new System.Text.StringBuilder();
        foreach (var (id, score) in failing)
        {
            if (parts.Length > 0)
                parts.Append("; ");

            parts.Append(id);
            parts.Append($" (similarity={score:F3})");
        }

        return parts.ToString();
    }

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

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
/// LAW-§26-NOTAS: Verifies that the thirteen mandatory verbatim "Notas aclaratorias"
/// are present in section 26 of the statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §26 mandates thirteen
/// numbered notes (a–m) that must appear verbatim (or near-verbatim within the tolerance
/// threshold) in section 26 ("Notas aclaratorias") of every credit-card statement.
/// </para>
/// <para>
/// All thirteen note texts are in <see cref="CondusefVerbatimCatalog.Section26Notes"/>.
/// Matching is tolerant (Jaccard + Levenshtein max via <see cref="VecTextMatcher"/>, plus a
/// token-sequence LCS ratio — <see cref="VerbatimBlockMatcher"/>, RC1.S4.a Fix 2a) to handle
/// PDF extraction artefacts (line-wrapping, soft hyphens, ligatures, multi-column interleaving).
/// A block also passes when it matches any accepted wording variant registered in
/// <see cref="CondusefVerbatimCatalog.AcceptedVariants"/> (RC1.S4.a Fix 2b, owner-ruled) — the
/// DOF text is always the primary candidate; variants only widen acceptance.
/// </para>
/// <para>
/// <b>Abstain (InsufficientData) paths:</b>
/// <list type="bullet">
///   <item>No StatementModel / empty NormalizedFullText.</item>
///   <item>Section-detection pass has not run (Sections is empty).</item>
///   <item>Section §26 was not detected → defer to <c>MandatorySectionsPresenceRule</c>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Classification:</b> <see cref="RuleClassification.BaselineLocked"/> — tenant cannot relax.
/// </para>
/// </remarks>
internal sealed class Section26NotasAclaratoriasRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-§26-NOTAS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §26";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    private static readonly IReadOnlyList<(string Id, IReadOnlyList<string> NormalizedCandidates)> NormalizedBlocks =
        BuildNormalized();

    private static IReadOnlyList<(string Id, IReadOnlyList<string> NormalizedCandidates)> BuildNormalized()
    {
        var list = new List<(string, IReadOnlyList<string>)>(CondusefVerbatimCatalog.Section26Notes.Count);
        foreach (var (id, text) in CondusefVerbatimCatalog.Section26Notes)
        {
            var candidates = new List<string> { VecTextMatcher.Normalize(text) };
            if (CondusefVerbatimCatalog.AcceptedVariants.TryGetValue(id, out var variants))
            {
                foreach (var variant in variants)
                    candidates.Add(VecTextMatcher.Normalize(variant));
            }

            list.Add((id, candidates));
        }

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

        var section26 = FindSection(model.Sections, 26);
        if (section26 is null || !section26.IsApplicable || !section26.IsPresent)
            return InsufficientData(
                "Section §26 (NOTAS ACLARATORIAS) was not detected in the document; " +
                "deferring to MandatorySectionsPresenceRule.");

        // Abstain for Indeterminate sections.
        if (section26.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§26 detection status is Indeterminate — cannot scope notas check.");

        // Match against the whole document instead of SectionText to guard against
        // two-column PDF layouts where the reading-order band scan may truncate
        // SectionText (false-Fail on a compliant statement). Each nota is a long,
        // unique, legally-fixed string (~200–400 chars) — the false-Pass risk from a
        // spurious occurrence elsewhere in the document is not realistic.
        var threshold = ResolveThreshold(ctx);

        var failing = VerbatimBlockMatcher.FindFailingBlocksMultiCandidate(model.NormalizedFullText, NormalizedBlocks, threshold);

        if (failing.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {NormalizedBlocks.Count} §26 notas aclaratorias (a–m) found in document (threshold={threshold:F3}).",
                    toleranceApplied: (decimal)threshold,
                    locator: section26.Locator));
        }

        var detail = BuildFailDetail(failing);

        // RC1-residuals adversarial-review fix: when §26's presence was established by the OCR
        // escalation ladder (raster-rendered heading, no text-layer heading band), the section
        // region itself is a rendered image — a text-layer scan of NormalizedFullText proves
        // nothing about the region's actual content. Content-level OCR verification does not
        // exist yet, so a text-layer miss under these conditions cannot be treated as a
        // confident absence. Abstain instead of false-Failing a statement whose notas are
        // legible on the rendered page. (Currently latent on the measured corpus — the notas
        // genuinely are in the text layer and this rule Passes above — but the same guard the
        // sibling §11/§17/§24/§27 rules carry keeps the Fail branch honest for future layouts.)
        if (section26.Source == SectionDetectionSource.Ocr)
            return InsufficientData(
                "§26 (NOTAS ACLARATORIAS) was detected via OCR escalation (raster-rendered " +
                "section, no text-layer heading band); a text-layer scan cannot prove the " +
                $"mandated notas are absent from the rendered region. Missing/low-similarity in " +
                $"text layer: {detail}.");

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"All {NormalizedBlocks.Count} §26 verbatim notas aclaratorias present.",
                observed: $"Missing/low-similarity §26 nota(s): {detail}",
                toleranceApplied: (decimal)threshold,
                locator: section26.Locator));
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

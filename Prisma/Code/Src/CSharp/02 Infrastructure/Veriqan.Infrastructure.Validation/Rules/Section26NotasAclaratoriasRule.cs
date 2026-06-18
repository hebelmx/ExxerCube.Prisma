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
/// Matching is tolerant (Jaccard + Levenshtein max via <see cref="VecTextMatcher"/>) to
/// handle PDF extraction artefacts (line-wrapping, soft hyphens, ligatures).
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

    private static readonly IReadOnlyList<(string Id, string NormalizedText)> NormalizedBlocks =
        BuildNormalized();

    private static IReadOnlyList<(string Id, string NormalizedText)> BuildNormalized()
    {
        var list = new List<(string, string)>(CondusefVerbatimCatalog.Section26Notes.Count);
        foreach (var (id, text) in CondusefVerbatimCatalog.Section26Notes)
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

        var failing = VerbatimBlockMatcher.FindFailingBlocks(model.NormalizedFullText, NormalizedBlocks, threshold);

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
    {
        _ = ctx;
        return CondusefVerbatimCatalog.DefaultSimilarityThreshold;
    }

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

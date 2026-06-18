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

        var threshold = ResolveThreshold(ctx);
        var docText = model.NormalizedFullText;

        var failing = VerbatimBlockMatcher.FindFailingBlocks(docText, NormalizedBlocks, threshold);

        if (failing.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {NormalizedBlocks.Count} §27 glosario terms (a–o) found.",
                    toleranceApplied: (decimal)threshold,
                    locator: section27.Locator));
        }

        var detail = BuildFailDetail(failing);
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

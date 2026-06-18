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
/// LAW-§17-LEGENDS: Verifies that the four art-6-IV mandatory legends appear in
/// section 17 ("Mensajes adicionales") of the statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §17 and art-6-IV of the
/// same Disposiciones require the following four legends in the "Mensajes adicionales" section:
/// <list type="bullet">
///   <item>"Al ser tu crédito de tasa variable, los intereses pueden aumentar."</item>
///   <item>"Incumplir tus obligaciones te puede generar comisiones e intereses moratorios."</item>
///   <item>"Contratar créditos que excedan tu capacidad de pago afecta tu historial crediticio."</item>
///   <item>"Realizar sólo el pago mínimo aumenta el tiempo de pago y el costo de la deuda"</item>
/// </list>
/// </para>
/// <para>
/// <b>Abstain (InsufficientData) when section §17 is absent or detection has not run.</b>
/// Never false-Fail: the rule defers to <c>MandatorySectionsPresenceRule</c> for section-presence.
/// </para>
/// </remarks>
internal sealed class Section17LegendsRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-§17-LEGENDS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §17";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    private static readonly IReadOnlyList<(string Id, string NormalizedText)> NormalizedBlocks =
        BuildNormalized();

    private static IReadOnlyList<(string Id, string NormalizedText)> BuildNormalized()
    {
        var list = new List<(string, string)>(CondusefVerbatimCatalog.Section17Legends.Count);
        foreach (var (id, text) in CondusefVerbatimCatalog.Section17Legends)
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

        var section17 = FindSection(model.Sections, 17);
        if (section17 is null || !section17.IsApplicable || !section17.IsPresent)
            return InsufficientData(
                "Section §17 (MENSAJES ADICIONALES) was not detected in the document; " +
                "deferring to MandatorySectionsPresenceRule.");

        // Abstain for Indeterminate sections.
        if (section17.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§17 detection status is Indeterminate — cannot scope legend check.");

        // Scope match to §17's own section text to prevent false-Pass from legends
        // appearing in other sections (e.g. in §26 notes or §27 glosario).
        var sectionText = section17.SectionText;
        if (string.IsNullOrWhiteSpace(sectionText))
            return InsufficientData(
                "§17 SectionText is empty — section-scoped text not available; " +
                "cannot reliably check §17 legends.");

        var threshold = ResolveThreshold(ctx);

        var failing = VerbatimBlockMatcher.FindFailingBlocks(sectionText, NormalizedBlocks, threshold);

        if (failing.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"All {NormalizedBlocks.Count} §17 art-6-IV mandatory legends found in §17 section text (threshold={threshold:F3}).",
                    toleranceApplied: (decimal)threshold,
                    locator: section17.Locator));
        }

        var detail = BuildFailDetail(failing);
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"All {NormalizedBlocks.Count} §17 art-6-IV mandatory legends present.",
                observed: $"Missing/low-similarity §17 legend(s): {detail}",
                toleranceApplied: (decimal)threshold,
                locator: section17.Locator));
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

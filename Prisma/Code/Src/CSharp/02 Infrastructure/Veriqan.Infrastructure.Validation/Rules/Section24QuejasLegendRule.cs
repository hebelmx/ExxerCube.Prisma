using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-§24-QUEJAS: Verifies that the invariant CONDUSEF "Atención de quejas" legend
/// (including the UNE contact block with phones 800-999-8080 / 55-53-40-09-99)
/// is present in section 24 of the statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §24. The full template
/// text is (issuer fills in its own name/address/phone):
/// "[Denominación] recibe las consultas, reclamaciones o aclaraciones en su Unidad Especializada
/// de Atención a Usuarios, … podrá acudir a la Comisión Nacional para la Protección y Defensa
/// de los Usuarios de Servicios Financieros. Correo electrónico: asesoria@condusef.gob.mx,
/// chat en línea www.condusef.gob.mx o Tel: 800 999 8080 y 55 53 40 09 99."
/// </para>
/// <para>
/// Only the invariant CONDUSEF portion is verified here. The issuer-specific slots are
/// [placeholder] text that varies per institution.
/// </para>
/// <para>
/// <b>Abstain (InsufficientData) when section §24 is absent or detection has not run.</b>
/// </para>
/// </remarks>
internal sealed class Section24QuejasLegendRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-§24-QUEJAS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §24";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    private static readonly string NormalizedExpected =
        VecTextMatcher.Normalize(CondusefVerbatimCatalog.Section24QuejasLegendFragment);

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

        var section24 = FindSection(model.Sections, 24);
        if (section24 is null || !section24.IsApplicable || !section24.IsPresent)
            return InsufficientData(
                "Section §24 (ATENCION DE QUEJAS) was not detected in the document; " +
                "deferring to MandatorySectionsPresenceRule.");

        var threshold = ResolveThreshold(ctx);
        var score = VerbatimBlockMatcher.BestWindowSimilarity(
            model.NormalizedFullText,
            NormalizedExpected);

        if (score >= threshold)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"§24 CONDUSEF contact legend found (similarity={score:F3}).",
                    toleranceApplied: (decimal)threshold,
                    locator: section24.Locator));
        }

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Invariant §24 CONDUSEF UNE/quejas legend present " +
                          "(including 800 999 8080 / 55 53 40 09 99).",
                observed: $"§24 CONDUSEF contact legend missing or low similarity " +
                          $"(similarity={score:F3}, threshold={threshold:F3}).",
                toleranceApplied: (decimal)threshold,
                locator: section24.Locator));
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

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

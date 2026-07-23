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
/// Also abstains when §24 was detected via the §-anchor OCR escalation ladder
/// (<see cref="SectionDetectionSource.Ocr"/>) and the legend is not found in the text layer —
/// the section region is raster-rendered, so a text-layer miss does not prove content absence
/// (RC1-residuals adversarial-review fix, 2026-07-23). A positive text-layer match still Passes
/// regardless of presence source.
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

        // Abstain for Indeterminate sections.
        if (section24.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§24 detection status is Indeterminate — cannot scope quejas legend check.");

        // Match against the whole document instead of SectionText to guard against
        // two-column PDF layouts where the reading-order band scan may truncate
        // SectionText (false-Fail on a compliant statement). The §24 quejas block is
        // a long, legally-fixed CONDUSEF contact string — an accidental false-Pass
        // from a spurious occurrence elsewhere is not a realistic risk.
        var threshold = ResolveThreshold(ctx);
        var score = VerbatimBlockMatcher.BestWindowSimilarity(model.NormalizedFullText, NormalizedExpected);

        if (score >= threshold)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"§24 CONDUSEF contact legend found in document (similarity={score:F3}).",
                    toleranceApplied: (decimal)threshold,
                    locator: section24.Locator));
        }

        // RC1-residuals adversarial-review fix: when §24's presence was established by the OCR
        // escalation ladder (raster-rendered heading, no text-layer heading band), the section
        // region itself is a rendered image — a text-layer scan of NormalizedFullText proves
        // nothing about the region's actual content, only that the legend is not in the
        // selectable text layer. Content-level OCR verification does not exist yet, so a
        // text-layer miss under these conditions cannot be treated as a confident absence.
        // Abstain instead of false-Failing a statement whose legend is legible on the rendered page.
        if (section24.Source == SectionDetectionSource.Ocr)
            return InsufficientData(
                "§24 (ATENCION DE QUEJAS) was detected via OCR escalation (raster-rendered " +
                "section, no text-layer heading band); a text-layer scan cannot prove the " +
                $"mandated quejas legend is absent from the rendered region (similarity=" +
                $"{score:F3}, threshold={threshold:F3}).");

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

    private Result<RuleFinding> InsufficientData(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

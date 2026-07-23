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
/// LAW-§11-URLS: Verifies that the two CONDUSEF-mandated URLs appear in section 11
/// ("Compara tu tarjeta") of the credit-card statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> CONDUSEF <i>Acuerdo</i> (DOF 29-Dec-2022) §11 requires the statement
/// to display the following two portal URLs so cardholders can compare products:
/// <list type="bullet">
///   <item><c>https://tarjetas.condusef.gob.mx/index.php</c></item>
///   <item><c>https://comparador.banxico.org.mx/</c></item>
/// </list>
/// </para>
/// <para>
/// <b>Matching:</b> tolerant similarity via <see cref="VerbatimBlockMatcher"/> /
/// <see cref="VecTextMatcher"/>. For short URL strings the normalized-contains fast-path
/// fires almost immediately.
/// </para>
/// <para>
/// <b>Abstain (InsufficientData) paths — never false-Fail:</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null or
///     <see cref="Domain.Extraction.StatementModel.NormalizedFullText"/> is empty.</item>
///   <item>Section 11 was not detected (host section absent or not applicable) — the rule
///     defers to Story 10.1 which owns section-detection; it never fires on a blank/missing
///     section.</item>
///   <item>Section 11 was detected via the §-anchor OCR escalation ladder
///     (<see cref="SectionDetectionSource.Ocr"/>) and the mandated URLs are not found in the
///     text layer — the section region is raster-rendered, so a text-layer miss does not prove
///     content absence (RC1-residuals adversarial-review fix, 2026-07-23). A positive text-layer
///     match still Passes regardless of presence source.</item>
/// </list>
/// </para>
/// <para>
/// <b>Classification:</b> <see cref="RuleClassification.BaselineLocked"/> — verbatim legal
/// text; tenant profiles may NOT relax this rule.
/// </para>
/// </remarks>
internal sealed class Section11ComparaUrlsRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <inheritdoc />
    public string CheckId => "LAW-§11-URLS";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §11";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <summary>
    /// Pre-normalized expected URL blocks for fast-path matching.
    /// </summary>
    private static readonly IReadOnlyList<(string Id, string NormalizedText)> NormalizedBlocks =
    [
        ("§11-url1", VecTextMatcher.Normalize(CondusefVerbatimCatalog.Section11Url1)),
        ("§11-url2", VecTextMatcher.Normalize(CondusefVerbatimCatalog.Section11Url2)),
    ];

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;
        if (model is null || string.IsNullOrEmpty(model.NormalizedFullText))
            return InsufficientData("StatementModel or NormalizedFullText is not populated.");

        // Abstain when section 11 detection has not run (empty Sections list).
        if (model.Sections.Count == 0)
            return InsufficientData(
                "Section-detection pass has not run (Sections list is empty); " +
                "cannot verify §11 URL blocks.");

        // Abstain when §11 was not detected in the document.
        var section11 = FindSection(model.Sections, 11);
        if (section11 is null || !section11.IsApplicable || !section11.IsPresent)
            return InsufficientData(
                "Section §11 (COMPARA TU TARJETA) was not detected in the document; " +
                "deferring to MandatorySectionsPresenceRule.");

        // Abstain for Indeterminate sections (no reliable text anchor).
        if (section11.DetectionStatus == SectionDetectionStatus.Indeterminate)
            return InsufficientData(
                "§11 detection status is Indeterminate — cannot scope URL check.");

        // Match against the whole document instead of SectionText to guard against
        // two-column PDF layouts where the reading-order band scan may truncate
        // SectionText (false-Fail on a compliant statement). These URL strings are
        // long, unique, and legally fixed — an accidental false-Pass from a spurious
        // occurrence elsewhere in the document is not a realistic risk.
        var threshold = ResolveThreshold(ctx);

        var failing = VerbatimBlockMatcher.FindFailingBlocks(model.NormalizedFullText, NormalizedBlocks, threshold);

        if (failing.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"Both §11 CONDUSEF URLs found in document (threshold={threshold:F3}).",
                    toleranceApplied: (decimal)threshold,
                    locator: section11.Locator));
        }

        var detail = BuildFailDetail(failing);

        // RC1-residuals adversarial-review fix: when §11's presence was established by the OCR
        // escalation ladder (raster-rendered heading, no text-layer heading band), the section
        // region itself is a rendered image — a text-layer scan of NormalizedFullText proves
        // nothing about the region's actual content, only that the CONDUSEF URL strings are not
        // in the selectable text layer. Content-level OCR verification does not exist yet, so a
        // text-layer miss under these conditions cannot be treated as a confident absence.
        // Abstain instead of false-Failing a statement whose URLs are legible on the rendered page.
        if (section11.Source == SectionDetectionSource.Ocr)
            return InsufficientData(
                "§11 (COMPARA TU TARJETA) was detected via OCR escalation (raster-rendered " +
                "section, no text-layer heading band); a text-layer scan cannot prove the " +
                $"mandated URLs are absent from the rendered region. Missing/low-similarity in " +
                $"text layer: {detail}.");

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: "Both CONDUSEF URLs present (§11): " +
                          CondusefVerbatimCatalog.Section11Url1 + " and " +
                          CondusefVerbatimCatalog.Section11Url2,
                observed: $"Missing/low-similarity §11 URL block(s): {detail}",
                toleranceApplied: (decimal)threshold,
                locator: section11.Locator));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the similarity threshold for this rule.
    /// Returns the tenant-configured value when available (Story 10.3 AC);
    /// falls back to <see cref="CondusefVerbatimCatalog.DefaultSimilarityThreshold"/> (0.82) otherwise.
    /// </summary>
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

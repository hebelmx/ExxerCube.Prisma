using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// LAW-SEC-ORDER-GAP: Verifies that all present mandatory sections appear in the legally-mandated
/// §1 → §28 reading order, and that no inter-section blank gap on the same page exceeds 2 cm
/// (Story 10.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Legal basis:</b> the CONDUSEF <i>Acuerdo relativo al formato de estado de cuenta
/// estandarizado de tarjeta de crédito</i> (DOF 29-Dec-2022, mandatory since 17-Oct-2024)
/// specifies the <em>guía de llenado</em> form rules: sections must appear in the fixed §1–§28
/// sequence and blank vertical gaps between them must not exceed 2 cm.
/// </para>
/// <para>
/// <b>Order check:</b> among all <em>present</em> sections, derive their reading order by
/// sorting ascending by (PageNumber, descending Bottom-Y) — i.e. page-first then top-to-bottom
/// within a page (origin bottom-left: higher Bottom = higher on page).  The reading-order
/// sequence of <see cref="DetectedSection.SectionNumber"/> values must be strictly ascending
/// (§1 &lt; §2 &lt; … &lt; §28).  Any deviation is reported.
/// </para>
/// <para>
/// <b>Blank-gap check:</b> any entry in <see cref="StatementModel.SectionGaps"/> whose
/// <see cref="SectionGap.GapPoints"/> exceeds <see cref="TwoCmInPoints"/> is a violation.
/// Only same-page gaps are considered (page-break whitespace is excluded by the extractor).
/// </para>
/// <para>
/// <b>Classification:</b> <see cref="RuleClassification.BaselineLocked"/> — the guía de llenado
/// fixes both the section sequence and the blank-gap limit; tenants may not relax either.
/// </para>
/// <para>
/// <b>Abstain paths (InsufficientData — never false-Fail):</b>
/// <list type="bullet">
///   <item><see cref="VerificationContext.StatementModel"/> is null.</item>
///   <item><see cref="StatementModel.Sections"/> is empty (section-detection pass did not run
///     or text layer is unreadable).</item>
///   <item>Fewer than two present sections are locatable (not enough geometry to order them).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SectionOrderAndGapRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// 2 cm expressed in PDF points (1 cm = 1/2.54 in = 72/2.54 pt ≈ 28.3464567 pt).
    /// Any inter-section same-page blank gap exceeding this value is a violation.
    /// </summary>
    public const double TwoCmInPoints = SectionGap.TwoCmInPoints;

    /// <inheritdoc />
    public string CheckId => "LAW-SEC-ORDER-GAP";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo Anexo — guía de llenado";

    /// <inheritdoc />
    public RuleClassification Classification => RuleClassification.BaselineLocked;

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;
        if (model is null)
            return Abstain("StatementModel is not populated; section-detection pass did not run.");

        var sections = model.Sections;
        if (sections.Count == 0)
            return Abstain(
                "Sections list is empty — extraction predates Story 10.1 or the PDF has no text layer.");

        // Collect present sections that have a real page locator (PageNumber > 0).
        var locatable = sections
            .Where(s => s.IsPresent && s.Locator.PageNumber > 0)
            .ToList();

        if (locatable.Count < 2)
            return Abstain(
                "Fewer than two present sections have page geometry; cannot determine reading order.");

        // ---- Order check --------------------------------------------------
        // Sort present sections by reading order: (PageNumber ASC, Bottom DESC).
        // Higher Bottom value = closer to top of page in PDF origin-bottom-left coords.
        var orderViolations = ComputeOrderViolations(locatable);

        // ---- Gap check ----------------------------------------------------
        var gapViolations = model.SectionGaps
            .Where(g => g.GapPoints > TwoCmInPoints)
            .OrderByDescending(g => g.GapPoints)
            .ToList();

        if (orderViolations.Count == 0 && gapViolations.Count == 0)
        {
            var summary = BuildPassSummary(locatable, model.SectionGaps);
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: summary));
        }

        // ---- Fail — describe all violations -------------------------------
        var observed = BuildFailObserved(orderViolations, gapViolations);
        var expected = BuildExpected(locatable);

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: expected,
                observed: observed));
    }

    // -----------------------------------------------------------------------
    // Order violation detection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the list of (readingPosition, section) pairs where the section number is
    /// lower than the previous section in reading order — i.e. it appears "before" a
    /// lower-numbered section but has a higher section number (wrong order).
    /// </summary>
    private static List<(int ReadingPosition, DetectedSection Section, DetectedSection PrecedingSection)>
        ComputeOrderViolations(List<DetectedSection> locatable)
    {
        // Sort by reading order: page ascending, then within-page top-to-bottom
        // (descending Bottom because origin is bottom-left).
        var readingOrder = locatable
            .OrderBy(s => s.Locator.PageNumber)
            .ThenByDescending(s => s.Locator.Bottom ?? 0.0)
            .ToList();

        var violations = new List<(int, DetectedSection, DetectedSection)>();
        int maxSectionNumberSeen = 0;
        DetectedSection? lastSection = null;

        for (var i = 0; i < readingOrder.Count; i++)
        {
            var current = readingOrder[i];
            if (current.SectionNumber < maxSectionNumberSeen && lastSection is not null)
            {
                violations.Add((i + 1, current, lastSection));
            }
            else
            {
                maxSectionNumberSeen = current.SectionNumber;
            }

            lastSection = current;
        }

        return violations;
    }

    // -----------------------------------------------------------------------
    // Message builders
    // -----------------------------------------------------------------------

    private static string BuildPassSummary(
        List<DetectedSection> locatable,
        IReadOnlyList<SectionGap> gaps)
    {
        var orderSeq = string.Join(" → ",
            locatable
                .OrderBy(s => s.Locator.PageNumber)
                .ThenByDescending(s => s.Locator.Bottom ?? 0.0)
                .Select(s => $"§{s.SectionNumber}"));

        var maxGap = gaps.Count > 0 ? gaps.Max(g => g.GapPoints) : 0.0;
        var maxGapCm = maxGap / SectionGap.CmToPoints;

        return $"Section order correct ({orderSeq}); " +
               $"max gap {maxGapCm:0.##} cm ({maxGap:0.##} pt) ≤ 2 cm threshold.";
    }

    private static string BuildFailObserved(
        List<(int ReadingPosition, DetectedSection Section, DetectedSection PrecedingSection)> orderViolations,
        List<SectionGap> gapViolations)
    {
        var sb = new StringBuilder();

        if (orderViolations.Count > 0)
        {
            sb.Append("Out-of-order sections: ");
            sb.Append(string.Join("; ", orderViolations.Select(v =>
                $"§{v.Section.SectionNumber} ({v.Section.Name}) appears after §{v.PrecedingSection.SectionNumber} " +
                $"at reading position {v.ReadingPosition} (page {v.Section.Locator.PageNumber})")));
        }

        if (gapViolations.Count > 0)
        {
            if (sb.Length > 0)
                sb.Append(". ");

            sb.Append("Blank gap(s) > 2 cm: ");
            sb.Append(string.Join("; ", gapViolations.Select(g =>
            {
                var cm = g.GapPoints / SectionGap.CmToPoints;
                return $"{cm:0.##} cm ({g.GapPoints:0.##} pt) between §{g.AfterSectionNumber} and §{g.BeforeSectionNumber} (page {g.PageNumber})";
            })));
        }

        return sb.ToString();
    }

    private static string BuildExpected(List<DetectedSection> locatable)
    {
        var orderedNumerals = string.Join(" → ",
            locatable
                .OrderBy(s => s.SectionNumber)
                .Select(s => $"§{s.SectionNumber}"));
        return $"Present sections in ascending §-order with no inter-section same-page gap > 2 cm ({TwoCmInPoints:0.##} pt). Expected order: {orderedNumerals}";
    }

    // -----------------------------------------------------------------------
    // InsufficientData factory
    // -----------------------------------------------------------------------

    private Result<RuleFinding> Abstain(string reason) =>
        Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: reason));
}

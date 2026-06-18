using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// LAW-SEC-SIZECAP: Verifies that legally-bounded sections are not oversized.
/// §17 must not exceed ¼ of a page; §21 and §28 must not exceed ⅓ of a page.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — geometric
/// measurement of the vertical extent each section occupies on its page, derived from
/// the heading-band <see cref="FieldLocator"/> coordinates produced by Epic 10.
/// </para>
/// <para>
/// <b>Cardinal rule — never false-Fail:</b>
/// A section's extent is measured as the gap from its heading down to the heading of the
/// next present section on the <i>same page</i>.  When no same-page successor exists
/// (section may span a page break), the rule abstains for that section — it does not Fail.
/// If all three target sections abstain, the rule returns <c>InsufficientData</c>.
/// </para>
/// <para>
/// <b>Epsilon bias toward Pass:</b> a section only Fails when its measured fraction
/// strictly exceeds the threshold plus the epsilon guard of 0.02.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction stage did not run).</item>
///   <item><see cref="StatementModel.Sections"/> is null or empty (Epic 10 did not run).</item>
///   <item><see cref="StatementModel.Pages"/> is null or empty (no page geometry available).</item>
///   <item>All three target sections (§17, §21, §28) are absent/abstained (nothing measurable).</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SectionSizeCapRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    // Regulatory thresholds
    private const double QuarterPage = 0.25;
    private const double ThirdPage = 1.0 / 3.0;

    // Epsilon: only flag when fraction > threshold + Epsilon (bias toward Pass)
    private const double Epsilon = 0.02;

    // Target sections and their caps: (sectionNumber, threshold)
    private static readonly (int SectionNumber, double Threshold)[] s_targets =
    [
        (17, QuarterPage),
        (21, ThirdPage),
        (28, ThirdPage),
    ];

    /// <inheritdoc />
    public string CheckId => "LAW-SEC-SIZECAP";

    /// <inheritdoc />
    public string DofNumeral => "Acuerdo §17 (¼ página) / §21·§28 (⅓ página)";

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

        // InsufficientData: model or geometry not available.
        if (model is null
            || model.Sections is null
            || model.Sections.Count == 0
            || model.Pages is null
            || model.Pages.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No section or page geometry data available (Epic 10 extraction did not run)."));
        }

        // Build a page-height lookup for quick access.
        var pageHeights = BuildPageHeightLookup(model.Pages);

        // Build a sorted list of all present sections with valid bottom coordinates.
        var presentWithGeometry = BuildPresentGeometryList(model.Sections);

        // Evaluate each target section.
        var exceededResult = (SectionNumber: 0, Fraction: 0.0, Threshold: 0.0, Locator: (FieldLocator?)null);
        var passCount = 0;
        var abstainCount = 0;
        var passDetail = new StringBuilder();
        var abstainDetail = new StringBuilder();

        foreach (var (targetNum, threshold) in s_targets)
        {
            var measureResult = MeasureSection(targetNum, threshold, model.Sections, presentWithGeometry, pageHeights);

            switch (measureResult.Kind)
            {
                case MeasureKind.Exceeded:
                    // First exceeded section wins the Fail verdict.
                    if (exceededResult.SectionNumber == 0)
                    {
                        exceededResult = (targetNum, measureResult.Fraction, threshold, measureResult.Locator);
                    }
                    break;

                case MeasureKind.WithinCap:
                    passCount++;
                    if (passDetail.Length > 0)
                        passDetail.Append("; ");
                    passDetail.Append(CultureInfo.InvariantCulture,
                        $"§{targetNum} {measureResult.Fraction:P1} ≤ {threshold:P0}");
                    break;

                case MeasureKind.Abstained:
                    abstainCount++;
                    if (abstainDetail.Length > 0)
                        abstainDetail.Append("; ");
                    abstainDetail.Append(CultureInfo.InvariantCulture,
                        $"§{targetNum} abstained ({measureResult.AbstainReason})");
                    break;
            }
        }

        // Verdict: exceeded > pass > all-abstained.
        if (exceededResult.SectionNumber != 0)
        {
            var capLabel = exceededResult.Threshold == QuarterPage ? "¼" : "⅓";
            var observed = $"§{exceededResult.SectionNumber} occupies {exceededResult.Fraction:P1} of the page" +
                           $" (cap: {capLabel} = {exceededResult.Threshold:P0}).";
            if (abstainDetail.Length > 0)
                observed += $" Abstained: {abstainDetail}.";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Fail(
                    checkId: CheckId,
                    technique: Technique,
                    severity: FindingSeverity.Critical,
                    engineVersion: Version,
                    expected: $"Section height ≤ {exceededResult.Threshold:P0} of page",
                    observed: observed,
                    locator: exceededResult.Locator!));
        }

        if (passCount > 0)
        {
            var observed = $"All measured sections within cap. Measured: {passDetail}.";
            if (abstainDetail.Length > 0)
                observed += $" {abstainDetail}.";

            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: observed));
        }

        // All three abstained — nothing measurable.
        return Result<RuleFinding>.WithSuccess(
            RuleFinding.InsufficientData(
                checkId: CheckId,
                technique: Technique,
                engineVersion: Version,
                reason: $"None of §17/§21/§28 could be measured (no same-page successor found for any). {abstainDetail}"));
    }

    // -----------------------------------------------------------------------
    // Measurement helpers
    // -----------------------------------------------------------------------

    private enum MeasureKind { Exceeded, WithinCap, Abstained }

    private readonly record struct MeasureResult(
        MeasureKind Kind,
        double Fraction,
        FieldLocator? Locator,
        string AbstainReason);

    private static MeasureResult Abstain(string reason) =>
        new(MeasureKind.Abstained, 0.0, null, reason);

    private static MeasureResult Within(double fraction) =>
        new(MeasureKind.WithinCap, fraction, null, string.Empty);

    private static MeasureResult Exceeded(double fraction, FieldLocator locator) =>
        new(MeasureKind.Exceeded, fraction, locator, string.Empty);

    /// <summary>
    /// Attempts to measure section <paramref name="sectionNumber"/>'s vertical extent
    /// as the gap from its heading down to the next present section's heading on the same page.
    /// </summary>
    private static MeasureResult MeasureSection(
        int sectionNumber,
        double threshold,
        IReadOnlyList<DetectedSection> allSections,
        List<(int SectionNumber, int PageNumber, double Bottom, FieldLocator Locator)> presentWithGeometry,
        Dictionary<int, double> pageHeights)
    {
        // Find the target section — must be Present.
        DetectedSection? target = null;
        foreach (var s in allSections)
        {
            if (s.SectionNumber == sectionNumber)
            {
                target = s;
                break;
            }
        }

        if (target is null || !target.IsPresent || target.DetectionStatus != SectionDetectionStatus.Present)
            return Abstain($"§{sectionNumber} is not present");

        if (target.Locator.Bottom is null)
            return Abstain($"§{sectionNumber} locator has no Bottom coordinate");

        var targetPage = target.Locator.PageNumber;
        var targetBottom = target.Locator.Bottom.Value;

        // Require a known positive page height.
        if (!pageHeights.TryGetValue(targetPage, out var pageHeight) || pageHeight <= 0.0)
            return Abstain($"§{sectionNumber} page {targetPage} has no valid height");

        // Find the next present section on the same page that is physically below the target
        // (i.e. has a smaller Bottom value in PDF coordinates, where higher Y = higher on page).
        (int SectionNumber, int PageNumber, double Bottom, FieldLocator Locator) nextEntry = default;
        var foundNext = false;

        foreach (var entry in presentWithGeometry)
        {
            // Must be a different section with a higher section number (logically "next").
            if (entry.SectionNumber <= sectionNumber)
                continue;

            // Must be on the same page.
            if (entry.PageNumber != targetPage)
                continue;

            // Must be physically below the target (smaller Bottom = lower on page).
            if (entry.Bottom >= targetBottom)
                continue;

            // Take the candidate with the largest SectionNumber that is still below (closest next heading).
            // Because presentWithGeometry is sorted by SectionNumber ascending, the first match
            // (smallest SectionNumber greater than target) is what we want for the "next section" gap.
            if (!foundNext)
            {
                nextEntry = entry;
                foundNext = true;
                break; // List is sorted ascending by SectionNumber; first match is the immediate next.
            }
        }

        if (!foundNext)
            return Abstain($"§{sectionNumber} has no same-page successor (may cross a page break)");

        // Extent = vertical distance from target heading down to next heading.
        // PDF bottom-left origin: target.Bottom > next.Bottom means target is above next.
        var extent = targetBottom - nextEntry.Bottom;
        if (extent <= 0.0)
            return Abstain($"§{sectionNumber} extent is non-positive (geometry inconsistency)");

        var fraction = extent / pageHeight;

        // Only flag when fraction strictly exceeds threshold + Epsilon.
        if (fraction > threshold + Epsilon)
            return Exceeded(fraction, target.Locator);

        return Within(fraction);
    }

    /// <summary>
    /// Builds a page-number to page-height dictionary from the page list.
    /// </summary>
    private static Dictionary<int, double> BuildPageHeightLookup(IReadOnlyList<PageInspectionFacts> pages)
    {
        var dict = new Dictionary<int, double>(pages.Count);
        foreach (var page in pages)
        {
            dict[page.PageNumber] = page.Height;
        }

        return dict;
    }

    /// <summary>
    /// Returns all Present sections that have a valid Bottom coordinate,
    /// sorted by SectionNumber ascending.
    /// </summary>
    private static List<(int SectionNumber, int PageNumber, double Bottom, FieldLocator Locator)>
        BuildPresentGeometryList(IReadOnlyList<DetectedSection> sections)
    {
        var list = new List<(int SectionNumber, int PageNumber, double Bottom, FieldLocator Locator)>(sections.Count);
        foreach (var s in sections)
        {
            if (!s.IsPresent || s.DetectionStatus != SectionDetectionStatus.Present)
                continue;
            if (s.Locator.Bottom is null)
                continue;

            list.Add((s.SectionNumber, s.Locator.PageNumber, s.Locator.Bottom.Value, s.Locator));
        }

        list.Sort(static (a, b) => a.SectionNumber.CompareTo(b.SectionNumber));
        return list;
    }
}

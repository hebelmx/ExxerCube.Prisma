using System;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Rules;

/// <summary>
/// CL-28: Verifies that no text in the statement PDF contains layout-level word overlap —
/// i.e. no two words on the same horizontal band have X-extents that intersect beyond the
/// rule threshold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — pure geometry
/// check on word bounding boxes extracted by PdfPig; no ML inference required.
/// </para>
/// <para>
/// <b>Overlap threshold (ADR-V3):</b> the threshold applied at rule time (in PDF points)
/// is recorded on every <see cref="RuleFinding"/> in <c>ToleranceApplied</c>.
/// The threshold is a <b>fixed compile-time constant</b>:
/// <see cref="DefaultOverlapThresholdPoints"/> = 1.0 pt (~0.35 mm), chosen to be
/// deliberately below the extraction epsilon of 2.0 pt so that any incident that made it
/// through the extractor is also flagged here.
/// <c>ctx.ToleranceConfig</c> is consulted but does not currently expose a
/// <c>VisualOverlapThresholdPoints</c> field (that would require a bundle schema change).
/// Bundle-configurable override is a <b>future enhancement</b>; until then the fixed
/// default is always applied.
/// </para>
/// <para>
/// <b>InsufficientData paths:</b>
/// <list type="bullet">
///   <item><c>StatementModel</c> is <see langword="null"/> (extraction did not run).</item>
/// </list>
/// </para>
/// <para>
/// <b>Pass:</b> <c>TextOverlapIncidents</c> is empty, or all incidents are at or below
/// the threshold (the extractor epsilon of 2.0 pt already filters trivial touching, so
/// all incidents recorded by the extractor are ≥ 2.0 pt).
/// </para>
/// </remarks>
internal sealed class Cl28TextOverlapRule : IVecValidationRule
{
    private const string Version = "1.0.0";

    /// <summary>
    /// Default overlap threshold (PDF points) applied when the bundle does not
    /// carry a <c>VisualOverlapThresholdPoints</c> value.
    /// </summary>
    /// <remarks>
    /// Set to <b>1.0 pt</b> so that any incident surviving the extractor's 2.0 pt
    /// epsilon (i.e. all recorded incidents) is treated as a violation.
    /// The value is documented and recorded on each finding (ADR-V3).
    /// </remarks>
    public const double DefaultOverlapThresholdPoints = 1.0;

    /// <inheritdoc />
    public string CheckId => "CL-28";

    /// <inheritdoc />
    public TechniqueClass Technique => TechniqueClass.Deterministic;

    /// <inheritdoc />
    public Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<RuleFinding>();

        var model = ctx.StatementModel;

        if (model is null)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.InsufficientData(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    reason: "No statement model available — extraction stage did not run."));
        }

        // Resolve the threshold to apply (ADR-V3: record the value used).
        var threshold = ResolveThreshold(ctx);

        var incidents = model.TextOverlapIncidents;

        if (incidents.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: "No text-overlap incidents detected.",
                    toleranceApplied: (decimal)threshold));
        }

        // Find incidents that exceed the rule threshold.
        var violations = incidents
            .Where(i => i.OverlapPoints > threshold)
            .OrderByDescending(i => i.OverlapPoints)
            .ToList();

        if (violations.Count == 0)
        {
            return Result<RuleFinding>.WithSuccess(
                RuleFinding.Pass(
                    checkId: CheckId,
                    technique: Technique,
                    engineVersion: Version,
                    observed: $"{incidents.Count} overlap incident(s) detected but all at or below threshold ({threshold:0.##} pt).",
                    toleranceApplied: (decimal)threshold));
        }

        // Report the worst offender + total count.
        var worst = violations[0];
        var detail = violations.Count == 1
            ? $"Text overlap of {worst.OverlapPoints:0.##} pt found: {worst.SampleText} (page {worst.PageNumber}). Threshold: {threshold:0.##} pt."
            : $"{violations.Count} text-overlap violation(s). Worst: {worst.OverlapPoints:0.##} pt — {worst.SampleText} (page {worst.PageNumber}). Threshold: {threshold:0.##} pt.";

        return Result<RuleFinding>.WithSuccess(
            RuleFinding.Fail(
                checkId: CheckId,
                technique: Technique,
                severity: FindingSeverity.Critical,
                engineVersion: Version,
                expected: $"No overlap exceeding {threshold:0.##} pt",
                observed: detail,
                toleranceApplied: (decimal)threshold,
                locator: worst.Locator));
    }

    // -----------------------------------------------------------------------
    // Threshold resolution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the overlap threshold to apply.
    /// Always returns <see cref="DefaultOverlapThresholdPoints"/>.
    /// </summary>
    /// <remarks>
    /// <c>ToleranceConfig</c> does not currently expose a
    /// <c>VisualOverlapThresholdPoints</c> field — bundle-configurable override is a
    /// future enhancement (requires a bundle schema change).  This method is a dedicated
    /// extension point so that adding the lookup later is a localised, one-line change.
    /// </remarks>
    private static double ResolveThreshold(VerificationContext ctx)
    {
        // ToleranceConfig consulted for forward-compatibility; no field exists yet.
        // Future enhancement: read ctx.ToleranceConfig.VisualOverlapThresholdPoints
        // once the bundle schema carries it.
        _ = ctx.ToleranceConfig;
        return DefaultOverlapThresholdPoints;
    }
}

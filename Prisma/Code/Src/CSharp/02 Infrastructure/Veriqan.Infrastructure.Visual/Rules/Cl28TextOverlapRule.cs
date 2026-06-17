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
/// i.e. no two words on the same horizontal band have X-extents that intersect beyond a
/// configurable threshold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Technique (ADR-V2):</b> <see cref="TechniqueClass.Deterministic"/> — pure geometry
/// check on word bounding boxes extracted by PdfPig; no ML inference required.
/// </para>
/// <para>
/// <b>Overlap threshold (ADR-V3):</b> the threshold applied at rule time (in PDF points)
/// is recorded on every <see cref="RuleFinding"/> in <c>ToleranceApplied</c>.
/// The default constant is <b><see cref="DefaultOverlapThresholdPoints"/> = 1.0 pt</b>
/// (~0.35 mm), which is deliberately below the extraction epsilon of 2.0 pt so that any
/// incident that made it through the extractor is also flagged here.
/// An operator may override the threshold via
/// <c>ctx.ToleranceConfig.VisualOverlapThresholdPoints</c> if the bundle carries one.
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
    /// Uses <see cref="DefaultOverlapThresholdPoints"/> unless the bundle supplies a
    /// numeric visual-overlap threshold via <c>ToleranceConfig</c>.
    /// </summary>
    private static double ResolveThreshold(VerificationContext ctx)
    {
        // ToleranceConfig does not currently carry a VisualOverlapThresholdPoints field
        // (that would require a bundle schema change).  Fall back to the constant.
        // If a future story adds it, implement the lookup here.
        _ = ctx.ToleranceConfig; // consumed — suppress unused-variable warning
        return DefaultOverlapThresholdPoints;
    }
}

// <copyright file="ExportGatePolicy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Application;

/// <summary>
/// Policy options that control when Stage-5 export is allowed to run.
/// A case is held for human review — and Stage 5 is blocked — when ANY of the
/// configured conditions is true.
/// </summary>
/// <remarks>
/// The default values implement the owner-ruling (binding, 2026-06-20):
/// export MUST block on low-confidence OR fusion manual-review-required OR unresolved conflicts.
///
/// Threshold scale: all thresholds use the 0.0–1.0 scale.
/// The classification pipeline currently expresses confidence as int 0–100; callers that
/// compare against <see cref="ClassificationConfidenceThreshold"/> must multiply by 100
/// (e.g. <c>confidence &lt; ClassificationConfidenceThreshold * 100</c>).
/// Story 2.6 (confidence-value-object) will remove the per-call conversion once a shared
/// <c>Confidence</c> VO normalises every stage to the 0–1 scale.
/// </remarks>
public sealed class ExportGatePolicy
{
    /// <summary>
    /// Configuration section name used in appsettings.json.
    /// <code>
    /// "ExportGatePolicy": {
    ///   "BlockOnLowConfidence": true,
    ///   "BlockOnFusionManualReviewRequired": true,
    ///   "BlockOnUnresolvedConflicts": true,
    ///   "ClassificationConfidenceThreshold": 0.70,
    ///   "ManualReviewThreshold": 0.80
    /// }
    /// </code>
    /// </summary>
    public const string SectionName = "ExportGatePolicy";

    /// <summary>
    /// When <see langword="true"/> (default), a classification confidence score below
    /// <c>ClassificationConfidenceThreshold</c> blocks Stage-5 export and routes the
    /// case to the manual-review queue.
    /// </summary>
    public bool BlockOnLowConfidence { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> (default), a fusion result whose
    /// <c>NextAction == ManualReviewRequired</c> blocks Stage-5 export and routes the
    /// case to the manual-review queue.
    /// </summary>
    public bool BlockOnFusionManualReviewRequired { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> (default), any unresolved field conflict in the fusion
    /// result (non-empty <c>ConflictingFields</c>) blocks Stage-5 export and routes the
    /// case to the manual-review queue.
    /// </summary>
    public bool BlockOnUnresolvedConflicts { get; set; } = true;

    /// <summary>
    /// Minimum classification confidence required to allow Stage-5 export (0.0–1.0 scale).
    /// Default: <c>0.70</c> (70 %).
    /// </summary>
    /// <remarks>
    /// Compared against <see cref="ExxerCube.Prisma.Domain.ValueObjects.ClassificationResult.Confidence"/>
    /// (int 0–100) as <c>confidence &lt; ClassificationConfidenceThreshold * 100</c>.
    /// Story 2.6 will remove this conversion once all stages use the shared Confidence VO.
    /// </remarks>
    public double ClassificationConfidenceThreshold { get; set; } = 0.70;

    /// <summary>
    /// Minimum classification confidence below which a manual-review case is created
    /// (0.0–1.0 scale). Default: <c>0.80</c> (80 %).
    /// </summary>
    /// <remarks>
    /// Used by <c>ManualReviewerService</c> to decide whether a LowConfidence review
    /// case should be opened. A document whose classification confidence is below this
    /// value is routed to the review queue even if the export gate allows it through
    /// (because the gate threshold is lower, 0.70 &lt; 0.80).
    /// Compared against <see cref="ExxerCube.Prisma.Domain.ValueObjects.ClassificationResult.Confidence"/>
    /// (int 0–100) as <c>confidence &lt; ManualReviewThreshold * 100</c>.
    /// Story 2.6 will remove this conversion once all stages use the shared Confidence VO.
    /// </remarks>
    public double ManualReviewThreshold { get; set; } = 0.80;
}

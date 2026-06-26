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
/// Threshold scale: all thresholds use the 0.0–1.0 scale. Since Story 2.6
/// (confidence-value-object), every pipeline stage normalises confidence to the shared
/// <c>Confidence</c> VO on 0–1, so callers compare directly on the same scale
/// (e.g. <c>classification.Confidence.Value &lt; ClassificationConfidenceThreshold</c>) —
/// no <c>* 100</c> conversion.
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
    /// Compared directly against <see cref="ExxerCube.Prisma.Domain.ValueObjects.ClassificationResult.Confidence"/>'s
    /// <c>Confidence.Value</c> (0–1) as <c>classification.Confidence.Value &lt; ClassificationConfidenceThreshold</c>
    /// — both on the 0–1 scale since Story 2.6, no <c>* 100</c>.
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
    /// Compared directly against <see cref="ExxerCube.Prisma.Domain.ValueObjects.ClassificationResult.Confidence"/>'s
    /// <c>Confidence.Value</c> (0–1) — both on the 0–1 scale since Story 2.6, no <c>* 100</c>.
    /// </remarks>
    public double ManualReviewThreshold { get; set; } = 0.80;
}

// <copyright file="ExportGatePolicy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace Prisma.Athena.Processing;

/// <summary>
/// Policy options that control when Stage-5 export is allowed to run.
/// A case is held for human review — and Stage 5 is blocked — when ANY of the
/// configured conditions is true.
/// </summary>
/// <remarks>
/// The default values implement the owner-ruling (binding, 2026-06-20):
/// export MUST block on low-confidence OR fusion manual-review-required OR unresolved conflicts.
/// </remarks>
public sealed class ExportGatePolicy
{
    /// <summary>
    /// Configuration section name used in appsettings.json.
    /// <code>
    /// "ExportGatePolicy": {
    ///   "BlockOnLowConfidence": true,
    ///   "BlockOnFusionManualReviewRequired": true,
    ///   "BlockOnUnresolvedConflicts": true
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
}

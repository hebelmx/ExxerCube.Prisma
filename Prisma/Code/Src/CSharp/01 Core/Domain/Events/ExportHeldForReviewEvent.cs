// <copyright file="ExportHeldForReviewEvent.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Published when Stage-5 export is blocked because the case requires human review
/// (low classification confidence, fusion ManualReviewRequired, or unresolved conflicts).
/// The case is held in the manual-review queue; export proceeds only after a reviewer
/// approves it via <see cref="ExxerCube.Prisma.Domain.Interfaces.IManualReviewerPanel.SubmitReviewDecisionAsync"/>.
/// </summary>
/// <remarks>
/// This event is emitted instead of <see cref="ExportCompletedEvent"/> when the
/// <c>ExportGatePolicy</c> (in <c>Prisma.Athena.Processing</c>) gates export.
/// Owner ruling (binding, 2026-06-20 / G-C2): unreviewed/low-confidence/conflicted
/// cases MUST NOT produce regulatory output.
/// </remarks>
public record ExportHeldForReviewEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier of the file whose export was blocked.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the human-readable reasons why export was blocked (one entry per gate condition triggered).
    /// </summary>
    public List<string> BlockReasons { get; init; } = new();

    /// <summary>
    /// Gets the classification confidence that triggered the gate (0-100).
    /// Null when the gate was triggered by fusion state rather than confidence alone.
    /// </summary>
    public int? ClassificationConfidence { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportHeldForReviewEvent"/> class.
    /// </summary>
    public ExportHeldForReviewEvent()
    {
        EventType = nameof(ExportHeldForReviewEvent);
    }
}

// <copyright file="ReviewDecisionApprovedEvent.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Published when a reviewer submits an <c>Approve</c> decision for a case that was held by the
/// export gate (<see cref="ExportHeldForReviewEvent"/>). Subscribing handlers (e.g.
/// <c>ReviewApprovalExportHandler</c>) reload the fused expediente via
/// <c>IExpedienteHandoffStore</c> and re-run Stage-5 export, this time bypassing the gate
/// because a human has explicitly approved the case.
/// </summary>
/// <remarks>
/// Owner ruling (binding, 2026-06-20 / G-C2b): export is blocked until human review; this event
/// is the signal that human approval has been granted and the release path may proceed.
/// A <c>Reject</c> decision does NOT publish this event — the case stays held / closed with no
/// export produced.
/// </remarks>
public record ReviewDecisionApprovedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier of the file whose export was held and has now been approved.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the review case identifier that was approved.
    /// </summary>
    public string CaseId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the decision identifier (populated by the review service when persisting).
    /// </summary>
    public string DecisionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reviewer's user identifier.
    /// </summary>
    public string ReviewerId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the storage-relative path of the fused expediente handoff artifact (e.g.
    /// <c>2026/06/12/{fileId}.fusion.json</c>).  Used by the re-export handler to reload the
    /// expediente via <c>IExpedienteHandoffStore</c>.
    /// <para>
    /// May be <see langword="null"/> or empty when the original processing ran in-process (no
    /// shared-storage handoff).  In that case the re-export handler gracefully skips the handoff
    /// load and uses a minimal <see cref="ExxerCube.Prisma.Domain.ValueObjects.FusionResult"/>
    /// with <c>NextAction = AutoProcess</c>.
    /// </para>
    /// </summary>
    public string? HandoffPath { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewDecisionApprovedEvent"/> class.
    /// </summary>
    public ReviewDecisionApprovedEvent()
    {
        EventType = nameof(ReviewDecisionApprovedEvent);
    }
}

using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing;

/// <summary>
/// Handles the release path for export-gated cases (G-C2b): when a reviewer submits an
/// <c>Approve</c> decision for a held case (signalled via <see cref="ReviewDecisionApprovedEvent"/>),
/// this handler reloads the fused expediente from shared storage and re-runs Stage-5 export,
/// bypassing the gate via the explicit reviewer-override flag.
/// </summary>
/// <remarks>
/// <para>
/// This is the only legitimate path through which a held case may export. A
/// <c>Reject</c> decision is a no-op here — the case remains held / closed with no export produced.
/// </para>
/// <para>
/// <strong>Gate bypass:</strong> the handler calls <see cref="ReconciliationOrchestrator.ReconcileAsync"/>
/// with <c>approvedByReviewer: true</c>, which causes the orchestrator to log-but-not-apply the
/// gate conditions.  The gate is NOT globally disabled — it continues to block all other un-approved
/// cases normally.
/// </para>
/// <para>
/// <strong>Handoff loading:</strong> when <see cref="ReviewDecisionApprovedEvent.HandoffPath"/> is
/// set, the handler loads the persisted <see cref="Expediente"/> via <see cref="IExpedienteHandoffStore"/>
/// and wraps it in a <see cref="FusionResult"/> with <c>NextAction = AutoProcess</c> and no
/// conflicts (the reviewer approved the case so any former fusion flags are superseded).
/// When the path is absent (in-process / unit-test path), a minimal
/// <see cref="FusionResult"/> is used — Stage 5 will produce output based on whatever
/// the underlying exporter can work with (or gracefully skip if the fused expediente is null).
/// </para>
/// <para>
/// Registration: call <see cref="StartAsync"/> once at host start (after all services are wired).
/// The handler is designed as a singleton (stateless beyond the subscription handle).
/// </para>
/// </remarks>
public sealed class ReviewApprovalExportHandler : IDisposable
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ReconciliationOrchestrator _orchestrator;
    private readonly IExpedienteHandoffStore? _handoffStore;
    private readonly ILogger<ReviewApprovalExportHandler> _logger;
    private IDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewApprovalExportHandler"/> class.
    /// </summary>
    /// <param name="eventPublisher">Event bus — used to subscribe to <see cref="ReviewDecisionApprovedEvent"/>.</param>
    /// <param name="orchestrator">The Reconciliator orchestrator that runs Stage-5 export.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="handoffStore">
    /// Optional store used to reload the fused expediente from shared storage when
    /// <see cref="ReviewDecisionApprovedEvent.HandoffPath"/> is set. When <see langword="null"/>
    /// handoff-path loading is skipped and a minimal <see cref="FusionResult"/> is used (graceful
    /// degradation; Stage 5 may skip if <c>FusedExpediente</c> is null).
    /// </param>
    public ReviewApprovalExportHandler(
        IEventPublisher eventPublisher,
        ReconciliationOrchestrator orchestrator,
        ILogger<ReviewApprovalExportHandler> logger,
        IExpedienteHandoffStore? handoffStore = null)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handoffStore = handoffStore;
    }

    /// <summary>
    /// Subscribes to the <see cref="ReviewDecisionApprovedEvent"/> stream and begins
    /// processing approval events.  Safe to call multiple times (subsequent calls are no-ops once
    /// subscribed).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the subscription lifetime.</param>
    public void StartAsync(CancellationToken cancellationToken = default)
    {
        if (_subscription is not null)
        {
            return; // Already started.
        }

        _logger.LogInformation(
            "ReviewApprovalExportHandler starting: subscribing to ReviewDecisionApprovedEvent (G-C2b)");

        _subscription = _eventPublisher
            .GetEventStream<ReviewDecisionApprovedEvent>()
            .Subscribe(
                onNext: async approvalEvent =>
                {
                    try
                    {
                        await HandleAsync(approvalEvent, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Unhandled error processing ReviewDecisionApprovedEvent for FileId {FileId} / CaseId {CaseId}",
                            approvalEvent.FileId, approvalEvent.CaseId);
                    }
                },
                onError: ex => _logger.LogError(ex,
                    "Error in ReviewDecisionApprovedEvent stream (ReviewApprovalExportHandler)"));

        _logger.LogInformation(
            "ReviewApprovalExportHandler started: subscribed to ReviewDecisionApprovedEvent stream");
    }

    /// <summary>
    /// Handles a single <see cref="ReviewDecisionApprovedEvent"/>: reloads the fused expediente
    /// and re-runs Stage-5 export with the reviewer-override flag set, bypassing the export gate.
    /// </summary>
    /// <param name="approvalEvent">The approval event emitted after a reviewer submits Approve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when Stage-5 export (or the graceful-skip path) finishes.</returns>
    public async Task HandleAsync(
        ReviewDecisionApprovedEvent approvalEvent,
        CancellationToken cancellationToken = default)
    {
        if (approvalEvent is null)
        {
            _logger.LogWarning(
                "ReviewApprovalExportHandler.HandleAsync received null event — skipping");
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "ReviewApprovalExportHandler.HandleAsync cancelled for FileId {FileId}", approvalEvent.FileId);
            return;
        }

        _logger.LogInformation(
            "ReviewApprovalExportHandler: processing approval for FileId {FileId}, CaseId {CaseId}, ReviewerId {ReviewerId}",
            approvalEvent.FileId, approvalEvent.CaseId, approvalEvent.ReviewerId);

        // Build the FusionResult to pass to Stage 4–5.
        // If a handoff path is provided, reload the persisted expediente from shared storage.
        // The gate-relevant flags (NextAction / ConflictingFields) are reset to AutoProcess / empty
        // because the reviewer has explicitly approved the case — former fusion flags are superseded.
        var fusionResult = await BuildApprovedFusionResultAsync(
            approvalEvent.FileId,
            approvalEvent.HandoffPath,
            cancellationToken).ConfigureAwait(false);

        // Re-run Stages 4–5 with the reviewer-override flag.
        // approvedByReviewer: true → gate diagnostics run but the block is not applied.
        var stages = await _orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: approvalEvent.FileId,
            correlationId: approvalEvent.CorrelationId,
            isComplete: true,
            handoffPath: approvalEvent.HandoffPath,
            approvedByReviewer: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "ReviewApprovalExportHandler: re-export complete for FileId {FileId}, CaseId {CaseId}. Stages completed: {Stages}",
            approvalEvent.FileId, approvalEvent.CaseId, stages);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="FusionResult"/> suitable for the approved-re-export path.
    /// When a <paramref name="handoffPath"/> is provided and a <see cref="IExpedienteHandoffStore"/>
    /// is wired, loads the persisted fused <see cref="Expediente"/>; otherwise uses an empty
    /// placeholder (Stage 5 will gracefully skip if <c>FusedExpediente</c> is null).
    /// In both cases <c>NextAction</c> is forced to <see cref="NextAction.AutoProcess"/> and
    /// <c>ConflictingFields</c> is empty — the reviewer approval supersedes prior fusion flags.
    /// </summary>
    private async Task<FusionResult> BuildApprovedFusionResultAsync(
        Guid fileId,
        string? handoffPath,
        CancellationToken cancellationToken)
    {
        Expediente? expediente = null;

        if (!string.IsNullOrWhiteSpace(handoffPath) && _handoffStore is not null)
        {
            var loadResult = await _handoffStore
                .LoadAsync(handoffPath, cancellationToken)
                .ConfigureAwait(false);

            if (loadResult.IsSuccess)
            {
                expediente = loadResult.Value;
                _logger.LogDebug(
                    "ReviewApprovalExportHandler: loaded expediente from handoff '{Path}' for FileId {FileId}",
                    handoffPath, fileId);
            }
            else
            {
                _logger.LogWarning(
                    "ReviewApprovalExportHandler: failed to load expediente from handoff '{Path}' for FileId {FileId}: {Errors}. " +
                    "Stage 5 will proceed with null expediente (may be skipped by exporter).",
                    handoffPath, fileId, string.Join(", ", loadResult.Errors));
            }
        }
        else if (!string.IsNullOrWhiteSpace(handoffPath) && _handoffStore is null)
        {
            _logger.LogWarning(
                "ReviewApprovalExportHandler: HandoffPath '{Path}' provided for FileId {FileId} " +
                "but IExpedienteHandoffStore is not wired — Stage 5 will proceed with null expediente.",
                handoffPath, fileId);
        }

        // Reset fusion flags to AutoProcess / no conflicts:
        // The reviewer has approved the case, so former gate-trigger conditions are superseded.
        return new FusionResult
        {
            FusedExpediente = expediente,
            NextAction = NextAction.AutoProcess,
            ConflictingFields = new System.Collections.Generic.List<string>(),
            OverallConfidence = 1.0,
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}

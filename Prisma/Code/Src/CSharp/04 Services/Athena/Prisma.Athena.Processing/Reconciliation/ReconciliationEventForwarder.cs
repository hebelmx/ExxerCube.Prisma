using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing.Reconciliation;

/// <summary>
/// Bridges the cross-process reconciliation transport (MVP-PATH 1.4 Reconciliator edge) into the
/// Reconciliator's in-process event stream: it republishes an <see cref="ExtractionCompletedEvent"/> received
/// from the Athena Extractor onto the local <see cref="IEventPublisher"/>, so the
/// <see cref="ReconciliationPipelineService"/>'s subscription drives Classification → Export. Mirrors the 1.3
/// <c>IngestionEventForwarder</c> on the Downloader → Extractor edge.
/// </summary>
/// <remarks>
/// <para>
/// Before republishing it validates the clearance token on the event (MVP-PATH 1.5, A5): only events whose
/// token carries <see cref="ProcessClearance.Extract"/> and whose <c>file_id</c> claim matches
/// <see cref="ExtractionCompletedEvent.FileId"/> are forwarded. All others are logged and silently dropped
/// (fail-closed; never throw). This enforces the Athena Extractor → Reconciliator trust boundary.
/// </para>
/// <para>
/// Unlike the ingestion forwarder, no path-stamping is needed here: the handoff is a shared-storage reference
/// and the pipeline loads the fused expediente via <see cref="IExpedienteHandoffStore"/>, which resolves the
/// path itself. A null event is ignored (a malformed transport frame must never crash the consumer). Kept as a
/// thin, host-agnostic seam so it is unit-testable without a live SignalR connection.
/// </para>
/// </remarks>
public sealed class ReconciliationEventForwarder
{
    private readonly IEventPublisher _eventPublisher;
    private readonly IProcessClearanceTokenService _clearanceTokenService;
    private readonly ILogger<ReconciliationEventForwarder> _logger;
    private readonly IClearanceReplayGuard _replayGuard;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationEventForwarder"/> class.</summary>
    /// <param name="eventPublisher">The local event publisher the pipeline subscribes to.</param>
    /// <param name="clearanceTokenService">Validates the per-document process clearance token carried on each cross-process event (MVP-PATH 1.5, A5).</param>
    /// <param name="logger">The logger.</param>
    /// <param name="replayGuard">Per-process anti-replay guard; rejects any clearance token whose jti has already been seen (issue #2.1).</param>
    public ReconciliationEventForwarder(
        IEventPublisher eventPublisher,
        IProcessClearanceTokenService clearanceTokenService,
        ILogger<ReconciliationEventForwarder> logger,
        IClearanceReplayGuard replayGuard)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _clearanceTokenService = clearanceTokenService ?? throw new ArgumentNullException(nameof(clearanceTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _replayGuard = replayGuard ?? throw new ArgumentNullException(nameof(replayGuard));
    }

    /// <summary>
    /// Validates the clearance token on a cross-process <see cref="ExtractionCompletedEvent"/> and, when
    /// authorised, republishes it onto the local event stream.
    /// A null event or one that fails clearance validation is silently dropped (never throws; fail-closed).
    /// </summary>
    /// <param name="completedEvent">The event received from the reconciliation hub.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ForwardAsync(ExtractionCompletedEvent? completedEvent,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (completedEvent is null)
        {
            _logger.LogWarning("Received a null ExtractionCompletedEvent from the reconciliation hub; ignoring");
            return;
        }

        // MVP-PATH 1.5 A5: validate the process clearance token before forwarding.
        if (string.IsNullOrWhiteSpace(completedEvent.ClearanceToken))
        {
            LogAndReject(completedEvent.FileId, actorId: null, "missing clearance token");
            return;
        }

        var validation = await _clearanceTokenService
            .ValidateAsync(completedEvent.ClearanceToken, cancellationToken)
            .ConfigureAwait(false);

        if (validation.IsFailure)
        {
            LogAndReject(completedEvent.FileId, actorId: null, validation.Error ?? "token validation failed");
            return;
        }

        var claims = validation.Value!;

        if (claims.Clearance != ProcessClearance.Extract)
        {
            LogAndReject(completedEvent.FileId, claims.ActorId,
                $"clearance {claims.Clearance} not permitted on reconciliation edge");
            return;
        }

        // Issue #2.2: connection-scope tokens (Guid.Empty file_id) are not permitted on the
        // per-message edge — each forwarded event must be bound to a specific document.
        if (claims.FileId == Guid.Empty)
        {
            LogAndReject(completedEvent.FileId, claims.ActorId,
                "clearance token file_id is empty (connection-scope token not permitted on the per-message edge)");
            return;
        }

        if (claims.FileId != completedEvent.FileId)
        {
            LogAndReject(completedEvent.FileId, claims.ActorId,
                "clearance token file_id mismatch (replay/tamper)");
            return;
        }

        // Issue #2.1: anti-replay gate — each jti must be seen exactly once within the retention window.
        if (!_replayGuard.TryRegister(claims.Jti))
        {
            LogAndReject(completedEvent.FileId, claims.ActorId,
                "clearance token replay detected (jti already seen or missing)");
            return;
        }

        _logger.LogInformation(
            "Forwarding ExtractionCompletedEvent {FileId} (corr {CorrelationId}) from the reconciliation hub to the local pipeline",
            completedEvent.FileId,
            completedEvent.CorrelationId);

        _eventPublisher.Publish(completedEvent);
    }

    /// <summary>
    /// Logs a rejection at Warning level and returns without publishing. Never throws.
    /// </summary>
    private void LogAndReject(Guid fileId, string? actorId, string reason)
    {
        _logger.LogWarning(
            "ReconciliationEventForwarder: rejected ExtractionCompletedEvent {FileId} (actor {ActorId}): {Reason}",
            fileId,
            actorId ?? "(unknown)",
            reason);
    }
}

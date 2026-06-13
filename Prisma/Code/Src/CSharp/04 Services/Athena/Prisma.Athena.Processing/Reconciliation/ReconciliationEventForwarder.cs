using System;
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
/// Unlike the ingestion forwarder, no path-stamping is needed here: the handoff is a shared-storage reference
/// and the pipeline loads the fused expediente via <see cref="IExpedienteHandoffStore"/>, which resolves the
/// path itself. A null event is ignored (a malformed transport frame must never crash the consumer). Kept as a
/// thin, host-agnostic seam so it is unit-testable without a live SignalR connection.
/// </remarks>
public sealed class ReconciliationEventForwarder
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ReconciliationEventForwarder> _logger;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationEventForwarder"/> class.</summary>
    /// <param name="eventPublisher">The local event publisher the pipeline subscribes to.</param>
    /// <param name="logger">The logger.</param>
    public ReconciliationEventForwarder(IEventPublisher eventPublisher, ILogger<ReconciliationEventForwarder> logger)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Republishes a cross-process <see cref="ExtractionCompletedEvent"/> onto the local event stream. A null
    /// event is ignored (defensive: a malformed transport frame must never crash the consumer).
    /// </summary>
    /// <param name="completedEvent">The event received from the reconciliation hub.</param>
    public void Forward(ExtractionCompletedEvent? completedEvent)
    {
        if (completedEvent is null)
        {
            _logger.LogWarning("Received a null ExtractionCompletedEvent from the reconciliation hub; ignoring");
            return;
        }

        _logger.LogInformation(
            "Forwarding ExtractionCompletedEvent {FileId} (corr {CorrelationId}) from the reconciliation hub to the local pipeline",
            completedEvent.FileId,
            completedEvent.CorrelationId);

        _eventPublisher.Publish(completedEvent);
    }
}

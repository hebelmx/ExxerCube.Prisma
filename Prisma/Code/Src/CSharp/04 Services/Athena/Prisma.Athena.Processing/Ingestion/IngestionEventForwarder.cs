using System;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing.Ingestion;

/// <summary>
/// Bridges the cross-process ingestion transport (MVP-PATH 1.3) into Athena's in-process event stream:
/// it republishes a <see cref="DocumentDownloadedEvent"/> received from the Orion <em>Downloader</em> actor
/// onto the local <see cref="IEventPublisher"/>, so the <see cref="ProcessingOrchestrator"/>'s existing
/// <c>GetEventStream&lt;DocumentDownloadedEvent&gt;()</c> subscription drives the pipeline.
/// </summary>
/// <remarks>
/// Kept as a thin, host-agnostic seam so the republish behavior is unit-testable without a live SignalR
/// connection — the SignalR client (<c>SiaraIngestionHubClient</c>) simply forwards each received event here.
/// </remarks>
public sealed class IngestionEventForwarder
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<IngestionEventForwarder> _logger;

    /// <summary>Initializes a new instance of the <see cref="IngestionEventForwarder"/> class.</summary>
    /// <param name="eventPublisher">The local event publisher the pipeline subscribes to.</param>
    /// <param name="logger">The logger.</param>
    public IngestionEventForwarder(IEventPublisher eventPublisher, ILogger<IngestionEventForwarder> logger)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Republishes a cross-process <see cref="DocumentDownloadedEvent"/> onto the local event stream.
    /// A null event is ignored (defensive: a malformed transport frame must never crash the consumer).
    /// </summary>
    /// <param name="downloadEvent">The event received from the Orion ingestion hub.</param>
    public void Forward(DocumentDownloadedEvent? downloadEvent)
    {
        if (downloadEvent is null)
        {
            _logger.LogWarning("Received a null DocumentDownloadedEvent from the ingestion hub; ignoring");
            return;
        }

        _logger.LogInformation(
            "Forwarding DocumentDownloadedEvent {FileId} (corr {CorrelationId}) from the ingestion hub to the local pipeline",
            downloadEvent.FileId,
            downloadEvent.CorrelationId);

        _eventPublisher.Publish(downloadEvent);
    }
}

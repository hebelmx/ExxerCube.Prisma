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
/// <para>
/// Before republishing it <em>resolves shared storage</em> (ADR-011): the event carries only a
/// storage-relative <see cref="DocumentDownloadedEvent.Path"/> (the two processes may mount the shared volume
/// at different absolute paths), so the forwarder turns it into a locally-loadable absolute path via
/// <see cref="IStoragePathResolver"/> and stamps it onto <see cref="DocumentDownloadedEvent.FileName"/> — the
/// field the orchestrator opens with the file loader. The relative <c>Path</c> is preserved.
/// </para>
/// <para>
/// Tolerant of configuration faults: when the event has no relative path, or resolution fails (unconfigured
/// base, escaping path), it logs and forwards the event <em>unchanged</em> rather than dropping it — the
/// orchestrator already logs-and-continues when a file cannot be loaded, so a storage misconfiguration
/// degrades gracefully instead of crashing ingestion. Kept as a thin, host-agnostic seam so this is
/// unit-testable without a live SignalR connection.
/// </para>
/// </remarks>
public sealed class IngestionEventForwarder
{
    private readonly IEventPublisher _eventPublisher;
    private readonly IStoragePathResolver _storagePathResolver;
    private readonly ILogger<IngestionEventForwarder> _logger;

    /// <summary>Initializes a new instance of the <see cref="IngestionEventForwarder"/> class.</summary>
    /// <param name="eventPublisher">The local event publisher the pipeline subscribes to.</param>
    /// <param name="storagePathResolver">Resolves the event's storage-relative path against this process's shared-storage base.</param>
    /// <param name="logger">The logger.</param>
    public IngestionEventForwarder(
        IEventPublisher eventPublisher,
        IStoragePathResolver storagePathResolver,
        ILogger<IngestionEventForwarder> logger)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _storagePathResolver = storagePathResolver ?? throw new ArgumentNullException(nameof(storagePathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Republishes a cross-process <see cref="DocumentDownloadedEvent"/> onto the local event stream, after
    /// resolving its storage-relative path to a locally-loadable absolute path.
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

        var resolvedEvent = ResolveStoragePath(downloadEvent);

        _logger.LogInformation(
            "Forwarding DocumentDownloadedEvent {FileId} (corr {CorrelationId}) from the ingestion hub to the local pipeline",
            resolvedEvent.FileId,
            resolvedEvent.CorrelationId);

        _eventPublisher.Publish(resolvedEvent);
    }

    /// <summary>
    /// Resolves the event's storage-relative <see cref="DocumentDownloadedEvent.Path"/> against the shared
    /// storage base and stamps the absolute result onto <see cref="DocumentDownloadedEvent.FileName"/>.
    /// Returns the event unchanged when there is no relative path or resolution fails (tolerant fallback).
    /// </summary>
    private DocumentDownloadedEvent ResolveStoragePath(DocumentDownloadedEvent downloadEvent)
    {
        if (string.IsNullOrWhiteSpace(downloadEvent.Path))
        {
            // No shared-storage hint (in-process / single-service flow): forward as-is.
            return downloadEvent;
        }

        var resolution = _storagePathResolver.Resolve(downloadEvent.Path);
        if (resolution.IsFailure)
        {
            _logger.LogWarning(
                "Could not resolve shared storage for DocumentDownloadedEvent {FileId} (relative path '{RelativePath}'): {Error}. " +
                "Forwarding with the original file name; the pipeline will log-and-continue if it cannot load the file.",
                downloadEvent.FileId,
                downloadEvent.Path,
                resolution.Error);
            return downloadEvent;
        }

        _logger.LogDebug(
            "Resolved shared storage for DocumentDownloadedEvent {FileId}: '{RelativePath}' -> '{AbsolutePath}'",
            downloadEvent.FileId,
            downloadEvent.Path,
            resolution.Value);

        return downloadEvent with { FileName = resolution.Value! };
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Enum;
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
/// Before republishing it validates the clearance token on the event (MVP-PATH 1.5, A5): only events whose
/// token carries <see cref="ProcessClearance.Download"/> and whose <c>file_id</c> claim matches
/// <see cref="DocumentDownloadedEvent.FileId"/> are forwarded. All others are logged and silently dropped
/// (fail-closed; never throw). This enforces the Orion Downloader → Athena Extractor trust boundary.
/// </para>
/// <para>
/// After clearance validation it <em>resolves shared storage</em> (ADR-011): the event carries only a
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
    private readonly IProcessClearanceTokenService _clearanceTokenService;
    private readonly ILogger<IngestionEventForwarder> _logger;

    /// <summary>Initializes a new instance of the <see cref="IngestionEventForwarder"/> class.</summary>
    /// <param name="eventPublisher">The local event publisher the pipeline subscribes to.</param>
    /// <param name="storagePathResolver">Resolves the event's storage-relative path against this process's shared-storage base.</param>
    /// <param name="clearanceTokenService">Validates the per-document process clearance token carried on each cross-process event (MVP-PATH 1.5, A5).</param>
    /// <param name="logger">The logger.</param>
    public IngestionEventForwarder(
        IEventPublisher eventPublisher,
        IStoragePathResolver storagePathResolver,
        IProcessClearanceTokenService clearanceTokenService,
        ILogger<IngestionEventForwarder> logger)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _storagePathResolver = storagePathResolver ?? throw new ArgumentNullException(nameof(storagePathResolver));
        _clearanceTokenService = clearanceTokenService ?? throw new ArgumentNullException(nameof(clearanceTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates the clearance token on a cross-process <see cref="DocumentDownloadedEvent"/> and, when
    /// authorised, republishes it onto the local event stream after resolving its storage-relative path.
    /// A null event or one that fails clearance validation is silently dropped (never throws; fail-closed).
    /// </summary>
    /// <param name="downloadEvent">The event received from the Orion ingestion hub.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ForwardAsync(DocumentDownloadedEvent? downloadEvent,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (downloadEvent is null)
        {
            _logger.LogWarning("Received a null DocumentDownloadedEvent from the ingestion hub; ignoring");
            return;
        }

        // MVP-PATH 1.5 A5: validate the process clearance token before forwarding.
        if (string.IsNullOrWhiteSpace(downloadEvent.ClearanceToken))
        {
            LogAndReject(downloadEvent.FileId, actorId: null, "missing clearance token");
            return;
        }

        var validation = await _clearanceTokenService
            .ValidateAsync(downloadEvent.ClearanceToken, cancellationToken)
            .ConfigureAwait(false);

        if (validation.IsFailure)
        {
            LogAndReject(downloadEvent.FileId, actorId: null, validation.Error ?? "token validation failed");
            return;
        }

        var claims = validation.Value!;

        if (claims.Clearance != ProcessClearance.Download)
        {
            LogAndReject(downloadEvent.FileId, claims.ActorId,
                $"clearance {claims.Clearance} not permitted on ingestion edge");
            return;
        }

        if (claims.FileId != downloadEvent.FileId)
        {
            LogAndReject(downloadEvent.FileId, claims.ActorId,
                "clearance token file_id mismatch (replay/tamper)");
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
    /// Logs a rejection at Warning level and returns without publishing. Never throws.
    /// </summary>
    private void LogAndReject(Guid fileId, string? actorId, string reason)
    {
        _logger.LogWarning(
            "IngestionEventForwarder: rejected DocumentDownloadedEvent {FileId} (actor {ActorId}): {Reason}",
            fileId,
            actorId ?? "(unknown)",
            reason);
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

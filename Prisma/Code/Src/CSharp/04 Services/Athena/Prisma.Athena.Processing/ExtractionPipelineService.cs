using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing;

/// <summary>
/// Drives the <em>Extractor</em> actor of the 3-process split (MVP-PATH 1.4 Reconciliator edge, ADR-011):
/// it subscribes to <see cref="DocumentDownloadedEvent"/> (republished from the Orion Downloader over the
/// 1.3 ingestion edge), runs the Extractor half (Quality → OCR → Fusion via
/// <see cref="ExtractionOrchestrator"/>), persists the fused expediente to shared storage via
/// <see cref="IExpedienteHandoffStore"/>, and broadcasts an <see cref="ExtractionCompletedEvent"/> carrying the
/// storage-relative handoff path to the downstream Reconciliator over <see cref="IExxerHub{T}"/>.
/// </summary>
/// <remarks>
/// Host-agnostic and Railway-Oriented: a quality rejection or a missing fused expediente ends the run cleanly
/// (no handoff); a storage or broadcast failure is a logged <see cref="Result"/> failure, never a throw — a
/// transient downstream outage must not crash the Extractor. The handoff is a shared-storage <em>reference</em>
/// (the raw document never crosses this edge — data minimization, MVP A5).
/// </remarks>
public sealed class ExtractionPipelineService : IReadinessProbe
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ExtractionOrchestrator _extractionOrchestrator;
    private readonly IExpedienteHandoffStore _handoffStore;
    private readonly IExxerHub<ExtractionCompletedEvent> _reconciliationHub;
    private readonly ILogger<ExtractionPipelineService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ISiaraActorIdentityProvider? _actorIdentityProvider;
    private readonly ProcessClearance _processClearance;
    private IDisposable? _subscription;

    /// <summary>
    /// Gets a value indicating whether the Extractor pipeline has started and subscribed to the document
    /// stream. Drives the Athena Extractor worker's readiness probe (MVP-PATH 4.2 / E1).
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <inheritdoc />
    bool IReadinessProbe.IsReady => IsStarted;

    /// <summary>Initializes a new instance of the <see cref="ExtractionPipelineService"/> class.</summary>
    /// <param name="eventPublisher">The local event stream the ingestion forwarder republishes onto.</param>
    /// <param name="extractionOrchestrator">The Extractor half (Stages 1–3).</param>
    /// <param name="handoffStore">Persists the fused expediente to shared storage.</param>
    /// <param name="reconciliationHub">Broadcasts the handoff event to the Reconciliator.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeFactory">
    /// Optional scope factory used to resolve <see cref="IAuditLogger"/> per audit call (avoids captive
    /// dependency: IAuditLogger is scoped, ExtractionPipelineService is singleton in the worker host).
    /// When <see langword="null"/>, audit calls are silently skipped (no-op). MVP-PATH 1.6 A6.
    /// </param>
    /// <param name="actorIdentityProvider">
    /// Optional provider of the current process actor identity. When <see langword="null"/>, audit records
    /// use a degraded identity ("system") — fail-open so the pipeline is never blocked by an audit failure.
    /// MVP-PATH 1.6 A6.
    /// </param>
    /// <param name="processClearance">
    /// The clearance level of this process, stamped on every audit record's <c>ActionDetails</c> JSON.
    /// Defaults to <see cref="ProcessClearance.Extract"/> (Athena Extractor). MVP-PATH 1.6 A6.
    /// </param>
    public ExtractionPipelineService(
        IEventPublisher eventPublisher,
        ExtractionOrchestrator extractionOrchestrator,
        IExpedienteHandoffStore handoffStore,
        IExxerHub<ExtractionCompletedEvent> reconciliationHub,
        ILogger<ExtractionPipelineService> logger,
        IServiceScopeFactory? scopeFactory = null,
        ISiaraActorIdentityProvider? actorIdentityProvider = null,
        ProcessClearance processClearance = ProcessClearance.Extract)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _extractionOrchestrator = extractionOrchestrator ?? throw new ArgumentNullException(nameof(extractionOrchestrator));
        _handoffStore = handoffStore ?? throw new ArgumentNullException(nameof(handoffStore));
        _reconciliationHub = reconciliationHub ?? throw new ArgumentNullException(nameof(reconciliationHub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory;
        _actorIdentityProvider = actorIdentityProvider;
        _processClearance = processClearance;
    }

    /// <summary>Subscribes to the local <see cref="DocumentDownloadedEvent"/> stream and runs extraction per document.</summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A completed task (the subscription runs until disposed).</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Extraction pipeline starting (Extractor actor): subscribing to DocumentDownloadedEvent");

        _subscription = _eventPublisher.GetEventStream<DocumentDownloadedEvent>()
            .Subscribe(
                onNext: async downloadEvent =>
                {
                    try
                    {
                        await ProcessAsync(downloadEvent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error extracting DocumentDownloadedEvent. FileId: {FileId}",
                            downloadEvent.FileId);
                    }
                },
                onError: ex => _logger.LogError(ex, "Error in DocumentDownloadedEvent stream"));

        IsStarted = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the Extractor half for one document, then persists + broadcasts the handoff to the Reconciliator.
    /// </summary>
    /// <param name="downloadEvent">The downloaded document event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success (incl. clean no-handoff outcomes), or a failure when storage/broadcast fails.</returns>
    public async Task<Result> ProcessAsync(
        DocumentDownloadedEvent downloadEvent,
        CancellationToken cancellationToken = default)
    {
        if (downloadEvent is null)
        {
            return Result.WithFailure("Download event cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var fileId = downloadEvent.FileId;
        var correlationId = downloadEvent.CorrelationId;
        // correlationId is Guid? (DomainEvent base) — convert once to string for audit calls.
        var correlationIdStr = correlationId.GetValueOrDefault().ToString();

        // Audit: extraction started.
        await EmitAuditAsync(
            AuditActionType.Extraction,
            ProcessingStage.Extraction,
            fileId: fileId.ToString(),
            correlationId: correlationIdStr,
            success: true,
            actionKey: "ExtractionStarted",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var extraction = await _extractionOrchestrator.ExtractAsync(downloadEvent, cancellationToken);

        if (extraction.QualityRejected)
        {
            _logger.LogInformation(
                "Extraction stopped: quality rejected, no handoff to the Reconciliator. FileId: {FileId}", fileId);

            // Audit: quality rejected.
            await EmitAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.Extraction,
                fileId: fileId.ToString(),
                correlationId: correlationIdStr,
                success: false,
                actionKey: "QualityRejected",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        var expediente = extraction.FusionResult?.FusedExpediente;
        if (expediente is null)
        {
            _logger.LogWarning(
                "Extraction produced no fused expediente; no handoff to the Reconciliator. FileId: {FileId}", fileId);

            // Audit: extraction failed (no expediente produced).
            await EmitAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.Extraction,
                fileId: fileId.ToString(),
                correlationId: correlationIdStr,
                success: false,
                actionKey: "ExtractionFailed_NoExpediente",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        var relativePath = BuildHandoffRelativePath(fileId);
        var save = await _handoffStore.SaveAsync(expediente, relativePath, cancellationToken);
        if (save.IsFailure)
        {
            _logger.LogWarning(
                "Failed to persist fused expediente handoff for {FileId}: {Errors}",
                fileId, string.Join(", ", save.Errors));

            // Audit: extraction failed (store failure).
            await EmitAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.Extraction,
                fileId: fileId.ToString(),
                correlationId: correlationIdStr,
                success: false,
                actionKey: "ExtractionFailed_StoreFailed",
                errorMessage: string.Join(", ", save.Errors),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.WithFailure(save.Errors);
        }

        var completedEvent = new ExtractionCompletedEvent
        {
            FileId = fileId,
            CorrelationId = correlationId,
            Path = save.Value!,
            FieldsFused = extraction.FusionResult?.FieldResults.Count ?? 0,
            ConflictsDetected = extraction.FusionResult?.ConflictingFields.Count ?? 0,
        };

        var broadcast = await _reconciliationHub.SendToAllAsync(completedEvent, cancellationToken);
        if (broadcast.IsFailure)
        {
            _logger.LogWarning(
                "Failed to broadcast ExtractionCompletedEvent for {FileId}: {Errors}",
                fileId, string.Join(", ", broadcast.Errors));

            // Audit: extraction failed (broadcast failure).
            await EmitAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.Extraction,
                fileId: fileId.ToString(),
                correlationId: correlationIdStr,
                success: false,
                actionKey: "ExtractionFailed_BroadcastFailed",
                errorMessage: string.Join(", ", broadcast.Errors),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.WithFailure(broadcast.Errors);
        }

        _logger.LogInformation(
            "Extraction complete; handoff broadcast to the Reconciliator. FileId: {FileId}, Path: {Path}",
            fileId, save.Value);

        // Audit: extraction + handoff completed.
        await EmitAuditAsync(
            AuditActionType.Extraction,
            ProcessingStage.Extraction,
            fileId: fileId.ToString(),
            correlationId: correlationIdStr,
            success: true,
            actionKey: "ExtractionCompleted",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Builds the storage-relative handoff path (forward-slash, mount-path independent), mirroring the document
    /// partitioning: <c>YYYY/MM/DD/{fileId}.fusion.json</c>.
    /// </summary>
    private static string BuildHandoffRelativePath(Guid fileId)
    {
        var now = DateTime.UtcNow;
        return $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}/{fileId}.fusion.json";
    }

    // -------------------------------------------------------------------------
    // Per-process audit helpers (MVP-PATH 1.6 A6)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits a single audit record stamped with the current process identity.
    /// Fail-open: any audit failure is logged at Warning and swallowed so the pipeline is never blocked.
    /// </summary>
    private async Task EmitAuditAsync(
        AuditActionType actionType,
        ProcessingStage stage,
        string? fileId,
        string correlationId,
        bool success,
        string actionKey,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory is null)
        {
            return; // Audit not wired — silent no-op.
        }

        try
        {
            var (actorId, details) = await ResolveProcessIdentityAsync(actionKey, cancellationToken)
                .ConfigureAwait(false);

            using var scope = _scopeFactory.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await auditLogger.LogAuditAsync(
                actionType,
                stage,
                fileId,
                correlationId,
                userId: actorId,
                actionDetails: details,
                success: success,
                errorMessage: errorMessage,
                cancellationToken: cancellationToken,
                processId: actorId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Audit emission failed for action {Action} (fail-open — pipeline continues). CorrelationId: {CorrelationId}",
                actionKey, correlationId);
        }
    }

    /// <summary>
    /// Resolves the current actor identity and builds the process-identity audit-details JSON.
    /// Fail-open: if the provider is null or resolution fails, returns a degraded ("unknown-process") identity.
    /// </summary>
    private async Task<(string actorId, string auditDetails)> ResolveProcessIdentityAsync(
        string action,
        CancellationToken ct)
    {
        if (_actorIdentityProvider is null)
        {
            return ("system", JsonSerializer.Serialize(new { action }));
        }

        var actorResult = await _actorIdentityProvider.GetCurrentActorAsync(ct).ConfigureAwait(false);
        if (actorResult.IsFailure)
        {
            _logger.LogWarning(
                "Process identity resolution failed for Athena audit (fail-open). Error: {Error}",
                string.Join(", ", actorResult.Errors));
            return ("unknown-process", JsonSerializer.Serialize(new
            {
                action,
                error = string.Join(", ", actorResult.Errors)
            }));
        }

        var actor = actorResult.Value!;
        return (actor.ActorId, BuildProcessAuditDetails(actor, _processClearance, action));
    }

    /// <summary>
    /// Serializes the process-identity payload that rides in <c>ActionDetails</c> on every worker audit record.
    /// </summary>
    private static string BuildProcessAuditDetails(SiaraActor actor, ProcessClearance clearance, string action)
        => JsonSerializer.Serialize(new
        {
            processId = actor.ActorId,
            processType = actor.ActorType.ToString(),
            processClearance = clearance.ToString(),
            displayName = actor.DisplayName,
            action
        });
}

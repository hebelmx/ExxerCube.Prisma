using System;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing.Reconciliation;

/// <summary>
/// Drives the <em>Reconciliator</em> actor of the 3-process split (MVP-PATH 1.4 Reconciliator edge, ADR-011):
/// it subscribes to <see cref="ExtractionCompletedEvent"/> (republished from the Athena Extractor over the
/// reconciliation edge), loads the fused expediente from shared storage via <see cref="IExpedienteHandoffStore"/>,
/// runs the Reconciliator half (Classification → Export via <see cref="ReconciliationOrchestrator"/>), and emits
/// the terminal <see cref="DocumentProcessingCompletedEvent"/>.
/// </summary>
/// <remarks>
/// Host-agnostic and Railway-Oriented: a failed handoff load is a logged <see cref="Result"/> failure, never a
/// throw — a missing or corrupt artifact must not crash the Reconciliator. The raw document never crosses this
/// edge; only the derived, fused expediente is loaded (data minimization, MVP A5).
/// </remarks>
public sealed class ReconciliationPipelineService
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ReconciliationOrchestrator _reconciliationOrchestrator;
    private readonly IExpedienteHandoffStore _handoffStore;
    private readonly ILogger<ReconciliationPipelineService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ISiaraActorIdentityProvider? _actorIdentityProvider;
    private readonly ProcessClearance _processClearance;
    private IDisposable? _subscription;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationPipelineService"/> class.</summary>
    /// <param name="eventPublisher">The local event stream the reconciliation forwarder republishes onto.</param>
    /// <param name="reconciliationOrchestrator">The Reconciliator half (Stages 4–5).</param>
    /// <param name="handoffStore">Loads the fused expediente from shared storage.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeFactory">
    /// Optional scope factory used to resolve <see cref="IAuditLogger"/> per audit call (avoids captive
    /// dependency: IAuditLogger is scoped, ReconciliationPipelineService is singleton in the worker host).
    /// When <see langword="null"/>, audit calls are silently skipped (no-op). MVP-PATH 1.6 A6.
    /// </param>
    /// <param name="actorIdentityProvider">
    /// Optional provider of the current process actor identity. When <see langword="null"/>, audit records
    /// use a degraded identity ("system") — fail-open so the pipeline is never blocked by an audit failure.
    /// MVP-PATH 1.6 A6.
    /// </param>
    /// <param name="processClearance">
    /// The clearance level of this process, stamped on every audit record's <c>ActionDetails</c> JSON.
    /// Defaults to <see cref="ProcessClearance.Reconcile"/> (Reconciliator). MVP-PATH 1.6 A6.
    /// </param>
    public ReconciliationPipelineService(
        IEventPublisher eventPublisher,
        ReconciliationOrchestrator reconciliationOrchestrator,
        IExpedienteHandoffStore handoffStore,
        ILogger<ReconciliationPipelineService> logger,
        IServiceScopeFactory? scopeFactory = null,
        ISiaraActorIdentityProvider? actorIdentityProvider = null,
        ProcessClearance processClearance = ProcessClearance.Reconcile)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _reconciliationOrchestrator = reconciliationOrchestrator ?? throw new ArgumentNullException(nameof(reconciliationOrchestrator));
        _handoffStore = handoffStore ?? throw new ArgumentNullException(nameof(handoffStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory;
        _actorIdentityProvider = actorIdentityProvider;
        _processClearance = processClearance;
    }

    /// <summary>Subscribes to the local <see cref="ExtractionCompletedEvent"/> stream and reconciles per document.</summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A completed task (the subscription runs until disposed).</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Reconciliation pipeline starting (Reconciliator actor): subscribing to ExtractionCompletedEvent");

        _subscription = _eventPublisher.GetEventStream<ExtractionCompletedEvent>()
            .Subscribe(
                onNext: async completedEvent =>
                {
                    try
                    {
                        await ProcessAsync(completedEvent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error reconciling ExtractionCompletedEvent. FileId: {FileId}",
                            completedEvent.FileId);
                    }
                },
                onError: ex => _logger.LogError(ex, "Error in ExtractionCompletedEvent stream"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads the fused expediente handoff and runs Classification → Export, then emits the terminal completion event.
    /// </summary>
    /// <param name="completedEvent">The extraction-completed handoff event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success once reconciliation ran, or a failure when the handoff cannot be loaded.</returns>
    public async Task<Result> ProcessAsync(
        ExtractionCompletedEvent completedEvent,
        CancellationToken cancellationToken = default)
    {
        if (completedEvent is null)
        {
            return Result.WithFailure("Extraction completed event cannot be null");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        var fileId = completedEvent.FileId;
        var correlationId = completedEvent.CorrelationId;
        // correlationId is Guid? (DomainEvent base) — convert once to string for audit calls.
        var correlationIdStr = correlationId.GetValueOrDefault().ToString();

        // Audit: reconciliation started.
        await EmitAuditAsync(
            AuditActionType.Classification,
            ProcessingStage.DecisionLogic,
            fileId: fileId.ToString(),
            correlationId: correlationIdStr,
            success: true,
            actionKey: "ReconciliationStarted",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var load = await _handoffStore.LoadAsync(completedEvent.Path, cancellationToken);
        if (load.IsFailure)
        {
            _logger.LogWarning(
                "Could not load the fused expediente handoff for {FileId} (path '{Path}'): {Errors}. Skipping reconciliation.",
                fileId, completedEvent.Path, string.Join(", ", load.Errors));

            // Audit: handoff load failed.
            await EmitAuditAsync(
                AuditActionType.Classification,
                ProcessingStage.DecisionLogic,
                fileId: fileId.ToString(),
                correlationId: correlationIdStr,
                success: false,
                actionKey: "HandoffLoadFailed",
                errorMessage: $"path='{completedEvent.Path}': {string.Join(", ", load.Errors)}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.WithFailure(load.Errors);
        }

        var fusionResult = new FusionResult { FusedExpediente = load.Value };

        var stagesCompleted = await _reconciliationOrchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: correlationId,
            isComplete: completedEvent.IsComplete,
            cancellationToken: cancellationToken);

        _eventPublisher.Publish(new DocumentProcessingCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            TotalProcessingTime = TimeSpan.Zero,
            AutoProcessed = true
        });

        _logger.LogInformation(
            "Reconciliation complete. FileId: {FileId}, ReconciliationStages: {Stages}",
            fileId, stagesCompleted);

        // Audit: reconciliation + export completed.
        await EmitAuditAsync(
            AuditActionType.Export,
            ProcessingStage.Export,
            fileId: fileId.ToString(),
            correlationId: correlationIdStr,
            success: true,
            actionKey: "ReconciliationCompleted",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Result.Success();
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
                "Process identity resolution failed for Reconciliator audit (fail-open). Error: {Error}",
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

using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
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
    private IDisposable? _subscription;

    /// <summary>Initializes a new instance of the <see cref="ReconciliationPipelineService"/> class.</summary>
    /// <param name="eventPublisher">The local event stream the reconciliation forwarder republishes onto.</param>
    /// <param name="reconciliationOrchestrator">The Reconciliator half (Stages 4–5).</param>
    /// <param name="handoffStore">Loads the fused expediente from shared storage.</param>
    /// <param name="logger">The logger.</param>
    public ReconciliationPipelineService(
        IEventPublisher eventPublisher,
        ReconciliationOrchestrator reconciliationOrchestrator,
        IExpedienteHandoffStore handoffStore,
        ILogger<ReconciliationPipelineService> logger)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _reconciliationOrchestrator = reconciliationOrchestrator ?? throw new ArgumentNullException(nameof(reconciliationOrchestrator));
        _handoffStore = handoffStore ?? throw new ArgumentNullException(nameof(handoffStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var load = await _handoffStore.LoadAsync(completedEvent.Path, cancellationToken);
        if (load.IsFailure)
        {
            _logger.LogWarning(
                "Could not load the fused expediente handoff for {FileId} (path '{Path}'): {Errors}. Skipping reconciliation.",
                fileId, completedEvent.Path, string.Join(", ", load.Errors));
            return Result.WithFailure(load.Errors);
        }

        var fusionResult = new FusionResult { FusedExpediente = load.Value };

        var stagesCompleted = await _reconciliationOrchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: correlationId,
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

        return Result.Success();
    }
}

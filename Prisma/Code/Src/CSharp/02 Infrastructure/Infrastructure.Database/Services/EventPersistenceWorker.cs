// <copyright file="EventPersistenceWorker.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Database.Services;

/// <summary>
/// Background service that subscribes to domain events and persists them to the database.
/// Implements defensive intelligence - never blocks main processing flow.
/// </summary>
public class EventPersistenceWorker : BackgroundService
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<EventPersistenceWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private IDisposable? _subscription;

    /// <summary>
    /// JSON serializer options optimized for .NET 10 record types with init properties.
    /// Uses default PascalCase naming to match C# property names exactly (for internal storage).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false, // Compact JSON for database storage
        DefaultIgnoreCondition = JsonIgnoreCondition.Never, // Include all properties
        PropertyNameCaseInsensitive = true, // Case-insensitive deserialization (defensive)
        PropertyNamingPolicy = null, // Use PascalCase (default) to match C# property names
        Converters = { new JsonStringEnumConverter() }, // Serialize enums as strings
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPersistenceWorker"/> class.
    /// </summary>
    /// <param name="eventPublisher">Event publisher to subscribe to.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="serviceScopeFactory">Service scope factory for creating scoped DbContext.</param>
    public EventPersistenceWorker(
        IEventPublisher eventPublisher,
        ILogger<EventPersistenceWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Executes the background service, subscribing to all domain events.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to signal shutdown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventPersistenceWorker starting - subscribing to domain events");

        _subscription = _eventPublisher
            .GetAllEventsStream()
            .Subscribe(
                onNext: async domainEvent => await PersistEventAsync(domainEvent, stoppingToken),
                onError: ex =>
                {
                    _logger.LogError(ex, "Error in event stream subscription");
                    // Defensive Intelligence: Don't throw - log and continue
                },
                onCompleted: () => _logger.LogInformation("Event stream completed"));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Persists a domain event to the database as an AuditRecord and records it in the
    /// OutboxEvents table for reliable at-least-once delivery (NFR14).
    /// </summary>
    /// <param name="domainEvent">The domain event to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PersistEventAsync(DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IPrismaDbContext>();

            // --- Outbox write (NFR14): persist the event payload first so the OutboxRetryWorker
            //     can detect and re-publish it if the audit-record write below fails.
            var outboxEntry = new OutboxEvent
            {
                OutboxEventId = domainEvent.EventId,
                EventType = domainEvent.EventType,
                Payload = JsonSerializer.Serialize(domainEvent, JsonOptions),
                OccurredAt = domainEvent.Timestamp,
            };

            // Guard against duplicate entries (idempotent replay from the retry worker).
            var alreadyExists = await dbContext.OutboxEvents
                .AnyAsync(e => e.OutboxEventId == domainEvent.EventId, cancellationToken)
                .ConfigureAwait(false);

            if (!alreadyExists)
            {
                dbContext.OutboxEvents.Add(outboxEntry);
            }

            // --- Audit record write (existing behaviour).
            var auditRecord = MapEventToAuditRecord(domainEvent);
            dbContext.AuditRecords.Add(auditRecord);

            // Commit both in one round-trip.
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Mark the outbox entry as processed now that the audit record is safely stored.
            // Fetch the tracked entity and update it.
            var tracked = await dbContext.OutboxEvents
                .FindAsync(new object[] { domainEvent.EventId }, cancellationToken)
                .ConfigureAwait(false);

            if (tracked is not null)
            {
                tracked.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug(
                "Persisted event {EventType} with ID {EventId} to database (outbox + audit)",
                domainEvent.EventType,
                domainEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist event {EventType} with ID {EventId} - Defensive Intelligence: continuing. " +
                "OutboxRetryWorker will re-publish this event on the next scan tick.",
                domainEvent.EventType,
                domainEvent.EventId);
            // Defensive Intelligence: Don't throw - event persistence failure should not break the system.
            // The outbox entry written above (if committed) remains unprocessed and will be re-published
            // by OutboxRetryWorker on the next scan tick.
        }
    }

    /// <summary>
    /// Maps a domain event to an AuditRecord entity.
    /// </summary>
    /// <param name="domainEvent">The domain event to map.</param>
    /// <returns>An AuditRecord entity.</returns>
    private AuditRecord MapEventToAuditRecord(DomainEvent domainEvent)
    {
        var (actionType, stage, fileId) = GetAuditDetails(domainEvent);

        return new AuditRecord
        {
            AuditId = Guid.NewGuid().ToString(),
            CorrelationId = domainEvent.CorrelationId?.ToString() ?? string.Empty,
            FileId = fileId,
            ActionType = actionType,
            ActionDetails = JsonSerializer.Serialize(domainEvent, JsonOptions),
            UserId = null, // System action
            Timestamp = domainEvent.Timestamp,
            Stage = stage,
            Success = !IsErrorEvent(domainEvent),
            ErrorMessage = GetErrorMessage(domainEvent),
        };
    }

    /// <summary>
    /// Determines the audit action type, processing stage, and file ID from a domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>Tuple of action type, stage, and file ID.</returns>
    private (AuditActionType ActionType, ProcessingStage Stage, string? FileId) GetAuditDetails(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            DocumentDownloadedEvent e => (AuditActionType.Download, ProcessingStage.Ingestion, e.FileId.ToString()),
            QualityAnalysisCompletedEvent e => (AuditActionType.Extraction, ProcessingStage.Extraction, e.FileId.ToString()),
            OcrCompletedEvent e => (AuditActionType.Extraction, ProcessingStage.Extraction, e.FileId.ToString()),
            ClassificationCompletedEvent e => (AuditActionType.Classification, ProcessingStage.DecisionLogic, e.FileId.ToString()),
            ConflictDetectedEvent e => (AuditActionType.Review, ProcessingStage.DecisionLogic, e.FileId.ToString()),
            DocumentFlaggedForReviewEvent e => (AuditActionType.Review, ProcessingStage.DecisionLogic, e.FileId.ToString()),
            DocumentProcessingCompletedEvent e => (AuditActionType.Export, ProcessingStage.Export, e.FileId.ToString()),
            ProcessingErrorEvent e => (AuditActionType.Other, ProcessingStage.Unknown, e.FileId?.ToString()),
            _ => (AuditActionType.Other, ProcessingStage.Unknown, null),
        };
    }

    /// <summary>
    /// Determines if a domain event represents an error.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>True if the event is an error event.</returns>
    private bool IsErrorEvent(DomainEvent domainEvent)
    {
        return domainEvent is ProcessingErrorEvent;
    }

    /// <summary>
    /// Extracts error message from an error event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>Error message if event is an error, otherwise null.</returns>
    private string? GetErrorMessage(DomainEvent domainEvent)
    {
        return domainEvent is ProcessingErrorEvent errorEvent ? errorEvent.ErrorMessage : null;
    }

    /// <summary>
    /// Disposes the event stream subscription on shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventPersistenceWorker stopping - unsubscribing from domain events");
        _subscription?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}

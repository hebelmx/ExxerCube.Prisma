// <copyright file="DomainEvent.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Base class for all domain events in the system.
/// Events are published via IObservable (Reactive Extensions) and consumed by:
/// - Background workers (persist to database)
/// - SignalR hubs (broadcast to UI in real-time)
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DocumentDownloadedEvent), "DocumentDownloadedEvent")]
[JsonDerivedType(typeof(QualityAnalysisCompletedEvent), "QualityAnalysisCompletedEvent")]
[JsonDerivedType(typeof(OcrCompletedEvent), "OcrCompletedEvent")]
[JsonDerivedType(typeof(ClassificationCompletedEvent), "ClassificationCompletedEvent")]
[JsonDerivedType(typeof(ConflictDetectedEvent), "ConflictDetectedEvent")]
[JsonDerivedType(typeof(DocumentFlaggedForReviewEvent), "DocumentFlaggedForReviewEvent")]
[JsonDerivedType(typeof(DocumentProcessingCompletedEvent), "DocumentProcessingCompletedEvent")]
[JsonDerivedType(typeof(ProcessingErrorEvent), "ProcessingErrorEvent")]
[JsonDerivedType(typeof(QualityRejectedEvent), "QualityRejectedEvent")]
[JsonDerivedType(typeof(FusionCompletedEvent), "FusionCompletedEvent")]
[JsonDerivedType(typeof(ExportCompletedEvent), "ExportCompletedEvent")]
[JsonDerivedType(typeof(ProcessingEvent), "ProcessingEvent")]
[JsonDerivedType(typeof(ProcessingCompletedEvent), "ProcessingCompletedEvent")]
[JsonDerivedType(typeof(QualityCompletedEvent), "QualityCompletedEvent")]
public abstract record DomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When this event occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Type of event (class name by default).
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID to link related events (e.g., all events for processing one document).
    /// </summary>
    public Guid? CorrelationId { get; init; }
}

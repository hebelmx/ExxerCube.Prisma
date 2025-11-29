// <copyright file="DomainEvent.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Base class for all domain events in the system.
/// Events are published via IObservable (Reactive Extensions) and consumed by:
/// - Background workers (persist to database)
/// - SignalR hubs (broadcast to UI in real-time)
/// </summary>
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

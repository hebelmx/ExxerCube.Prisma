// <copyright file="EventPublisher.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Infrastructure.Events;

/// <summary>
/// Implementation of event publisher using Reactive Extensions Subject.
/// Thread-safe singleton registered in DI container.
/// </summary>
public class EventPublisher : IEventPublisher, IDisposable
{
    private readonly Subject<DomainEvent> _eventStream = new();
    private readonly ILogger<EventPublisher> _logger;
    private bool _disposed;

    /// <summary>
    /// Creates a new event publisher backed by an Rx subject.
    /// </summary>
    /// <param name="logger">Logger used for diagnostics.</param>
    public EventPublisher(ILogger<EventPublisher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Publishes a domain event to all subscribers in a fire-and-forget manner.
    /// </summary>
    /// <typeparam name="TEvent">Type of the domain event.</typeparam>
    /// <param name="domainEvent">The event instance to publish.</param>
    public void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent
    {
        if (_disposed)
        {
            _logger.LogWarning("Attempted to publish event after EventPublisher disposed");
            return;
        }

        try
        {
            _logger.LogDebug(
                "Publishing event {EventType} with ID {EventId} (Correlation: {CorrelationId})",
                domainEvent.EventType,
                domainEvent.EventId,
                domainEvent.CorrelationId);

            _eventStream.OnNext(domainEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType}", domainEvent.EventType);
            // IMPORTANT: Don't throw - event publishing should NEVER break main processing flow
            // This is part of "Defensive Intelligence" - system continues even if observability fails
        }
    }

    /// <summary>
    /// Returns an observable stream of events for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event to subscribe to.</typeparam>
    /// <returns>An observable stream of the specified event type.</returns>
    public IObservable<TEvent> GetEventStream<TEvent>() where TEvent : DomainEvent
    {
        return _eventStream
            .OfType<TEvent>()
            .AsObservable();
    }

    /// <summary>
    /// Returns an observable stream of all domain events.
    /// </summary>
    public IObservable<DomainEvent> GetAllEventsStream()
    {
        return _eventStream.AsObservable();
    }

    /// <summary>
    /// Disposes the underlying event stream and prevents further publishing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _eventStream?.Dispose();
    }
}
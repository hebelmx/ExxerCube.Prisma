using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Central event publisher using Reactive Extensions (Rx.NET).
/// All domain events flow through this service as IObservable streams.
/// Subscribers: Background workers (persist), SignalR hubs (broadcast to UI).
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event to all subscribers.
    /// Non-blocking - publishing never fails the main processing flow.
    /// </summary>
    void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent;

    /// <summary>
    /// Subscribe to all events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">Domain event type to observe.</typeparam>
    IObservable<TEvent> GetEventStream<TEvent>() where TEvent : DomainEvent;

    /// <summary>
    /// Subscribe to all events (any type).
    /// </summary>
    IObservable<DomainEvent> GetAllEventsStream();
}
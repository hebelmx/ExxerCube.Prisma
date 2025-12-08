namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Event subscription registration for wiring handlers to event names.
/// </summary>
public interface IEventSubscriber
{
    /// <summary>
    /// Subscribes a handler to a specific event name.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventName">The event name constant (e.g., DocumentEvents.DocumentDownloaded).</param>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <remarks>
    /// ITDD Contract:
    /// - Multiple handlers can subscribe to the same event
    /// - Handlers are invoked in registration order (for in-memory) or concurrently (for distributed)
    /// - Liskov: All implementations must support multiple subscribers
    /// </remarks>
    void Subscribe<TEvent>(string eventName, IEventHandler<TEvent> handler)
        where TEvent : notnull;
}

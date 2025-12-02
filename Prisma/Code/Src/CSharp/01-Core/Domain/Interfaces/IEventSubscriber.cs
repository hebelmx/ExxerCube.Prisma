using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExxerCube.Prisma.Domain.Interfaces.Contracts;

/// <summary>
/// Event handler abstraction for processing domain events.
/// Implementations must be substitutable (Liskov) - any handler should be invokable
/// without knowledge of the underlying event bus mechanism.
/// </summary>
/// <typeparam name="TEvent">The event type this handler processes.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : notnull
{
    /// <summary>
    /// Handles the received event.
    /// </summary>
    /// <param name="eventName">The event name constant.</param>
    /// <param name="payload">The deserialized event payload.</param>
    /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous handling operation.</returns>
    /// <remarks>
    /// ITDD Contract:
    /// - MUST be idempotent (safe to call multiple times with same event)
    /// - MUST propagate correlation ID to downstream operations
    /// - MUST NOT throw (defensive - log errors and return gracefully)
    /// - Liskov: All implementations must handle events independently (no shared state)
    /// </remarks>
    Task HandleAsync(
        string eventName,
        TEvent payload,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

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

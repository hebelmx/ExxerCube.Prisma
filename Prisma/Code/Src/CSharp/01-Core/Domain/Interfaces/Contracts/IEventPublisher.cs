using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExxerCube.Prisma.Domain.Interfaces.Contracts;

/// <summary>
/// Event publisher abstraction for publishing domain events to subscribers.
/// Implementations must be substitutable (Liskov) - any publisher should work identically
/// from the caller's perspective, whether in-memory, message queue, or distributed event bus.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to all registered subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must be serializable).</typeparam>
    /// <param name="eventName">The event name constant (e.g., DocumentEvents.DocumentDownloaded).</param>
    /// <param name="payload">The event payload (will be serialized).</param>
    /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <remarks>
    /// ITDD Contract:
    /// - MUST preserve correlation ID through serialization/deserialization
    /// - MUST NOT throw on publish failure (defensive - log and continue)
    /// - SHOULD emit event within 100ms for in-memory, within SLA for distributed
    /// - Liskov: All implementations must honor fire-and-forget semantics
    /// </remarks>
    Task PublishAsync<TEvent>(
        string eventName,
        TEvent payload,
        Guid correlationId,
        CancellationToken cancellationToken = default)
        where TEvent : notnull;
}

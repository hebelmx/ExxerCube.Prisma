// <copyright file="OutboxEvent.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a domain event that has been persisted to the outbox for reliable at-least-once delivery.
/// The OutboxRetryWorker periodically scans for unprocessed outbox events and re-publishes them
/// via the EventPublisher, providing event-processing reliability (NFR14).
/// </summary>
public class OutboxEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this outbox entry (matches DomainEvent.EventId).
    /// </summary>
    public Guid OutboxEventId { get; set; }

    /// <summary>
    /// Gets or sets the fully-qualified CLR event type name (e.g. "DocumentDownloadedEvent").
    /// Used for logging and observability — not for deserialization in this implementation.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON-serialised payload of the original domain event.
    /// Polymorphic serialisation via DomainEvent JSON attributes preserves the concrete type.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the event was first written to the outbox.
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp when the event was successfully processed (re-published).
    /// Null while the event is pending or being retried.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of re-publish attempts made so far.
    /// Zero on initial insertion (first attempt happens on the first worker tick).
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the error detail from the most recent failed re-publish attempt, if any.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the event was moved to dead-letter status
    /// after exhausting all retry attempts. Null while the event is still eligible for retry.
    /// </summary>
    public DateTime? DeadLetteredAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether this outbox event is eligible for processing:
    /// not yet processed, not dead-lettered, and below the maximum retry ceiling.
    /// The ceiling is enforced by OutboxRetryWorker.MaxRetries.
    /// </summary>
    public bool IsPending => ProcessedAt is null && DeadLetteredAt is null;
}

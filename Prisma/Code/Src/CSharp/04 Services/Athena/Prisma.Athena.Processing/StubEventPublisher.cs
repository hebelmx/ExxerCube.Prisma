using System;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Processing;

/// <summary>
/// Stub implementation of IEventPublisher for testing and development.
/// Logs events without actually publishing them. Replace with actual event bus in production.
/// </summary>
public sealed class StubEventPublisher : IEventPublisher
{
    private readonly ILogger<StubEventPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubEventPublisher"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public StubEventPublisher(ILogger<StubEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent
    {
        _logger.LogInformation("StubEventPublisher: Event published: {EventType}", typeof(TEvent).Name);
    }

    /// <inheritdoc/>
    public IObservable<TEvent> GetEventStream<TEvent>() where TEvent : DomainEvent
    {
        _logger.LogWarning("StubEventPublisher: GetEventStream called for {EventType} - returning empty observable", typeof(TEvent).Name);
        return new EmptyObservable<TEvent>();
    }

    /// <inheritdoc/>
    public IObservable<DomainEvent> GetAllEventsStream()
    {
        _logger.LogWarning("StubEventPublisher: GetAllEventsStream called - returning empty observable");
        return new EmptyObservable<DomainEvent>();
    }

    /// <summary>
    /// Empty observable implementation for stub purposes.
    /// </summary>
    private sealed class EmptyObservable<T> : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnCompleted();
            return new EmptyDisposable();
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}

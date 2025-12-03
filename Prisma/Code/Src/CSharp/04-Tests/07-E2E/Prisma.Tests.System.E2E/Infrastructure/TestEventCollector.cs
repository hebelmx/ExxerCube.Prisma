using Prisma.Shared.Contracts;
using System.Collections.Concurrent;

namespace Prisma.Tests.System.E2E.Infrastructure;

/// <summary>
/// Collects events broadcast via IExxerHub&lt;T&gt; for E2E test validation.
/// </summary>
/// <typeparam name="TEvent">The event type to collect</typeparam>
public sealed class TestEventCollector<TEvent> where TEvent : class
{
    private readonly ConcurrentBag<TEvent> _events = new();
    private readonly ConcurrentBag<Guid> _correlationIds = new();

    /// <summary>
    /// All collected events.
    /// </summary>
    public IReadOnlyList<TEvent> Events => _events.ToList();

    /// <summary>
    /// All correlation IDs seen in collected events.
    /// </summary>
    public IReadOnlyList<Guid> CorrelationIds => _correlationIds.ToList();

    /// <summary>
    /// Number of events collected.
    /// </summary>
    public int Count => _events.Count;

    /// <summary>
    /// Records an event broadcast.
    /// </summary>
    public void RecordEvent(TEvent evt)
    {
        _events.Add(evt);

        // Extract correlation ID if event has one
        if (TryGetCorrelationId(evt, out var correlationId))
        {
            _correlationIds.Add(correlationId);
        }
    }

    /// <summary>
    /// Waits for at least the specified number of events to be collected.
    /// </summary>
    public async Task<bool> WaitForEventsAsync(int expectedCount, TimeSpan timeout)
    {
        var startTime = DateTimeOffset.UtcNow;

        while (_events.Count < expectedCount)
        {
            if (DateTimeOffset.UtcNow - startTime > timeout)
                return false;

            await Task.Delay(100);
        }

        return true;
    }

    /// <summary>
    /// Gets the first event matching the predicate.
    /// </summary>
    public TEvent? GetEvent(Func<TEvent, bool> predicate)
    {
        return _events.FirstOrDefault(predicate);
    }

    /// <summary>
    /// Gets all events matching the predicate.
    /// </summary>
    public IReadOnlyList<TEvent> GetEvents(Func<TEvent, bool> predicate)
    {
        return _events.Where(predicate).ToList();
    }

    /// <summary>
    /// Clears all collected events.
    /// </summary>
    public void Clear()
    {
        _events.Clear();
        _correlationIds.Clear();
    }

    /// <summary>
    /// Tries to extract correlation ID from an event.
    /// </summary>
    private static bool TryGetCorrelationId(TEvent evt, out Guid correlationId)
    {
        correlationId = Guid.Empty;

        // Check for common correlation ID properties using reflection
        var eventType = evt.GetType();
        var correlationProperty = eventType.GetProperty("CorrelationId");

        if (correlationProperty?.PropertyType == typeof(Guid))
        {
            var value = correlationProperty.GetValue(evt);
            if (value is Guid guid)
            {
                correlationId = guid;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Factory for creating mock IExxerHub implementations that collect events.
/// </summary>
public static class MockEventHubFactory
{
    /// <summary>
    /// Creates a mock IExxerHub&lt;T&gt; that records all broadcasts to a collector.
    /// </summary>
    public static IExxerHub<TEvent> CreateCollectorHub<TEvent>(
        TestEventCollector<TEvent> collector) where TEvent : class
    {
        var hub = Substitute.For<IExxerHub<TEvent>>();

        // Mock SendToAllAsync to record events
        hub.SendToAllAsync(Arg.Any<TEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var evt = callInfo.ArgAt<TEvent>(0);
                collector.RecordEvent(evt);
                return Task.FromResult(Result.Success());
            });

        // Mock SendToGroupAsync to record events
        hub.SendToGroupAsync(Arg.Any<string>(), Arg.Any<TEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var evt = callInfo.ArgAt<TEvent>(1);
                collector.RecordEvent(evt);
                return Task.FromResult(Result.Success());
            });

        // Mock SendToClientAsync to record events
        hub.SendToClientAsync(Arg.Any<string>(), Arg.Any<TEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var evt = callInfo.ArgAt<TEvent>(1);
                collector.RecordEvent(evt);
                return Task.FromResult(Result.Success());
            });

        return hub;
    }
}

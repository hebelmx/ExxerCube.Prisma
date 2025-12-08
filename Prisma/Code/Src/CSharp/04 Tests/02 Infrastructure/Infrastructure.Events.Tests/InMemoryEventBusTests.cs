using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Infrastructure.Events.Tests;

/// <summary>
/// ITDD tests proving InMemoryEventBus satisfies IEventPublisher and IEventSubscriber contracts.
/// </summary>
/// <remarks>
/// These tests verify:
/// - Handlers register correctly (Subscribe)
/// - Multiple handlers can subscribe to same event
/// - Publish invokes all registered handlers in order
/// - Defensive: no throw when handler fails
/// - Defensive: no throw when no handlers registered
/// - Correlation IDs preserved through publish/handle
/// - Cancellation handled correctly
/// </remarks>
public sealed class InMemoryEventBusTests
{
    private readonly InMemoryEventBus _eventBus;

    public InMemoryEventBusTests()
    {
        _eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
    }

    [Fact]
    public void Subscribe_RegistersHandler_Successfully()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        const string eventName = "TestEvent";

        // Act
        _eventBus.Subscribe(eventName, handler);

        // Assert - no exception thrown, handler registered
        // (Verification happens when PublishAsync is called in other tests)
    }

    [Fact]
    public async Task PublishAsync_InvokesRegisteredHandler_Successfully()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        const string eventName = "TestEvent";
        var payload = new TestEvent("Test Payload");
        var correlationId = Guid.NewGuid();

        _eventBus.Subscribe(eventName, handler);

        // Act
        await _eventBus.PublishAsync(eventName, payload, correlationId, TestContext.Current.CancellationToken);

        // Assert
        await handler.Received(1).HandleAsync(
            eventName,
            Arg.Is<TestEvent>(e => e.Message == "Test Payload"),
            correlationId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_InvokesAllInOrder()
    {
        // Arrange
        var handler1 = Substitute.For<IEventHandler<TestEvent>>();
        var handler2 = Substitute.For<IEventHandler<TestEvent>>();
        var handler3 = Substitute.For<IEventHandler<TestEvent>>();
        var invocationOrder = new List<int>();

        handler1.HandleAsync(Arg.Any<string>(), Arg.Any<TestEvent>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { invocationOrder.Add(1); return Task.CompletedTask; });
        handler2.HandleAsync(Arg.Any<string>(), Arg.Any<TestEvent>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { invocationOrder.Add(2); return Task.CompletedTask; });
        handler3.HandleAsync(Arg.Any<string>(), Arg.Any<TestEvent>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { invocationOrder.Add(3); return Task.CompletedTask; });

        const string eventName = "TestEvent";
        var payload = new TestEvent("Multi Handler Test");
        var correlationId = Guid.NewGuid();

        _eventBus.Subscribe(eventName, handler1);
        _eventBus.Subscribe(eventName, handler2);
        _eventBus.Subscribe(eventName, handler3);

        // Act
        await _eventBus.PublishAsync(eventName, payload, correlationId, TestContext.Current.CancellationToken);

        // Assert
        invocationOrder.ToArray().ShouldBe(new[] { 1, 2, 3 }, "Handlers should be invoked in registration order");
        await handler1.Received(1).HandleAsync(eventName, payload, correlationId, Arg.Any<CancellationToken>());
        await handler2.Received(1).HandleAsync(eventName, payload, correlationId, Arg.Any<CancellationToken>());
        await handler3.Received(1).HandleAsync(eventName, payload, correlationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_NoHandlersRegistered_DoesNotThrow()
    {
        // Arrange
        const string eventName = "UnsubscribedEvent";
        var payload = new TestEvent("No Handlers");
        var correlationId = Guid.NewGuid();

        // Act & Assert - DEFENSIVE: should not throw
        await _eventBus.PublishAsync(eventName, payload, correlationId, TestContext.Current.CancellationToken);
        // If we got here without exception, test passes
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_DoesNotThrow_ContinuesOtherHandlers()
    {
        // Arrange
        var failingHandler = Substitute.For<IEventHandler<TestEvent>>();
        var successHandler = Substitute.For<IEventHandler<TestEvent>>();

        failingHandler.HandleAsync(Arg.Any<string>(), Arg.Any<TestEvent>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Handler failure"));

        const string eventName = "TestEvent";
        var payload = new TestEvent("Handler Failure Test");
        var correlationId = Guid.NewGuid();

        _eventBus.Subscribe(eventName, failingHandler);
        _eventBus.Subscribe(eventName, successHandler);

        // Act & Assert - DEFENSIVE: should not throw, should continue to successHandler
        await _eventBus.PublishAsync(eventName, payload, correlationId, TestContext.Current.CancellationToken);

        // Verify success handler was still invoked despite first handler failing
        await successHandler.Received(1).HandleAsync(eventName, payload, correlationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_CorrelationId_PreservedThroughHandling()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        const string eventName = "CorrelationTest";
        var payload = new TestEvent("Correlation Test");
        var correlationId = Guid.Parse("12345678-1234-1234-1234-123456789012");

        _eventBus.Subscribe(eventName, handler);

        // Act
        await _eventBus.PublishAsync(eventName, payload, correlationId, TestContext.Current.CancellationToken);

        // Assert - CRITICAL: correlation ID must be preserved exactly
        await handler.Received(1).HandleAsync(
            eventName,
            payload,
            Arg.Is<Guid>(id => id == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_Cancellation_StopsProcessing()
    {
        // Arrange
        var handler1 = Substitute.For<IEventHandler<TestEvent>>();
        var handler2 = Substitute.For<IEventHandler<TestEvent>>();
        var cts = new CancellationTokenSource();

        handler1.HandleAsync(Arg.Any<string>(), Arg.Any<TestEvent>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                cts.Cancel(); // Cancel after first handler
                await Task.CompletedTask;
                throw new OperationCanceledException(cts.Token);
            });

        const string eventName = "CancellationTest";
        var payload = new TestEvent("Cancellation Test");
        var correlationId = Guid.NewGuid();

        _eventBus.Subscribe(eventName, handler1);
        _eventBus.Subscribe(eventName, handler2);

        // Act
        await _eventBus.PublishAsync(eventName, payload, correlationId, cts.Token);

        // Assert - second handler should NOT be invoked
        await handler1.Received(1).HandleAsync(eventName, payload, correlationId, cts.Token);
        await handler2.DidNotReceive().HandleAsync(eventName, payload, correlationId, cts.Token);
    }

    [Fact]
    public void Subscribe_NullEventName_ThrowsArgumentException()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => _eventBus.Subscribe(null!, handler));
        Should.Throw<ArgumentException>(() => _eventBus.Subscribe(string.Empty, handler));
        Should.Throw<ArgumentException>(() => _eventBus.Subscribe("   ", handler));
    }

    [Fact]
    public void Subscribe_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        const string eventName = "TestEvent";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _eventBus.Subscribe<TestEvent>(eventName, null!));
    }

    [Fact]
    public async Task PublishAsync_NullPayload_DoesNotThrow()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        const string eventName = "TestEvent";
        var correlationId = Guid.NewGuid();

        _eventBus.Subscribe(eventName, handler);

        // Act & Assert - DEFENSIVE: should not throw, should just log warning
        await _eventBus.PublishAsync<TestEvent>(eventName, null!, correlationId, TestContext.Current.CancellationToken);

        // Handler should NOT be invoked with null payload
        await handler.DidNotReceive().HandleAsync(
            Arg.Any<string>(),
            Arg.Any<TestEvent>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Simple test event for testing event bus.
    /// </summary>
    public sealed record TestEvent(string Message);
}

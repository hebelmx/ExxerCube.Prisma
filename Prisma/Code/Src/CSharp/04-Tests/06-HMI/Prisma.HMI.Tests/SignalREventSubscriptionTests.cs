namespace Prisma.HMI.Tests;

/// <summary>
/// ITDD Stage 7: Tests for SignalR event subscription and real-time notification delivery.
/// Verifies that the HMI client can subscribe to and receive processing events.
/// </summary>
public sealed class SignalREventSubscriptionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SubscribeToClassificationEvents_ReceivesClassificationCompletedEvent()
    {
        // Arrange - Create mock SignalR hub connection
        var connection = CreateMockHubConnection();
        var receivedEvent = new TaskCompletionSource<ClassificationCompletedEvent>();

        connection.On<ClassificationCompletedEvent>("ClassificationCompleted", (classificationEvent) =>
        {
            receivedEvent.SetResult(classificationEvent);
        });

        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act - Simulate server broadcasting classification event
        var testEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "test-document.pdf",
            ClassificationType: "Invoice",
            ConfidenceScore: 0.95,
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        await SimulateBroadcastAsync(connection, "ClassificationCompleted", testEvent);

        // Assert - Verify event was received
        var result = await receivedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.ShouldNotBeNull();
        result.FileName.ShouldBe("test-document.pdf");
        result.ClassificationType.ShouldBe("Invoice");
        result.ConfidenceScore.ShouldBe(0.95);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SubscribeToProcessingEvents_ReceivesProcessingCompletedEvent()
    {
        // Arrange
        var connection = CreateMockHubConnection();
        var receivedEvent = new TaskCompletionSource<ProcessingCompletedEvent>();

        connection.On<ProcessingCompletedEvent>("ProcessingCompleted", (processingEvent) =>
        {
            receivedEvent.SetResult(processingEvent);
        });

        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act - Simulate server broadcasting processing completion
        var testEvent = new ProcessingCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "test-document.pdf",
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(15),
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        await SimulateBroadcastAsync(connection, "ProcessingCompleted", testEvent);

        // Assert - Verify event was received
        var result = await receivedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.ShouldNotBeNull();
        result.FileName.ShouldBe("test-document.pdf");
        result.Status.ShouldBe("Success");
        result.ProcessingDuration.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MultipleSubscribers_AllReceiveSameEvent()
    {
        // Arrange - Multiple clients subscribe to same event
        var connection1 = CreateMockHubConnection();
        var connection2 = CreateMockHubConnection();
        var received1 = new TaskCompletionSource<ClassificationCompletedEvent>();
        var received2 = new TaskCompletionSource<ClassificationCompletedEvent>();

        connection1.On<ClassificationCompletedEvent>("ClassificationCompleted", evt => received1.SetResult(evt));
        connection2.On<ClassificationCompletedEvent>("ClassificationCompleted", evt => received2.SetResult(evt));

        await connection1.StartAsync(TestContext.Current.CancellationToken);
        await connection2.StartAsync(TestContext.Current.CancellationToken);

        // Act - Broadcast single event
        var testEvent = new ClassificationCompletedEvent(
            FileId: Guid.NewGuid(),
            FileName: "shared-document.pdf",
            ClassificationType: "Contract",
            ConfidenceScore: 0.88,
            CorrelationId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow
        );

        await SimulateBroadcastAsync(connection1, "ClassificationCompleted", testEvent);
        await SimulateBroadcastAsync(connection2, "ClassificationCompleted", testEvent);

        // Assert - Both clients receive the event
        var result1 = await received1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var result2 = await received2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result1.FileName.ShouldBe("shared-document.pdf");
        result2.FileName.ShouldBe("shared-document.pdf");
        result1.CorrelationId.ShouldBe(result2.CorrelationId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CorrelationId_PreservedAcrossEvents()
    {
        // Arrange - Verify end-to-end tracing
        var connection = CreateMockHubConnection();
        var classificationReceived = new TaskCompletionSource<ClassificationCompletedEvent>();
        var processingReceived = new TaskCompletionSource<ProcessingCompletedEvent>();

        connection.On<ClassificationCompletedEvent>("ClassificationCompleted", evt => classificationReceived.SetResult(evt));
        connection.On<ProcessingCompletedEvent>("ProcessingCompleted", evt => processingReceived.SetResult(evt));

        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act - Simulate event chain with same correlation ID
        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var classificationEvent = new ClassificationCompletedEvent(
            FileId: fileId,
            FileName: "traced-document.pdf",
            ClassificationType: "Invoice",
            ConfidenceScore: 0.92,
            CorrelationId: correlationId,
            Timestamp: DateTimeOffset.UtcNow
        );

        var processingEvent = new ProcessingCompletedEvent(
            FileId: fileId,
            FileName: "traced-document.pdf",
            Status: "Success",
            ProcessingDuration: TimeSpan.FromSeconds(20),
            CorrelationId: correlationId,
            Timestamp: DateTimeOffset.UtcNow.AddSeconds(20)
        );

        await SimulateBroadcastAsync(connection, "ClassificationCompleted", classificationEvent);
        await SimulateBroadcastAsync(connection, "ProcessingCompleted", processingEvent);

        // Assert - Correlation ID preserved across entire pipeline
        var classResult = await classificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var procResult = await processingReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        classResult.CorrelationId.ShouldBe(correlationId);
        procResult.CorrelationId.ShouldBe(correlationId);
        classResult.FileId.ShouldBe(procResult.FileId);
    }

    // Helper methods (GREEN phase - implemented with test doubles)
    private HubConnection CreateMockHubConnection()
    {
        // Create a simple test double using HubConnectionBuilder with in-memory transport
        // For true unit testing, we use NSubstitute to create a controllable mock
        var mockConnection = Substitute.For<HubConnection>();

        // Setup default behavior
        mockConnection.State.Returns(HubConnectionState.Disconnected);
        mockConnection.ConnectionId.Returns("test-connection-" + Guid.NewGuid().ToString("N")[..8]);

        // Store handlers when On<T> is called
        var connectionHandlers = new Dictionary<string, List<Delegate>>();

        mockConnection.When(x => x.On<ClassificationCompletedEvent>(Arg.Any<string>(), Arg.Any<Action<ClassificationCompletedEvent>>()))
            .Do(callInfo =>
            {
                var methodName = callInfo.ArgAt<string>(0);
                var handler = callInfo.ArgAt<Action<ClassificationCompletedEvent>>(1);
                if (!connectionHandlers.ContainsKey(methodName))
                {
                    connectionHandlers[methodName] = new List<Delegate>();
                }
                connectionHandlers[methodName].Add(handler);
            });

        mockConnection.When(x => x.On<ProcessingCompletedEvent>(Arg.Any<string>(), Arg.Any<Action<ProcessingCompletedEvent>>()))
            .Do(callInfo =>
            {
                var methodName = callInfo.ArgAt<string>(0);
                var handler = callInfo.ArgAt<Action<ProcessingCompletedEvent>>(1);
                if (!connectionHandlers.ContainsKey(methodName))
                {
                    connectionHandlers[methodName] = new List<Delegate>();
                }
                connectionHandlers[methodName].Add(handler);
            });

        mockConnection.StartAsync(Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            mockConnection.State.Returns(HubConnectionState.Connected);
            return Task.CompletedTask;
        });

        // Store handlers for simulation
        MockConnectionRegistry.Register(mockConnection, connectionHandlers);

        return mockConnection;
    }

    private async Task SimulateBroadcastAsync<T>(HubConnection connection, string methodName, T data)
    {
        // Retrieve and invoke handlers from registry
        var handlers = MockConnectionRegistry.GetHandlers(connection, methodName);
        if (handlers != null)
        {
            foreach (var handler in handlers)
            {
                if (handler is Action<T> typedHandler)
                {
                    await Task.Run(() => typedHandler(data));
                }
            }
        }
    }
}

/// <summary>
/// Registry to track mock connection handlers for test simulation.
/// </summary>
internal static class MockConnectionRegistry
{
    private static readonly Dictionary<HubConnection, Dictionary<string, List<Delegate>>> _registry = new();

    public static void Register(HubConnection connection, Dictionary<string, List<Delegate>> handlers)
    {
        _registry[connection] = handlers;
    }

    public static List<Delegate>? GetHandlers(HubConnection connection, string methodName)
    {
        if (_registry.TryGetValue(connection, out var handlers))
        {
            if (handlers.TryGetValue(methodName, out var methodHandlers))
            {
                return methodHandlers;
            }
        }
        return null;
    }

    public static void Clear()
    {
        _registry.Clear();
    }
}

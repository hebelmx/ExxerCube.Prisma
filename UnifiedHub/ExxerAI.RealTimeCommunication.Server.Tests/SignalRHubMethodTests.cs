namespace ExxerAI.RealTimeCommunication.Server.Tests;

/// <summary>
/// Integration tests for SignalR hub methods using real server connections.
/// Tests all hub communication patterns: broadcast, group messaging, user messaging,
/// event broadcasting, notifications, and alerts.
///
/// This replaces the mock-based tests in SignalRAdapterTests.cs that failed due to
/// extension method limitations in NSubstitute.
/// </summary>
public sealed class SignalRHubMethodTests : IClassFixture<SignalRServerFactory>, IAsyncDisposable
{
    private readonly SignalRServerFactory _factory;
    private readonly List<HubConnection> _connections = new();

    /// <summary>
    /// Constructor: Initializes test with SignalR server factory.
    /// </summary>
    public SignalRHubMethodTests(SignalRServerFactory factory)
    {
        _factory = factory;
    }

    //  Broadcast Message Tests

    /// <summary>
    /// Business Logic Test: Should broadcast message to all connected clients on SystemHub.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SystemHub_Should_Broadcast_Message_To_All_Clients()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        var messageReceived = new TaskCompletionSource<object?>();
        connection.On<object>("TestMessage", data => messageReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "TestMessage",
            new { Message = "Hello World" }, CancellationToken.None);

        // Wait for message with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await messageReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Business Logic Test: Should handle broadcast on all communication targets (hubs).
    /// </summary>
    [Theory(Timeout = 30_000)]
    [InlineData("/hubs/system")]
    [InlineData("/hubs/agent")]
    [InlineData("/hubs/task")]
    [InlineData("/hubs/document")]
    [InlineData("/hubs/economic")]
    public async Task Should_Broadcast_Message_On_All_Hub_Types(string hubPath)
    {
        // Arrange
        var connection = _factory.CreateHubConnection(hubPath);
        _connections.Add(connection);

        var messageReceived = new TaskCompletionSource<object?>();
        connection.On<object>("TestMessage", data => messageReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "TestMessage",
            new { Message = "Test" }, CancellationToken.None);

        // Wait for message with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await messageReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Cancellation Test: Should handle cancellation during broadcast operations.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task BroadcastMessage_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        await connection.StartAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await connection.InvokeAsync("BroadcastMessage", "TestMessage",
                new { Message = "Test" }, cts.Token);
        });

        // Cleanup
        await connection.StopAsync();
    }

    //  Broadcast Message Tests

    //  Group Message Tests

    /// <summary>
    /// Business Logic Test: Should send message to specific group on AgentHub.
    /// NOTE: This test validates the SendToGroup hub method works correctly.
    /// Group membership must be managed server-side via SignalRAdapter.AddToGroupAsync.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task AgentHub_Should_Send_Message_To_Group()
    {
        // Arrange
        var connection1 = _factory.CreateHubConnection("/hubs/agent");
        var connection2 = _factory.CreateHubConnection("/hubs/agent");
        _connections.Add(connection1);
        _connections.Add(connection2);

        var messageReceived = new TaskCompletionSource<object?>();
        connection1.On<object>("GroupMessage", data => messageReceived.TrySetResult(data));

        await connection1.StartAsync(CancellationToken.None);
        await connection2.StartAsync(CancellationToken.None);

        // NOTE: In production, groups are managed via SignalRAdapter.AddToGroupAsync
        // For this test, we verify the SendToGroup hub method works (even if no one is in the group yet)

        // Act - Send to group (hub method should execute without error)
        await connection2.InvokeAsync("SendToGroup", "test-group", "GroupMessage",
            new { Message = "Hello Group" }, CancellationToken.None);

        // Assert - Message sent successfully (no exception thrown)
        // Note: We can't verify message receipt without server-side group management,
        // but we can verify the hub method executes correctly

        // Cleanup
        await connection1.StopAsync();
        await connection2.StopAsync();
    }

    //  Group Message Tests

    //  User Message Tests

    /// <summary>
    /// Business Logic Test: Should send message to specific user on TaskHub.
    /// NOTE: SendToUser requires SignalR user ID mapping (configured via IUserIdProvider).
    /// This test validates the hub method executes without error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task TaskHub_Should_Send_Message_To_User()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/task");
        _connections.Add(connection);

        await connection.StartAsync(CancellationToken.None);

        // Act - Send to user (hub method should execute without error)
        // NOTE: In production, user IDs are mapped via IUserIdProvider configuration
        // For this test, we verify the SendToUser hub method works (even without user ID mapping)
        await connection.InvokeAsync("SendToUser", "test-user", "UserMessage",
            new { Message = "Hello User" }, CancellationToken.None);

        // Assert - Message sent successfully (no exception thrown)
        // Note: We can't verify message receipt without IUserIdProvider configuration,
        // but we can verify the hub method executes correctly

        // Cleanup
        await connection.StopAsync();
    }

    //  User Message Tests

    //  Event Broadcasting Tests

    /// <summary>
    /// Business Logic Test: Should broadcast system event to all clients.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SystemHub_Should_Broadcast_System_Event()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        var eventReceived = new TaskCompletionSource<object?>();
        connection.On<object>("SystemEvent", data => eventReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "SystemEvent",
            new
            {
                EventType = "SystemStarted",
                Timestamp = DateTime.UtcNow,
                Data = new { Status = "Online" }
            }, CancellationToken.None);

        // Wait for event with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await eventReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Business Logic Test: Should broadcast agent event to all clients.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task AgentHub_Should_Broadcast_Agent_Event()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/agent");
        _connections.Add(connection);

        var eventReceived = new TaskCompletionSource<object?>();
        connection.On<object>("AgentEvent", data => eventReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "AgentEvent",
            new
            {
                EventType = "AgentStarted",
                Timestamp = DateTime.UtcNow,
                Data = new { Status = "Active" },
                AgentId = "agent123"
            }, CancellationToken.None);

        // Wait for event with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await eventReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Business Logic Test: Should broadcast task event to all clients.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task TaskHub_Should_Broadcast_Task_Event()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/task");
        _connections.Add(connection);

        var eventReceived = new TaskCompletionSource<object?>();
        connection.On<object>("TaskEvent", data => eventReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "TaskEvent",
            new
            {
                EventType = "TaskStarted",
                Timestamp = DateTime.UtcNow,
                Data = new { Status = "Running" },
                TaskId = "task123"
            }, CancellationToken.None);

        // Wait for event with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await eventReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    //  Event Broadcasting Tests

    //  Notification Tests

    /// <summary>
    /// Business Logic Test: Should send notification to all clients.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SystemHub_Should_Send_Notification()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        var notificationReceived = new TaskCompletionSource<object?>();
        connection.On<object>("Notification", data => notificationReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "Notification",
            new
            {
                Type = "InfoNotification",
                Title = "Test Notification",
                Message = "This is a test notification"
            }, CancellationToken.None);

        // Wait for notification with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await notificationReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Business Logic Test: Should send alert with severity to all clients.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SystemHub_Should_Send_Alert()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        var alertReceived = new TaskCompletionSource<object?>();
        connection.On<object>("Alert", data => alertReceived.TrySetResult(data));

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "Alert",
            new
            {
                Type = "SystemAlert",
                Severity = "Warning",
                Message = "System warning message"
            }, CancellationToken.None);

        // Wait for alert with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await alertReceived.Task.WaitAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    //  Notification Tests

    //  Performance and Thread Safety Tests

    /// <summary>
    /// Performance Test: Should handle concurrent broadcast operations efficiently.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Concurrent_Broadcasts_Should_Be_Thread_Safe()
    {
        // Arrange
        const int operationCount = 10;
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        var receivedMessages = new List<object>();
        var allReceived = new TaskCompletionSource<bool>();

        connection.On<object>("ConcurrentTest", data =>
        {
            lock (receivedMessages)
            {
                receivedMessages.Add(data);
                if (receivedMessages.Count >= operationCount)
                {
                    allReceived.TrySetResult(true);
                }
            }
        });

        await connection.StartAsync(CancellationToken.None);

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < operationCount; i++)
        {
            var index = i; // Capture for closure
            var task = connection.InvokeAsync("BroadcastMessage", "ConcurrentTest",
                new { Index = index, Message = $"Message{index}" },
                CancellationToken.None);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Wait for all messages to be received
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        await allReceived.Task.WaitAsync(cts.Token);

        // Assert
        receivedMessages.Count.ShouldBe(operationCount);

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Performance Test: Multiple clients should receive broadcast messages simultaneously.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Multiple_Clients_Should_Receive_Broadcast_Simultaneously()
    {
        // Arrange
        const int clientCount = 5;
        var connections = new List<HubConnection>();
        var receivedCounts = new List<TaskCompletionSource<bool>>();

        for (int i = 0; i < clientCount; i++)
        {
            var connection = _factory.CreateHubConnection("/hubs/system");
            var tcs = new TaskCompletionSource<bool>();

            connection.On<object>("MultiClientTest", data => tcs.TrySetResult(true));

            await connection.StartAsync(CancellationToken.None);

            connections.Add(connection);
            receivedCounts.Add(tcs);
            _connections.Add(connection);
        }

        // Act
        await connections[0].InvokeAsync("BroadcastMessage", "MultiClientTest",
            new { Message = "Broadcast to all" }, CancellationToken.None);

        // Wait for all clients to receive
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        await Task.WhenAll(receivedCounts.Select(tcs => tcs.Task.WaitAsync(cts.Token)));

        // Assert
        receivedCounts.Count.ShouldBe(clientCount);
        receivedCounts.ShouldAllBe(tcs => tcs.Task.IsCompletedSuccessfully);

        // Cleanup
        foreach (var connection in connections)
        {
            await connection.StopAsync();
        }
    }

    //  Performance and Thread Safety Tests

    /// <summary>
    /// Cleanup: Disposes all hub connections.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                await connection.StopAsync();
            }
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }
}

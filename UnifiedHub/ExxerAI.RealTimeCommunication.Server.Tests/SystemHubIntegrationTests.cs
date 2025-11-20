namespace ExxerAI.RealTimeCommunication.Server.Tests;

/// <summary>
/// Integration tests for SystemHub using real SignalR server.
/// These tests verify end-to-end hub communication with actual connections.
/// </summary>
public sealed class SystemHubIntegrationTests : IClassFixture<SignalRServerFactory>, IAsyncDisposable
{
    private readonly SignalRServerFactory _factory;
    private readonly List<HubConnection> _connections = new();

    /// <summary>
    /// Constructor: Initializes test with SignalR server factory.
    /// </summary>
    public SystemHubIntegrationTests(SignalRServerFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Test: Should connect to SystemHub successfully.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Should_Connect_To_SystemHub_Successfully()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        // Act
        await connection.StartAsync(CancellationToken.None);

        // Assert
        connection.State.ShouldBe(HubConnectionState.Connected);

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Test: Should receive broadcast messages from SystemHub.
    /// </summary>
    [Fact(Timeout = 30_000, Skip = "Skipped: BroadcastMessage serialization issue with anonymous objects")]
    public async Task Should_Receive_Broadcast_Messages_From_SystemHub()
    {
        // Arrange
        var connection = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection);

        var messageReceived = new TaskCompletionSource<object?>();
        string? receivedMessageType = null;
        object? receivedData = null;

        connection.On<object>("TestMessage", data =>
        {
            receivedData = data;
            receivedMessageType = "TestMessage";
            messageReceived.TrySetResult(data);
        });

        await connection.StartAsync(CancellationToken.None);

        // Act
        await connection.InvokeAsync("BroadcastMessage", "TestMessage", new { Content = "Hello World" }, CancellationToken.None);

        // Wait for message with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        await messageReceived.Task.WaitAsync(cts.Token);

        // Assert
        receivedMessageType.ShouldBe("TestMessage");
        receivedData.ShouldNotBeNull();

        // Cleanup
        await connection.StopAsync();
    }

    /// <summary>
    /// Test: Multiple clients should receive broadcast messages.
    /// </summary>
    [Fact(Timeout = 30_000, Skip = "Skipped: BroadcastMessage serialization issue with anonymous objects")]
    public async Task Multiple_Clients_Should_Receive_Broadcast_Messages()
    {
        // Arrange
        var connection1 = _factory.CreateHubConnection("/hubs/system");
        var connection2 = _factory.CreateHubConnection("/hubs/system");
        _connections.Add(connection1);
        _connections.Add(connection2);

        var client1Received = new TaskCompletionSource<object?>();
        var client2Received = new TaskCompletionSource<object?>();

        connection1.On<object>("MulticastTest", data => client1Received.TrySetResult(data));
        connection2.On<object>("MulticastTest", data => client2Received.TrySetResult(data));

        await connection1.StartAsync(CancellationToken.None);
        await connection2.StartAsync(CancellationToken.None);

        // Act
        await connection1.InvokeAsync("BroadcastMessage", "MulticastTest", new { Message = "Broadcast to all" }, CancellationToken.None);

        // Wait for both clients to receive
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Assert - await tasks to avoid blocking

        // Assert
        var client1Message = await client1Received.Task.WaitAsync(cts.Token);
        var client2Message = await client2Received.Task.WaitAsync(cts.Token);
        client1Message.ShouldNotBeNull();
        client2Message.ShouldNotBeNull();

        // Cleanup
        await connection1.StopAsync();
        await connection2.StopAsync();
    }

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

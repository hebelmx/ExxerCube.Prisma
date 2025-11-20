using ExxerAI.RealTimeCommunication.Client.Dashboards;
using ExxerAI.RealTimeCommunication.Client.Health;
using ExxerAI.RealTimeCommunication.Server.Hubs;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Dashboards;
using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;
using ExxerCube.Prisma.SignalR.Abstractions.Infrastructure.Connection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HealthStatusData = ExxerAI.RealTimeCommunication.Client.Health.HealthStatusData;

namespace ExxerAI.RealTimeCommunication.Server.Tests.E2E;

/// <summary>
/// End-to-End integration tests that test the complete flow:
/// Server → Hub → Client Dashboard → Health Monitoring
/// These tests verify the entire system works together as a real-world example.
/// </summary>
public sealed class EndToEndTests : IClassFixture<SignalRServerFactory>, IAsyncDisposable
{
    private readonly SignalRServerFactory _serverFactory;
    private readonly List<IDashboard<SystemMessage>> _systemDashboards = new();
    private readonly List<IDashboard<HealthUpdate>> _healthDashboards = new();
    private readonly List<IServiceHealth<HealthStatusData>> _healthMonitors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EndToEndTests"/> class.
    /// </summary>
    /// <param name="serverFactory">The SignalR server factory.</param>
    public EndToEndTests(SignalRServerFactory serverFactory)
    {
        _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
    }

    /// <summary>
    /// E2E Test: Complete flow from server hub to client dashboard.
    /// Tests: Server sends message → Hub broadcasts → Client dashboard receives.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task E2E_ServerHubToClientDashboard_CompleteFlow()
    {
        // Arrange - Server side (simulated via hub connection)
        var serverHubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        await serverHubConnection.StartAsync(CancellationToken.None);

        // Arrange - Client side
        var clientHubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        var logger = new LoggerFactory().CreateLogger<SystemDashboard>();
        var dashboard = new SystemDashboard(clientHubConnection, null, logger);
        _systemDashboards.Add(dashboard);

        var messageReceived = new TaskCompletionSource<SystemMessage>();
        dashboard.DataReceived += (sender, args) => messageReceived.TrySetResult(args.Data);

        await dashboard.ConnectAsync(CancellationToken.None);

        // Act - Server sends message via hub
        var testMessage = new SystemMessage
        {
            Content = "E2E Test Message",
            Timestamp = DateTime.UtcNow,
            MessageType = "E2ETest"
        };

        // Simulate server sending via hub (in real scenario, this would be via IExxerHub<T>)
        await serverHubConnection.InvokeAsync("SendMessage", testMessage, CancellationToken.None);

        // Wait for message
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var receivedMessage = await messageReceived.Task.WaitAsync(cts.Token);

        // Assert
        receivedMessage.ShouldNotBeNull();
        receivedMessage.Content.ShouldBe(testMessage.Content);
        dashboard.Data.Count.ShouldBeGreaterThan(0);
        dashboard.ConnectionState.ShouldBe(ConnectionState.Connected);

        // Cleanup
        await dashboard.DisconnectAsync(CancellationToken.None);
        await serverHubConnection.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// E2E Test: Health monitoring flow from server to client.
    /// Tests: Server collects diagnostics → Hub broadcasts → Client dashboard receives → Health monitor tracks.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task E2E_HealthMonitoring_ServerToClient_CompleteFlow()
    {
        // Arrange - Server side
        var serverHubConnection = _serverFactory.CreateHubConnection("/hubs/health");
        await serverHubConnection.StartAsync(CancellationToken.None);

        // Arrange - Client side
        var clientHubConnection = _serverFactory.CreateHubConnection("/hubs/health");
        var dashboardLogger = new LoggerFactory().CreateLogger<HealthDashboard>();
        var healthLogger = new LoggerFactory().CreateLogger<ClientServiceHealth>();

        var dashboard = new HealthDashboard(clientHubConnection, null, dashboardLogger);
        var healthMonitor = new ClientServiceHealth(healthLogger);

        _healthDashboards.Add(dashboard);
        _healthMonitors.Add(healthMonitor);

        var updateReceived = new TaskCompletionSource<HealthUpdate>();
        var healthChanged = new TaskCompletionSource<HealthStatusChangedEventArgs<HealthStatusData>>();

        dashboard.DataReceived += (sender, args) => updateReceived.TrySetResult(args.Data);
        healthMonitor.HealthStatusChanged += (sender, args) => healthChanged.TrySetResult(args);

        await dashboard.ConnectAsync(CancellationToken.None);

        // Act - Server sends health update
        var healthUpdate = new HealthUpdate
        {
            ServiceName = "Server",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                { "CpuUsagePercent", 25.5 },
                { "MemoryUsageMB", 512 },
                { "ProcessCount", 150 }
            }
        };

        await serverHubConnection.InvokeAsync("SendHealthUpdate", healthUpdate, CancellationToken.None);

        // Wait for update
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var receivedUpdate = await updateReceived.Task.WaitAsync(cts.Token);

        // Update client health based on received update
        var clientHealthData = new HealthStatusData
        {
            ClientId = Environment.MachineName,
            ConnectionCount = 1,
            LastActivity = DateTime.UtcNow,
            CpuUsagePercent = 15.0,
            MemoryUsageMB = 256,
            TotalMemoryMB = 1024,
            MemoryUsagePercent = 25.0,
            ProcessCount = 100,
            ThreadCount = 50
        };

        await healthMonitor.UpdateHealthAsync(HealthStatus.Healthy, clientHealthData, CancellationToken.None);

        // Assert
        receivedUpdate.ShouldNotBeNull();
        receivedUpdate.ServiceName.ShouldBe("Server");
        receivedUpdate.Status.ShouldBe("Healthy");
        dashboard.Data.Count.ShouldBeGreaterThan(0);

        healthMonitor.Status.ShouldBe(HealthStatus.Healthy);
        healthMonitor.Data.ShouldNotBeNull();
        healthMonitor.Data!.ClientId.ShouldBe(Environment.MachineName);

        // Cleanup
        await dashboard.DisconnectAsync(CancellationToken.None);
        await serverHubConnection.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// E2E Test: Multiple clients receiving broadcast messages.
    /// Tests: Server broadcasts → Multiple client dashboards receive simultaneously.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task E2E_MultipleClients_ReceiveBroadcastMessages()
    {
        // Arrange - Server
        var serverHubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        await serverHubConnection.StartAsync(CancellationToken.None);

        // Arrange - Multiple clients
        var client1Connection = _serverFactory.CreateHubConnection("/hubs/system");
        var client2Connection = _serverFactory.CreateHubConnection("/hubs/system");
        var client3Connection = _serverFactory.CreateHubConnection("/hubs/system");

        var logger1 = new LoggerFactory().CreateLogger<SystemDashboard>();
        var logger2 = new LoggerFactory().CreateLogger<SystemDashboard>();
        var logger3 = new LoggerFactory().CreateLogger<SystemDashboard>();

        var dashboard1 = new SystemDashboard(client1Connection, null, logger1);
        var dashboard2 = new SystemDashboard(client2Connection, null, logger2);
        var dashboard3 = new SystemDashboard(client3Connection, null, logger3);

        _systemDashboards.AddRange(new[] { dashboard1, dashboard2, dashboard3 });

        var client1Received = new TaskCompletionSource<SystemMessage>();
        var client2Received = new TaskCompletionSource<SystemMessage>();
        var client3Received = new TaskCompletionSource<SystemMessage>();

        dashboard1.DataReceived += (sender, args) => client1Received.TrySetResult(args.Data);
        dashboard2.DataReceived += (sender, args) => client2Received.TrySetResult(args.Data);
        dashboard3.DataReceived += (sender, args) => client3Received.TrySetResult(args.Data);

        await dashboard1.ConnectAsync(CancellationToken.None);
        await dashboard2.ConnectAsync(CancellationToken.None);
        await dashboard3.ConnectAsync(CancellationToken.None);

        // Act - Server broadcasts message
        var broadcastMessage = new SystemMessage
        {
            Content = "Broadcast to all clients",
            Timestamp = DateTime.UtcNow,
            MessageType = "Broadcast"
        };

        await serverHubConnection.InvokeAsync("SendMessage", broadcastMessage, CancellationToken.None);

        // Wait for all clients to receive
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        await Task.WhenAll(
            client1Received.Task.WaitAsync(cts.Token),
            client2Received.Task.WaitAsync(cts.Token),
            client3Received.Task.WaitAsync(cts.Token)
        );

        // Assert
        var client1Message = await client1Received.Task;
        var client2Message = await client2Received.Task;
        var client3Message = await client3Received.Task;

        client1Message.ShouldNotBeNull();
        client2Message.ShouldNotBeNull();
        client3Message.ShouldNotBeNull();

        client1Message.Content.ShouldBe(broadcastMessage.Content);
        client2Message.Content.ShouldBe(broadcastMessage.Content);
        client3Message.Content.ShouldBe(broadcastMessage.Content);

        dashboard1.Data.Count.ShouldBeGreaterThan(0);
        dashboard2.Data.Count.ShouldBeGreaterThan(0);
        dashboard3.Data.Count.ShouldBeGreaterThan(0);

        // Cleanup
        await dashboard1.DisconnectAsync(CancellationToken.None);
        await dashboard2.DisconnectAsync(CancellationToken.None);
        await dashboard3.DisconnectAsync(CancellationToken.None);
        await serverHubConnection.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// E2E Test: Connection state management across server and client.
    /// Tests: Connection → Disconnection → Reconnection flow.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task E2E_ConnectionStateManagement_CompleteFlow()
    {
        // Arrange
        var hubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        var logger = new LoggerFactory().CreateLogger<SystemDashboard>();
        var dashboard = new SystemDashboard(hubConnection, null, logger);
        _systemDashboards.Add(dashboard);

        var stateChanges = new List<ConnectionStateChangedEventArgs>();
        dashboard.ConnectionStateChanged += (sender, args) => stateChanges.Add(args);

        // Act - Connect
        var connectResult = await dashboard.ConnectAsync(CancellationToken.None);
        await Task.Delay(100, CancellationToken.None); // Allow state change

        // Assert - Connected
        connectResult.IsSuccess.ShouldBeTrue();
        dashboard.ConnectionState.ShouldBe(ConnectionState.Connected);

        // Act - Disconnect
        var disconnectResult = await dashboard.DisconnectAsync(CancellationToken.None);
        await Task.Delay(100, CancellationToken.None); // Allow state change

        // Assert - Disconnected
        disconnectResult.IsSuccess.ShouldBeTrue();
        dashboard.ConnectionState.ShouldBe(ConnectionState.Disconnected);

        // Act - Reconnect
        var reconnectResult = await dashboard.ConnectAsync(CancellationToken.None);
        await Task.Delay(100, CancellationToken.None); // Allow state change

        // Assert - Reconnected
        reconnectResult.IsSuccess.ShouldBeTrue();
        dashboard.ConnectionState.ShouldBe(ConnectionState.Connected);

        // Verify state changes were tracked
        stateChanges.Count.ShouldBeGreaterThanOrEqualTo(2);
        stateChanges.ShouldContain(s => s.NewState == ConnectionState.Connected);
        stateChanges.ShouldContain(s => s.NewState == ConnectionState.Disconnected);

        // Cleanup
        await dashboard.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// E2E Test: Health status changes trigger events correctly.
    /// Tests: Health monitor → Status change → Event fired → Dashboard updated.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task E2E_HealthStatusChanges_TriggerEvents()
    {
        // Arrange
        var logger = new LoggerFactory().CreateLogger<ClientServiceHealth>();
        var healthMonitor = new ClientServiceHealth(logger);
        _healthMonitors.Add(healthMonitor);

        var statusChanges = new List<HealthStatusChangedEventArgs<HealthStatusData>>();
        healthMonitor.HealthStatusChanged += (sender, args) => statusChanges.Add(args);

        var healthData = new HealthStatusData
        {
            ClientId = "E2ETestClient",
            ConnectionCount = 1,
            LastActivity = DateTime.UtcNow,
            CpuUsagePercent = 10.0,
            MemoryUsageMB = 100,
            TotalMemoryMB = 1024,
            MemoryUsagePercent = 10.0,
            ProcessCount = 50,
            ThreadCount = 25
        };

        // Act - Update to Healthy
        await healthMonitor.UpdateHealthAsync(HealthStatus.Healthy, healthData, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Act - Update to Degraded
        var degradedData = healthData with { CpuUsagePercent = 92.0, MemoryUsagePercent = 85.0 };
        await healthMonitor.UpdateHealthAsync(HealthStatus.Degraded, degradedData, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Act - Update to Unhealthy
        var unhealthyData = healthData with { CpuUsagePercent = 98.0, MemoryUsagePercent = 96.0 };
        await healthMonitor.UpdateHealthAsync(HealthStatus.Unhealthy, unhealthyData, CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);

        // Assert
        healthMonitor.Status.ShouldBe(HealthStatus.Unhealthy);
        statusChanges.Count.ShouldBeGreaterThanOrEqualTo(2); // Healthy→Degraded, Degraded→Unhealthy
        statusChanges.ShouldContain(s => s.NewStatus == HealthStatus.Degraded);
        statusChanges.ShouldContain(s => s.NewStatus == HealthStatus.Unhealthy);
    }

    /// <summary>
    /// Cleanup: Disposes all dashboards and health monitors.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var dashboard in _systemDashboards)
        {
            await dashboard.DisconnectAsync(CancellationToken.None);
            if (dashboard is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }

        foreach (var dashboard in _healthDashboards)
        {
            await dashboard.DisconnectAsync(CancellationToken.None);
            if (dashboard is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }

        _systemDashboards.Clear();
        _healthDashboards.Clear();
        _healthMonitors.Clear();
    }
}


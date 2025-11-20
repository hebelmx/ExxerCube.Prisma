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

namespace ExxerAI.RealTimeCommunication.Server.Tests.Integration;

/// <summary>
/// Integration tests for Dashboard&lt;T&gt; and ServiceHealth&lt;T&gt; using WebApplicationFactory.
/// Tests real SignalR communication between server and client.
/// </summary>
public sealed class DashboardIntegrationTests : IClassFixture<SignalRServerFactory>, IAsyncDisposable
{
    private readonly SignalRServerFactory _serverFactory;
    private readonly List<IDashboard<SystemMessage>> _systemDashboards = new();
    private readonly List<IDashboard<HealthUpdate>> _healthDashboards = new();
    private readonly List<IServiceHealth<HealthStatusData>> _healthMonitors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardIntegrationTests"/> class.
    /// </summary>
    /// <param name="serverFactory">The SignalR server factory.</param>
    public DashboardIntegrationTests(SignalRServerFactory serverFactory)
    {
        _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
    }

    /// <summary>
    /// Test: Dashboard should connect to SystemHub successfully.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Dashboard_Should_Connect_To_SystemHub_Successfully()
    {
        // Arrange
        var hubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        var logger = new LoggerFactory().CreateLogger<SystemDashboard>();
        var dashboard = new SystemDashboard(hubConnection, null, logger);
        _systemDashboards.Add(dashboard);

        // Act
        var result = await dashboard.ConnectAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        dashboard.ConnectionState.ShouldBe(ConnectionState.Connected);

        // Cleanup
        await dashboard.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// Test: Dashboard should receive messages from SystemHub.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Dashboard_Should_Receive_Messages_From_SystemHub()
    {
        // Arrange
        var hubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        var logger = new LoggerFactory().CreateLogger<SystemDashboard>();
        var dashboard = new SystemDashboard(hubConnection, null, logger);
        _systemDashboards.Add(dashboard);

        var messageReceived = new TaskCompletionSource<SystemMessage>();
        dashboard.DataReceived += (sender, args) => messageReceived.TrySetResult(args.Data);

        await dashboard.ConnectAsync(CancellationToken.None);

        // Act - Send message via hub connection
        var testMessage = new SystemMessage
        {
            Content = "Test message from integration test",
            Timestamp = DateTime.UtcNow,
            MessageType = "Test"
        };

        await hubConnection.InvokeAsync("SendMessage", testMessage, CancellationToken.None);

        // Wait for message with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var receivedMessage = await messageReceived.Task.WaitAsync(cts.Token);

        // Assert
        receivedMessage.ShouldNotBeNull();
        receivedMessage.Content.ShouldBe(testMessage.Content);
        dashboard.Data.Count.ShouldBeGreaterThan(0);

        // Cleanup
        await dashboard.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// Test: Health dashboard should connect and receive health updates.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task HealthDashboard_Should_Connect_And_Receive_Updates()
    {
        // Arrange
        var hubConnection = _serverFactory.CreateHubConnection("/hubs/health");
        var logger = new LoggerFactory().CreateLogger<HealthDashboard>();
        var dashboard = new HealthDashboard(hubConnection, null, logger);
        _healthDashboards.Add(dashboard);

        var updateReceived = new TaskCompletionSource<HealthUpdate>();
        dashboard.DataReceived += (sender, args) => updateReceived.TrySetResult(args.Data);

        await dashboard.ConnectAsync(CancellationToken.None);

        // Act - Send health update via hub connection
        var healthUpdate = new HealthUpdate
        {
            ServiceName = "TestService",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow
        };

        await hubConnection.InvokeAsync("SendHealthUpdate", healthUpdate, CancellationToken.None);

        // Wait for update with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var receivedUpdate = await updateReceived.Task.WaitAsync(cts.Token);

        // Assert
        receivedUpdate.ShouldNotBeNull();
        receivedUpdate.ServiceName.ShouldBe(healthUpdate.ServiceName);
        receivedUpdate.Status.ShouldBe(healthUpdate.Status);

        // Cleanup
        await dashboard.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// Test: ServiceHealth should track health status changes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ServiceHealth_Should_Track_Health_Status_Changes()
    {
        // Arrange
        var logger = new LoggerFactory().CreateLogger<ClientServiceHealth>();
        var healthMonitor = new ClientServiceHealth(logger);
        _healthMonitors.Add(healthMonitor);

        var statusChanged = new TaskCompletionSource<HealthStatusChangedEventArgs<HealthStatusData>>();
        healthMonitor.HealthStatusChanged += (sender, args) => statusChanged.TrySetResult(args);

        // Act
        var healthData = new HealthStatusData
        {
            ClientId = "TestClient",
            ConnectionCount = 1,
            LastActivity = DateTime.UtcNow,
            MemoryUsageMB = 100
        };

        var result = await healthMonitor.UpdateHealthAsync(HealthStatus.Healthy, healthData, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        healthMonitor.Status.ShouldBe(HealthStatus.Healthy);
        healthMonitor.Data.ShouldNotBeNull();
        healthMonitor.Data!.ClientId.ShouldBe("TestClient");

        // Update to unhealthy
        await healthMonitor.UpdateHealthAsync(HealthStatus.Unhealthy, healthData, CancellationToken.None);
        healthMonitor.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    /// <summary>
    /// Test: Dashboard should handle connection state changes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Dashboard_Should_Handle_Connection_State_Changes()
    {
        // Arrange
        var hubConnection = _serverFactory.CreateHubConnection("/hubs/system");
        var logger = new LoggerFactory().CreateLogger<SystemDashboard>();
        var dashboard = new SystemDashboard(hubConnection, null, logger);
        _systemDashboards.Add(dashboard);

        var stateChanges = new List<ConnectionStateChangedEventArgs>();
        dashboard.ConnectionStateChanged += (sender, args) => stateChanges.Add(args);

        // Act
        await dashboard.ConnectAsync(CancellationToken.None);
        await Task.Delay(100, CancellationToken.None); // Allow state change to propagate
        await dashboard.DisconnectAsync(CancellationToken.None);

        // Assert
        stateChanges.Count.ShouldBeGreaterThan(0);
        stateChanges.ShouldContain(s => s.NewState == ConnectionState.Connected);
        stateChanges.ShouldContain(s => s.NewState == ConnectionState.Disconnected);
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


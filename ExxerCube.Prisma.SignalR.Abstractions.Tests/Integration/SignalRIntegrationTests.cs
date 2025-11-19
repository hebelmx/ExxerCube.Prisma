using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestServer = Microsoft.AspNetCore.TestHost.TestServer;

namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Integration;

/// <summary>
/// Integration tests for SignalR hubs with real server infrastructure.
/// These tests use TestServer to run a real SignalR server in-memory.
/// </summary>
public class SignalRIntegrationTests : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly TestServer _testServer;
    private readonly List<HubConnection> _connections;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRIntegrationTests"/> class.
    /// </summary>
    public SignalRIntegrationTests()
    {
        _connections = new List<HubConnection>();

        // Create test host with SignalR
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSignalR();
                    services.AddLogging(builder => builder.AddConsole());
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHub<TestIntegrationHub>("/hubs/test");
                    });
                });
            });

        _host = hostBuilder.Build();
        _host.StartAsync().GetAwaiter().GetResult();
        _testServer = _host.GetTestServer();
    }

    /// <summary>
    /// Tests that a hub connection can be established successfully.
    /// </summary>
    [Fact]
    public async Task Connect_ToHub_Successfully()
    {
        // Arrange
        var connection = CreateConnection("/hubs/test");

        // Act
        await connection.StartAsync(CancellationToken.None);

        // Assert
        connection.State.ShouldBe(HubConnectionState.Connected);
    }

    /// <summary>
    /// Tests that messages can be received from the hub.
    /// </summary>
    [Fact]
    public async Task Receive_Message_FromHub()
    {
        // Arrange
        var connection = CreateConnection("/hubs/test");
        await connection.StartAsync(CancellationToken.None);
        
        var receivedMessage = false;
        var receivedData = (TestData?)null;
        using var eventWaitHandle = new ManualResetEventSlim(false);

        connection.On<TestData>("ReceiveMessage", data =>
        {
            receivedMessage = true;
            receivedData = data;
            eventWaitHandle.Set();
        });

        // Act - Send message via hub context
        var hubContext = _host.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<TestIntegrationHub>>();
        var testData = new TestData { Id = 1, Name = "Test" };
        await hubContext.Clients.All.SendAsync("ReceiveMessage", testData, CancellationToken.None);
        
        // Wait for message to be received with timeout
        eventWaitHandle.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);

        // Assert
        receivedMessage.ShouldBeTrue();
        receivedData.ShouldNotBeNull();
        receivedData!.Id.ShouldBe(1);
        receivedData.Name.ShouldBe("Test");
    }

    /// <summary>
    /// Tests that connection can be disconnected gracefully.
    /// </summary>
    [Fact]
    public async Task Disconnect_FromHub_Successfully()
    {
        // Arrange
        var connection = CreateConnection("/hubs/test");
        await connection.StartAsync(CancellationToken.None);
        connection.State.ShouldBe(HubConnectionState.Connected);

        // Act
        await connection.StopAsync(CancellationToken.None);

        // Assert
        connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    /// <summary>
    /// Creates a hub connection to the specified endpoint.
    /// </summary>
    private HubConnection CreateConnection(string endpoint)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_testServer.BaseAddress + endpoint.TrimStart('/'), o =>
            {
                o.HttpMessageHandlerFactory = _ => _testServer.CreateHandler();
            })
            .Build();

        _connections.Add(connection);
        return connection;
    }

    /// <summary>
    /// Disposes the test infrastructure.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            if (connection.State != HubConnectionState.Disconnected)
            {
                await connection.StopAsync();
            }
            await connection.DisposeAsync();
        }

        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// Test hub for integration testing.
    /// </summary>
    private class TestIntegrationHub : ExxerHub<TestData>
    {
        public TestIntegrationHub(ILogger<TestIntegrationHub> logger)
            : base(logger)
        {
        }
    }

    /// <summary>
    /// Test data class.
    /// </summary>
    public class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}


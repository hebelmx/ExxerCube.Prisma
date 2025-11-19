namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Abstractions.Dashboards;

/// <summary>
/// Tests for the Dashboard&lt;T&gt; base class.
/// </summary>
public class DashboardTests
{
    private readonly ILogger<Dashboard<TestData>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardTests"/> class.
    /// </summary>
    public DashboardTests()
    {
        _logger = Substitute.For<ILogger<Dashboard<TestData>>>();
    }

    /// <summary>
    /// Tests that initial connection state is Disconnected.
    /// </summary>
    [Fact]
    public void Constructor_Initializes_WithDisconnectedState()
    {
        // Arrange & Act
        var dashboard = new TestDashboard(null, null, _logger);

        // Assert
        dashboard.ConnectionState.ShouldBe(ConnectionState.Disconnected);
        dashboard.Data.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that ConnectAsync returns failure when hub connection is null.
    /// </summary>
    [Fact]
    public async Task ConnectAsync_WithNullHubConnection_ReturnsFailure()
    {
        // Arrange
        var dashboard = new TestDashboard(null, null, _logger);

        // Act
        var result = await dashboard.ConnectAsync(CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.ShouldContain("not configured");
    }

    /// <summary>
    /// Tests that ConnectAsync returns cancelled when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task ConnectAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        // Note: HubConnection cannot be easily mocked due to its constructor requirements
        // This test focuses on cancellation logic which is testable
        var dashboard = new TestDashboard(null, null, _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await dashboard.ConnectAsync(cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that DisconnectAsync returns success when hub connection is null.
    /// </summary>
    [Fact]
    public async Task DisconnectAsync_WithNullHubConnection_ReturnsSuccess()
    {
        // Arrange
        var dashboard = new TestDashboard(null, null, _logger);

        // Act
        var result = await dashboard.DisconnectAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that OnMessageReceived adds data to the collection.
    /// </summary>
    [Fact]
    public void OnMessageReceived_WithValidData_AddsToCollection()
    {
        // Arrange
        var dashboard = new TestDashboard(null, null, _logger);
        var testData = new TestData { Id = 1, Name = "Test" };

        // Act
        dashboard.OnMessageReceivedPublic(testData);

        // Assert
        dashboard.Data.ShouldContain(testData);
        dashboard.Data.Count.ShouldBe(1);
    }

    /// <summary>
    /// Tests that OnMessageReceived raises DataReceived event.
    /// </summary>
    [Fact]
    public void OnMessageReceived_Raises_DataReceivedEvent()
    {
        // Arrange
        var dashboard = new TestDashboard(null, null, _logger);
        var testData = new TestData { Id = 1, Name = "Test" };
        var eventRaised = false;
        DataReceivedEventArgs<TestData>? eventArgs = null;

        dashboard.DataReceived += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act
        dashboard.OnMessageReceivedPublic(testData);

        // Assert
        eventRaised.ShouldBeTrue();
        eventArgs.ShouldNotBeNull();
        eventArgs.Data.ShouldBe(testData);
    }

    /// <summary>
    /// Tests that OnMessageReceived ignores null data.
    /// </summary>
    [Fact]
    public void OnMessageReceived_WithNullData_DoesNotAddToCollection()
    {
        // Arrange
        var dashboard = new TestDashboard(null, null, _logger);

        // Act
        dashboard.OnMessageReceivedPublic(null!);

        // Assert
        dashboard.Data.ShouldBeEmpty();
    }

    // Note: HubConnection disposal testing requires actual SignalR infrastructure
    // Integration tests should be added separately for full connection lifecycle testing

    /// <summary>
    /// Test dashboard implementation for testing.
    /// </summary>
    private class TestDashboard : Dashboard<TestData>
    {
        public TestDashboard(
            HubConnection? hubConnection,
            ReconnectionStrategy? reconnectionStrategy,
            ILogger<Dashboard<TestData>> logger)
            : base(hubConnection, reconnectionStrategy, logger)
        {
        }

        public void OnMessageReceivedPublic(TestData data)
        {
            OnMessageReceived(data);
        }
    }

    /// <summary>
    /// Test data class for testing.
    /// </summary>
    public class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}


namespace ExxerCube.Prisma.SignalR.Abstractions.Tests.Abstractions.Health;

/// <summary>
/// Tests for the ServiceHealth&lt;T&gt; class.
/// </summary>
public class ServiceHealthTests
{
    private readonly ILogger<ServiceHealth<TestHealthData>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceHealthTests"/> class.
    /// </summary>
    public ServiceHealthTests()
    {
        _logger = Substitute.For<ILogger<ServiceHealth<TestHealthData>>>();
    }

    /// <summary>
    /// Tests that initial status is Healthy.
    /// </summary>
    [Fact]
    public void Constructor_Initializes_WithHealthyStatus()
    {
        // Arrange & Act
        var health = new ServiceHealth<TestHealthData>(_logger);

        // Assert
        health.Status.ShouldBe(HealthStatus.Healthy);
        health.Data.ShouldBeNull();
    }

    /// <summary>
    /// Tests that UpdateHealthAsync updates the status successfully.
    /// </summary>
    [Fact]
    public async Task UpdateHealthAsync_WithNewStatus_UpdatesStatus()
    {
        // Arrange
        var health = new ServiceHealth<TestHealthData>(_logger);
        var newStatus = HealthStatus.Degraded;
        var healthData = new TestHealthData { Message = "Test" };

        // Act
        var result = await health.UpdateHealthAsync(newStatus, healthData, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        health.Status.ShouldBe(newStatus);
        health.Data.ShouldBe(healthData);
    }

    /// <summary>
    /// Tests that UpdateHealthAsync raises HealthStatusChanged event when status changes.
    /// </summary>
    [Fact]
    public async Task UpdateHealthAsync_WithStatusChange_RaisesEvent()
    {
        // Arrange
        var health = new ServiceHealth<TestHealthData>(_logger);
        var eventRaised = false;
        HealthStatusChangedEventArgs<TestHealthData>? eventArgs = null;

        health.HealthStatusChanged += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act
        await health.UpdateHealthAsync(HealthStatus.Unhealthy, null, CancellationToken.None);

        // Assert
        eventRaised.ShouldBeTrue();
        eventArgs.ShouldNotBeNull();
        eventArgs.PreviousStatus.ShouldBe(HealthStatus.Healthy);
        eventArgs.NewStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    /// <summary>
    /// Tests that UpdateHealthAsync does not raise event when status doesn't change.
    /// </summary>
    [Fact]
    public async Task UpdateHealthAsync_WithSameStatus_DoesNotRaiseEvent()
    {
        // Arrange
        var health = new ServiceHealth<TestHealthData>(_logger);
        var eventRaised = false;

        health.HealthStatusChanged += (sender, args) => eventRaised = true;

        // Act
        await health.UpdateHealthAsync(HealthStatus.Healthy, null, CancellationToken.None);

        // Assert
        eventRaised.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that UpdateHealthAsync returns cancelled when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task UpdateHealthAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var health = new ServiceHealth<TestHealthData>(_logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await health.UpdateHealthAsync(HealthStatus.Unhealthy, null, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that LastUpdated is updated when health status changes.
    /// </summary>
    [Fact]
    public async Task UpdateHealthAsync_Updates_LastUpdatedTimestamp()
    {
        // Arrange
        var health = new ServiceHealth<TestHealthData>(_logger);
        var initialTime = health.LastUpdated;
        await Task.Delay(10, CancellationToken.None);

        // Act
        await health.UpdateHealthAsync(HealthStatus.Degraded, null, CancellationToken.None);

        // Assert
        health.LastUpdated.ShouldBeGreaterThan(initialTime);
    }

    /// <summary>
    /// Test health data class for testing.
    /// </summary>
    public class TestHealthData
    {
        public string Message { get; set; } = string.Empty;
    }
}


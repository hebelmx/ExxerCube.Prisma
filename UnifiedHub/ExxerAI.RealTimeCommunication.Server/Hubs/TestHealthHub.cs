using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Server.Hubs;

/// <summary>
/// Test implementation of HealthHub for integration testing and usage examples.
/// Demonstrates health monitoring via SignalR.
/// </summary>
public class TestHealthHub : ExxerHub<HealthUpdate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestHealthHub"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public TestHealthHub(ILogger<TestHealthHub> logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Hub method exposed to SignalR clients for sending health updates to all clients.
    /// Wrapper around SendToAllAsync that returns Task for SignalR compatibility.
    /// </summary>
    /// <param name="healthUpdate">The health update data.</param>
    public async Task SendHealthUpdate(HealthUpdate healthUpdate)
    {
        // Call the base method but ignore the Result return value for SignalR compatibility
        _ = await base.SendToAllAsync(healthUpdate, CancellationToken.None);
    }

}

/// <summary>
/// Health update model for testing and examples.
/// </summary>
public record HealthUpdate
{
    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the additional health data.
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }
}


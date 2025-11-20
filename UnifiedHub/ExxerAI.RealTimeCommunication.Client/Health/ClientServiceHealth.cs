using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Health;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Client.Health;

/// <summary>
/// Client-side service health monitoring implementation.
/// Demonstrates ServiceHealth&lt;T&gt; usage for tracking client application health.
/// </summary>
public class ClientServiceHealth : ServiceHealth<HealthStatusData>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientServiceHealth"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ClientServiceHealth(ILogger<ClientServiceHealth> logger)
        : base(logger)
    {
    }
}

/// <summary>
/// Health status data model for client health monitoring.
/// Uses System.Diagnostics for system metrics.
/// </summary>
public record HealthStatusData
{
    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection count.
    /// </summary>
    public int ConnectionCount { get; init; }

    /// <summary>
    /// Gets or sets the last activity timestamp.
    /// </summary>
    public DateTime LastActivity { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets or sets the memory usage in MB.
    /// </summary>
    public long MemoryUsageMB { get; init; }

    /// <summary>
    /// Gets or sets the total memory in MB.
    /// </summary>
    public long TotalMemoryMB { get; init; }

    /// <summary>
    /// Gets or sets the memory usage percentage.
    /// </summary>
    public double MemoryUsagePercent { get; init; }

    /// <summary>
    /// Gets or sets the process count.
    /// </summary>
    public int ProcessCount { get; init; }

    /// <summary>
    /// Gets or sets the thread count.
    /// </summary>
    public int ThreadCount { get; init; }
}


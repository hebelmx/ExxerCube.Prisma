namespace ExxerAI.RealTimeCommunication.Server.Models;

/// <summary>
/// System diagnostics data model for health monitoring.
/// Contains CPU, memory, and other system metrics.
/// </summary>
public record SystemDiagnosticsData
{
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

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the machine name.
    /// </summary>
    public string MachineName { get; init; } = Environment.MachineName;
}


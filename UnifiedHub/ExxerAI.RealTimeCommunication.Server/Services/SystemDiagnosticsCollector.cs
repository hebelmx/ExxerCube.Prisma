using System.Diagnostics;
using ExxerAI.RealTimeCommunication.Server.Models;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Server.Services;

/// <summary>
/// Service that collects system diagnostics information.
/// Provides CPU, memory, and process metrics using System.Diagnostics.
/// Uses process-based CPU calculation for cross-platform compatibility.
/// </summary>
public class SystemDiagnosticsCollector
{
    private readonly ILogger<SystemDiagnosticsCollector> _logger;
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemDiagnosticsCollector"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SystemDiagnosticsCollector(ILogger<SystemDiagnosticsCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Collects current system diagnostics data.
    /// Uses process-based CPU calculation for cross-platform compatibility.
    /// </summary>
    /// <returns>System diagnostics data.</returns>
    public SystemDiagnosticsData Collect()
    {
        var process = Process.GetCurrentProcess();
        var currentCpuTime = process.TotalProcessorTime;
        var currentTime = DateTime.UtcNow;
        var timeDelta = (currentTime - _lastCpuCheck).TotalSeconds;

        // Calculate CPU usage (process-based, cross-platform)
        double cpuUsage = 0;
        if (timeDelta > 0 && _lastCpuCheck != default)
        {
            var cpuTimeDelta = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var totalCpuTime = Environment.ProcessorCount * timeDelta * 1000; // Total available CPU time
            cpuUsage = totalCpuTime > 0 ? (cpuTimeDelta / totalCpuTime) * 100 : 0;
            cpuUsage = Math.Min(100, Math.Max(0, cpuUsage)); // Clamp between 0-100
        }

        _lastCpuCheck = currentTime;
        _lastCpuTime = currentCpuTime;

        // Get memory information
        var workingSetMB = process.WorkingSet64 / 1024 / 1024;
        var totalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
        var memoryUsagePercent = totalMemoryMB > 0 ? (workingSetMB * 100.0 / totalMemoryMB) : 0;

        // Get process and thread counts
        var processCount = Process.GetProcesses().Length;
        var threadCount = process.Threads.Count;

        return new SystemDiagnosticsData
        {
            CpuUsagePercent = cpuUsage,
            MemoryUsageMB = workingSetMB,
            TotalMemoryMB = totalMemoryMB,
            MemoryUsagePercent = memoryUsagePercent,
            ProcessCount = processCount,
            ThreadCount = threadCount,
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName
        };
    }
}


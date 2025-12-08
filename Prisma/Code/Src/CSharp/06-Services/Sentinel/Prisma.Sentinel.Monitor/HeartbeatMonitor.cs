using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Monitors worker heartbeats and detects failures based on missed heartbeat thresholds.
/// </summary>
public sealed class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly ISentinelConfiguration _configuration;
    private readonly ConcurrentDictionary<string, WorkerHeartbeat> _heartbeats = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HeartbeatMonitor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="configuration">Sentinel configuration (optional, uses defaults if null).</param>
    public HeartbeatMonitor(ILogger<HeartbeatMonitor> logger, ISentinelConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? new DefaultSentinelConfiguration();
    }

    /// <inheritdoc />
    public Task RecordHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        if (heartbeat == null)
            throw new ArgumentNullException(nameof(heartbeat));

        _heartbeats.AddOrUpdate(heartbeat.WorkerId, heartbeat, (_, _) => heartbeat);
        _logger.LogDebug("Recorded heartbeat from worker {WorkerId} at {Timestamp}", heartbeat.WorkerId, heartbeat.Timestamp);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetFailedWorkersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var timeout = _configuration.HeartbeatTimeout;
        var threshold = _configuration.MissedHeartbeatThreshold;

        var failedWorkers = _heartbeats.Values
            .Where(heartbeat =>
            {
                var elapsed = now - heartbeat.Timestamp;
                var missedHeartbeats = (int)(elapsed.TotalSeconds / timeout.TotalSeconds);
                return missedHeartbeats >= threshold;
            })
            .Select(h => h.WorkerId)
            .ToList();

        if (failedWorkers.Any())
        {
            _logger.LogWarning("Detected {Count} failed workers: {WorkerIds}", failedWorkers.Count, string.Join(", ", failedWorkers));
        }

        return Task.FromResult<IEnumerable<string>>(failedWorkers);
    }
}

/// <summary>
/// Default sentinel configuration with standard values.
/// </summary>
internal sealed class DefaultSentinelConfiguration : ISentinelConfiguration
{
    /// <inheritdoc />
    public TimeSpan HeartbeatTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public int MissedHeartbeatThreshold => 3;

    /// <inheritdoc />
    public TimeSpan CheckInterval => TimeSpan.FromSeconds(10);
}

namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Configuration for Sentinel monitoring behavior.
/// </summary>
public interface ISentinelConfiguration
{
    /// <summary>
    /// Maximum time allowed between heartbeats before considering a worker failed.
    /// </summary>
    TimeSpan HeartbeatTimeout { get; }

    /// <summary>
    /// Number of consecutive missed heartbeats before triggering restart.
    /// </summary>
    int MissedHeartbeatThreshold { get; }

    /// <summary>
    /// Interval at which to check for failed workers.
    /// </summary>
    TimeSpan CheckInterval { get; }
}

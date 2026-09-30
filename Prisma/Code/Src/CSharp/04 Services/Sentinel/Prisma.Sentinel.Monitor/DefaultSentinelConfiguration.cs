namespace Prisma.Sentinel.Monitor;

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
namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents the health status of a component or system.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The component is healthy and functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// The component is degraded but still functioning.
    /// </summary>
    Degraded,

    /// <summary>
    /// The component is unhealthy and not functioning properly.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// The health status is unknown.
    /// </summary>
    Unknown
}
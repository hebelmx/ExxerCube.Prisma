namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Worker status enumeration for heartbeat monitoring.
/// </summary>
public enum WorkerStatus
{
    /// <summary>
    /// Worker is running and ready to process.
    /// </summary>
    Running,

    /// <summary>
    /// Worker is idle (no work available).
    /// </summary>
    Idle,

    /// <summary>
    /// Worker is actively processing.
    /// </summary>
    Processing,

    /// <summary>
    /// Worker encountered an error but is still alive.
    /// </summary>
    Error
}
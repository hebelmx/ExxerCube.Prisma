namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Dashboard statistics.
/// </summary>
/// <param name="WorkerName">Name of the worker.</param>
/// <param name="Status">Current worker status.</param>
/// <param name="DocumentsProcessed">Total documents processed since start.</param>
/// <param name="LastEventTime">Timestamp of last processed event (null if none).</param>
/// <param name="LastHeartbeat">Timestamp of last heartbeat.</param>
/// <param name="QueueDepth">Current queue depth (0 if not available).</param>
public record DashboardStats(
    string WorkerName,
    string Status,
    int DocumentsProcessed,
    DateTime? LastEventTime,
    DateTime? LastHeartbeat,
    int QueueDepth);
namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Worker heartbeat event for monitoring and health tracking.
/// </summary>
/// <remarks>
/// Emitted periodically by workers (Orion, Athena) to signal they are alive and processing.
/// Sentinel Monitor consumes these to detect zombie/dead workers and trigger restarts.
/// </remarks>
/// <param name="WorkerId">Unique identifier for the worker instance.</param>
/// <param name="WorkerName">Human-readable worker name (e.g., "Orion Ingestion Worker").</param>
/// <param name="Timestamp">UTC timestamp when heartbeat was emitted.</param>
/// <param name="Status">Worker status (Running, Idle, Processing, Error).</param>
/// <param name="DocumentsProcessed">Total documents processed since worker start.</param>
/// <param name="LastEventTime">Timestamp of last processed event (null if none).</param>
/// <param name="HealthEndpoint">Optional health endpoint URL for verification.</param>
public record WorkerHeartbeat(
    string WorkerId,
    string WorkerName,
    DateTime Timestamp,
    WorkerStatus Status,
    int DocumentsProcessed,
    DateTime? LastEventTime,
    string? HealthEndpoint = null);
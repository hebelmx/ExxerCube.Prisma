using Prisma.Shared.Contracts;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Orchestrates SIARA monitoring, download, and journaling (logic only, host-agnostic).
/// </summary>
public class IngestionOrchestrator
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Placeholder for watcher wiring; implement SIARA polling, download, partitioned storage, and journal writes.
        return Task.CompletedTask;
    }
}

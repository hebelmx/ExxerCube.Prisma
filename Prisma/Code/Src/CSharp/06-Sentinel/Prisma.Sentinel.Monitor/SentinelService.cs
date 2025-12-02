using Prisma.Shared.Contracts;

namespace Prisma.Sentinel.Monitor;

/// <summary>
/// Placeholder for process/heartbeat monitoring across Orion/Athena workers.
/// </summary>
public class SentinelService
{
    public Task MonitorAsync(CancellationToken cancellationToken = default)
    {
        // TODO: implement heartbeat polling and restart hooks.
        return Task.CompletedTask;
    }
}

using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;

namespace Prisma.Orion.Worker.Ingestion;

/// <summary>
/// The SignalR hub the Orion <em>Downloader</em> actor hosts so the downstream <em>Extractor</em> actor
/// (Athena) can subscribe to <see cref="DocumentDownloadedEvent"/>s over the real IndFusion.Ember transport
/// (MVP-PATH 1.3, the first cross-process edge of the Three-Actors split — ADR-009/ADR-011).
/// </summary>
/// <remarks>
/// This type is only the connection endpoint: clients connect here and receive broadcasts on the Ember
/// <c>"ReceiveMessage"</c> protocol (see <see cref="ExxerHub{T}"/>). Outbound broadcasting is done by
/// <see cref="SignalRIngestionBroadcaster"/> via <see cref="Microsoft.AspNetCore.SignalR.IHubContext{THub}"/>,
/// because a hub instance resolved from DI (rather than created per-invocation by SignalR) has a null
/// <c>Clients</c> and cannot send.
/// </remarks>
public sealed class IngestionHub : ExxerHub<DocumentDownloadedEvent>
{
    /// <summary>Initializes a new instance of the <see cref="IngestionHub"/> class.</summary>
    /// <param name="logger">The logger instance.</param>
    public IngestionHub(ILogger<IngestionHub> logger)
        : base(logger)
    {
    }
}

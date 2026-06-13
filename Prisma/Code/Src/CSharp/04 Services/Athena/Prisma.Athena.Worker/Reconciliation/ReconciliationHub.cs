using ExxerCube.Prisma.Domain.Events;
using IndFusion.Ember.Abstractions.Hubs;

namespace Prisma.Athena.Worker.Reconciliation;

/// <summary>
/// The SignalR hub the Athena <em>Extractor</em> actor hosts so the downstream <em>Reconciliator</em> actor can
/// subscribe to <see cref="ExtractionCompletedEvent"/>s over the real IndFusion.Ember transport (MVP-PATH 1.4,
/// the second cross-process edge of the Three-Actors split — ADR-009/ADR-011).
/// </summary>
/// <remarks>
/// This type is only the connection endpoint: clients connect here and receive broadcasts on the Ember
/// <c>"ReceiveMessage"</c> protocol (see <see cref="ExxerHub{T}"/>). Outbound broadcasting is done by
/// <see cref="SignalRReconciliationBroadcaster"/> via <see cref="Microsoft.AspNetCore.SignalR.IHubContext{THub}"/>,
/// because a hub instance resolved from DI (rather than created per-invocation by SignalR) has a null
/// <c>Clients</c> and cannot send.
/// </remarks>
public sealed class ReconciliationHub : ExxerHub<ExtractionCompletedEvent>
{
    /// <summary>Initializes a new instance of the <see cref="ReconciliationHub"/> class.</summary>
    /// <param name="logger">The logger instance.</param>
    public ReconciliationHub(ILogger<ReconciliationHub> logger)
        : base(logger)
    {
    }
}

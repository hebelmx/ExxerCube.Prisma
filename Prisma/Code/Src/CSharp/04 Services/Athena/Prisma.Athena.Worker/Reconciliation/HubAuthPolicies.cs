namespace Prisma.Athena.Worker.Reconciliation;

/// <summary>
/// Authorization policy name constants for the Athena Worker hub (follow-up to MVP-PATH 1.5).
/// </summary>
internal static class HubAuthPolicies
{
    /// <summary>
    /// Policy that requires the connecting caller to present a JWT clearance token with
    /// <c>ProcessClearance.Reconcile</c> — the Reconciliator is the only authorized subscriber to
    /// <c>/hubs/reconciliation</c>.
    /// </summary>
    internal const string RequireReconcileClearance = "RequireReconcileClearance";
}

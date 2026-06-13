namespace Prisma.Orion.Worker.Ingestion;

/// <summary>
/// Authorization policy name constants for the Orion Worker hub (follow-up to MVP-PATH 1.5).
/// </summary>
internal static class HubAuthPolicies
{
    /// <summary>
    /// Policy that requires the connecting caller to present a JWT clearance token with
    /// <c>ProcessClearance.Extract</c> — the Athena Extractor is the only authorized subscriber to
    /// <c>/hubs/ingestion</c>.
    /// </summary>
    internal const string RequireExtractClearance = "RequireExtractClearance";
}

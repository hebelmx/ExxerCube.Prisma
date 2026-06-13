using System;

namespace Prisma.Reconciliator.Worker;

/// <summary>
/// Deployment-time configuration for the Reconciliator's subscriber (MVP-PATH 1.4 Reconciliator edge): how to
/// reach the Athena Extractor actor's SignalR reconciliation hub. Bind from the <c>Reconciliation</c>
/// configuration section. Mirrors the 1.3 <c>IngestionClientOptions</c>.
/// </summary>
public sealed class ReconciliationClientOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Reconciliation";

    /// <summary>
    /// Gets or sets the absolute URL of the Athena reconciliation hub (for example
    /// <c>http://athena:8080/hubs/reconciliation</c>). When blank, the subscriber stays idle (it logs a warning
    /// and does not attempt to connect) — so a single-service or test deployment boots cleanly.
    /// </summary>
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the delay between connection attempts while the hub is unreachable. Default 5s.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}

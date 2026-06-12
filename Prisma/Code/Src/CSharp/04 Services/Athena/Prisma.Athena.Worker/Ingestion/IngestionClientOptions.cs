using System;

namespace Prisma.Athena.Worker.Ingestion;

/// <summary>
/// Deployment-time configuration for the Athena ingestion subscriber (MVP-PATH 1.3): how to reach the Orion
/// Downloader actor's SignalR ingestion hub. Bind from the <c>Ingestion</c> configuration section.
/// </summary>
public sealed class IngestionClientOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Gets or sets the absolute URL of the Orion ingestion hub (for example
    /// <c>http://orion:8080/hubs/ingestion</c>). When blank, the subscriber stays idle (it logs a warning
    /// and does not attempt to connect) — so a single-service or test deployment boots cleanly.
    /// </summary>
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the delay between connection attempts while the hub is unreachable. Default 5s.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}

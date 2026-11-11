using System;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Deployment-time configuration for the SIARA watch loop (MVP-PATH 1.2). Bind from the
/// <c>OrionWatchLoop</c> configuration section.
/// </summary>
public sealed class WatchLoopOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "OrionWatchLoop";

    /// <summary>
    /// Gets or sets the delay between discovery passes. A relaxed cadence is deliberate: SIARA presents a
    /// modest daily volume (≤ ~2k documents/day) and the SHA-256 journal makes re-discovery idempotent, so
    /// there is no need to poll aggressively. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);
}

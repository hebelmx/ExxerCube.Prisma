using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;

/// <summary>
/// Configuration options for the VEC RED-verdict alert system.
/// Bind from the <c>"Veriqan:Alerts"</c> configuration section.
/// </summary>
public sealed class AlertOptions
{
    /// <summary>The configuration section key used when binding from <c>appsettings.json</c>.</summary>
    public const string SectionKey = "Veriqan:Alerts";

    /// <summary>
    /// Gets or sets the list of recipient email addresses that receive RED-verdict alerts.
    /// Must contain at least one address for alerts to be dispatched.
    /// </summary>
    public List<string> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of send attempts (initial attempt + retries).
    /// Defaults to <c>3</c>.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay in milliseconds between retry attempts.
    /// Each retry waits <c>BaseRetryDelayMs * 2^(attempt-1)</c> (exponential back-off).
    /// Defaults to <c>500</c> ms.
    /// </summary>
    public int BaseRetryDelayMs { get; set; } = 500;
}

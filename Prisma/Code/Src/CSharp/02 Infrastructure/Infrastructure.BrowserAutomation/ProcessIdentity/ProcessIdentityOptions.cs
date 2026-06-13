using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;

/// <summary>
/// Deployment-time configuration for the per-process JWT clearance token service (MVP-PATH 1.5, A5).
/// Bind from the <c>ProcessIdentity</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// Each of the three pipeline processes (Orion Downloader, Athena Extractor, Reconciliator) is configured
/// with the same <see cref="JwtIssuer"/>, <see cref="JwtAudience"/>, and shared <see cref="JwtSecret"/>
/// (mount via Kubernetes Secret or Key Vault config provider — never hardcode a real secret).
/// The <see cref="Clearance"/> differs per process and determines which stage it is authorised to push.
/// </para>
/// <para>
/// Modelled on <c>EfCoreIdentityConfiguration</c> in the Auth.Infrastructure project: same field names,
/// same HMAC-SHA256 signing pattern. The two are independent — no Identity DB dependency here.
/// </para>
/// </remarks>
public sealed class ProcessIdentityOptions
{
    /// <summary>The configuration section name these options bind to.</summary>
    public const string SectionName = "ProcessIdentity";

    /// <summary>
    /// Gets or sets the symmetric signing secret shared across all three pipeline processes. This is a
    /// bearer secret — provide it via an environment variable or a Key Vault config provider in
    /// production; never commit a real value to source control.
    /// </summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JWT issuer claim. All three processes must use the same value so tokens minted
    /// by one process pass validation in another. Defaults to <c>prisma-pipeline</c>.
    /// </summary>
    public string JwtIssuer { get; set; } = "prisma-pipeline";

    /// <summary>
    /// Gets or sets the JWT audience claim. All three processes must use the same value. Defaults to
    /// <c>prisma-pipeline</c>.
    /// </summary>
    public string JwtAudience { get; set; } = "prisma-pipeline";

    /// <summary>
    /// Gets or sets how long minted tokens remain valid. Defaults to 5 minutes — long enough to survive
    /// worst-case transit latency; short enough to limit replay exposure. Set <c>ClockSkew = 30s</c> in
    /// the receiver to tolerate minor NTP drift (see <c>JwtProcessClearanceTokenService</c>).
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets which pipeline stage this process is cleared to initiate.
    /// Orion Downloader → <see cref="ProcessClearance.Download"/>;
    /// Athena Extractor → <see cref="ProcessClearance.Extract"/>;
    /// Reconciliator → <see cref="ProcessClearance.Reconcile"/>.
    /// </summary>
    public ProcessClearance Clearance { get; set; } = ProcessClearance.Download;
}

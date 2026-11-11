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

    /// <summary>
    /// Gets or sets retired signing secrets still accepted for token <em>validation</em> during a
    /// zero-downtime HMAC secret rotation (RC6 item 3.9). <see cref="JwtSecret"/> remains the sole secret
    /// used to <em>mint</em> new tokens; entries here are validate-only, so an in-flight token minted with
    /// the previous secret before a rotation is not rejected mid-transit. Defaults to empty (no grace
    /// window in effect).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Rotation procedure</strong> is <strong>three-phase</strong>, config-only, no code deploy:
    /// (A) pre-stage — leave <see cref="JwtSecret"/> unchanged, put the new secret into this list, and
    /// rolling-restart all three processes so every validator accepts the new secret while nobody mints
    /// it yet; (B) cut over — set <see cref="JwtSecret"/> to the new value, move the old value into this
    /// list, and rolling-restart again so minting switches over while every validator still accepts both;
    /// (C) retire — once <see cref="TokenLifetime"/> plus the 30s validation <c>ClockSkew</c> has drained
    /// (comfortably covered by waiting ~6 minutes after the last Phase-B restart), remove the old secret
    /// from this list and rolling-restart once more. A single-phase "swap secret + old-as-previous, restart
    /// in any order" rollout is <em>not</em> safe: the first host restarted immediately mints with the new
    /// secret while not-yet-restarted peers (whose accept-set was baked in at their own last startup) still
    /// validate old-only, and the handoff between them is rejected fail-closed. See the "Correction" note
    /// at the top of <c>docs/architecture/adr/ADR-012-addendum-hmac-rotation-grace-2026-07-23.md</c> §3 for
    /// the full failure-mode walkthrough and the corrected three-phase procedure.
    /// </para>
    /// <para>
    /// Use <see cref="ProcessIdentitySigningKeys.BuildAcceptedKeys(ProcessIdentityOptions)"/> to compute
    /// the accepted validation key set (current secret first, then these, de-duplicated) — it is the
    /// single source of truth shared by <c>JwtProcessClearanceTokenService.ValidateAsync</c> and each
    /// worker's <c>AddJwtBearer</c> connection-level auth so the two paths cannot drift.
    /// </para>
    /// <para>
    /// Secret values are deliberately excluded from any diagnostic surface: this type does not override
    /// <see cref="object.ToString"/>, so default reflection-based formatting (e.g. record-style dumps) is
    /// the only way its property values could ever be rendered, and no code in this codebase logs
    /// <see cref="ProcessIdentityOptions"/> instances directly — only individual non-secret claims
    /// (actor id, clearance, file id) are ever passed to the logger.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> PreviousJwtSecrets { get; set; } = Array.Empty<string>();
}

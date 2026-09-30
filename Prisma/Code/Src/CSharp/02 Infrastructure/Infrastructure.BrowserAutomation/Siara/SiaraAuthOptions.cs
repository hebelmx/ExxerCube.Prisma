using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// Deployment-time configuration for the SIARA authentication seam (ADR-010). The client selects the
/// mode and the per-mode parameters; nothing is hardcoded and no raw credentials are carried here.
/// </summary>
/// <remarks>
/// Grown incrementally across the SIARA-AUTH-DESIGN tasks: S5 adds <see cref="Passthrough"/>; S6 adds
/// <see cref="Interactive"/>; the <see cref="AuthMode"/> selector plus the automated sub-options and the
/// resolver wiring land in S7. Bind from the <c>Siara</c> configuration section.
/// </remarks>
public sealed class SiaraAuthOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Siara";

    /// <summary>
    /// Gets or sets the auth mode the resolver should honor. Defaults to the bank-safe
    /// <see cref="SiaraAuthMode.SessionPassthrough"/>. (The resolver that reads this lands in S7.)
    /// </summary>
    public SiaraAuthMode AuthMode { get; set; } = SiaraAuthMode.SessionPassthrough;

    /// <summary>
    /// Gets or sets whether logging in against the real SIARA production host
    /// (<see cref="SiaraHostPolicy.ProductionHost"/>) is permitted. Defaults to <c>false</c> — the login
    /// driver fails closed on the production host (ADR-010 P8) so the fake-credential simulator and the
    /// automation tooling cannot be repointed at the regulator's portal. A deployment may only set this to
    /// <c>true</c> once legal/compliance has signed off on automated access (ADR-010 P1); flipping it is a
    /// configuration change, not a code change.
    /// </summary>
    public bool AllowProductionHost { get; set; }

    /// <summary>Gets or sets the <see cref="SiaraAuthMode.SessionPassthrough"/> parameters.</summary>
    public SiaraPassthroughOptions Passthrough { get; set; } = new();

    /// <summary>Gets or sets the <see cref="SiaraAuthMode.InteractiveLogin"/> parameters.</summary>
    public SiaraInteractiveOptions Interactive { get; set; } = new();

    /// <summary>Gets or sets the <see cref="SiaraAuthMode.AutomatedLogin"/> parameters.</summary>
    public SiaraAutomatedOptions Automated { get; set; } = new();

    /// <summary>
    /// Gets or sets the deployment-configured service-account identity used by all acquisition modes to
    /// stamp the acquiring actor on every session for audit and non-repudiation (ADR-010 P2). Consumed by
    /// ConfiguredSiaraActorIdentityProvider.
    /// </summary>
    public SiaraActorOptions Actor { get; set; } = new();
}

/// <summary>
/// Deployment-configured identity of the service account that acquires SIARA sessions in unattended
/// modes. Consumed by ConfiguredSiaraActorIdentityProvider (ADR-010 P2).
/// </summary>
/// <remarks>
/// Set these values at deployment time (appsettings / Key Vault config provider). The ActorId is
/// recorded in audit trails — it must be a stable, recognizable identifier (for example, a service
/// principal name or a process identity string), not a credential.
/// </remarks>
public sealed class SiaraActorOptions
{
    /// <summary>
    /// Gets or sets the stable service-account identifier recorded in audit trails. Must be non-empty
    /// for actor resolution to succeed. Never a credential.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional human-readable label for the actor (for example, a display name or team
    /// name). Informational only; the canonical audit identity is ActorId.
    /// </summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// Parameters for <see cref="SiaraAuthMode.SessionPassthrough"/>: how to attach to the
/// externally-authenticated browser context and how to confirm it is authenticated.
/// </summary>
public sealed class SiaraPassthroughOptions
{
    /// <summary>
    /// Gets or sets the transport used to ride the external context. Defaults to
    /// <see cref="SiaraPassthroughTransport.StorageState"/> — the portable, headless, bank-safe option
    /// (ADR-010 §Resolved decisions); <see cref="SiaraPassthroughTransport.Cdp"/> is opt-in.
    /// </summary>
    public SiaraPassthroughTransport Transport { get; set; } = SiaraPassthroughTransport.StorageState;

    /// <summary>
    /// Gets or sets a default storage-state reference used when the acquisition request does not carry
    /// its own <see cref="Domain.ValueObjects.SiaraSessionRequest.ExistingContextEndpoint"/>. This is a
    /// bearer session secret (ADR-010 §footnote 1) — never log it in full. Optional.
    /// </summary>
    public string? StorageStateRef { get; set; }

    /// <summary>
    /// Gets or sets the SIARA URL the provider navigates to before probing authentication. Required because
    /// importing a storage-state leaves the browser on a blank page — the provider must load a real SIARA
    /// page for the <see cref="PostLoginSelector"/> probe to mean anything. Defaults to the SIARA portal.
    /// </summary>
    public string DashboardUrl { get; set; } = "https://siara.cnbv.gob.mx/";

    /// <summary>
    /// Gets or sets a selector that is present only once authenticated, used to confirm (fail-closed)
    /// that the handed-in context really is logged in. Deployment-specific to whatever SIARA presents.
    /// </summary>
    public string PostLoginSelector { get; set; } = "#dashboard";
}

/// <summary>
/// Parameters for <see cref="SiaraAuthMode.InteractiveLogin"/>: where the human logs in, how to confirm
/// the login completed, and how long to wait for them (ADR-010).
/// </summary>
/// <remarks>
/// Interactive login requires a <strong>headed</strong> browser so the human can complete whatever auth
/// SIARA presents (password, MFA, CAPTCHA). The headed launch option is an adapter/DI concern (S7); this
/// provider drives navigation and the post-login wait against that browser.
/// </remarks>
public sealed class SiaraInteractiveOptions
{
    /// <summary>
    /// Gets or sets the URL the human is taken to in order to log in on SIARA's own page. Deployment-
    /// specific; defaults to the real SIARA portal.
    /// </summary>
    public string LoginUrl { get; set; } = "https://siara.cnbv.gob.mx/";

    /// <summary>
    /// Gets or sets a selector that is present only once authenticated, used to detect that the human has
    /// completed login (fail-closed) and, later, to re-probe the session. Deployment-specific.
    /// </summary>
    public string PostLoginSelector { get; set; } = "#dashboard";

    /// <summary>
    /// Gets or sets the default time to wait for the human to complete login when the acquisition request
    /// does not carry its own <see cref="Domain.ValueObjects.SiaraSessionRequest.MaxWaitForHuman"/>.
    /// </summary>
    public TimeSpan MaxWaitForHuman { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Parameters for <see cref="SiaraAuthMode.AutomatedLogin"/>: where to find the credentials, how to drive
/// and confirm login, and the P3 lockout-safety controls (ADR-010). ⚠️ This mode is gated on legal
/// sign-off (ADR-010 P1) and must never enable a retry storm against SIARA's max-attempt lockout (P3).
/// </summary>
public sealed class SiaraAutomatedOptions
{
    /// <summary>Gets or sets the URL of the SIARA login page the unattended login navigates to.</summary>
    public string LoginUrl { get; set; } = "https://siara.cnbv.gob.mx/";

    /// <summary>
    /// Gets or sets a selector that is present only once authenticated, used to confirm (fail-closed) and
    /// re-probe the session. Deployment-specific.
    /// </summary>
    public string PostLoginSelector { get; set; } = "#dashboard";

    /// <summary>
    /// Gets or sets the configuration key the credential source reads the username from. The key names a
    /// location in the client's secret store (env var / Key Vault config provider) — not the secret itself.
    /// </summary>
    public string UsernameConfigKey { get; set; } = "Siara:Credentials:Username";

    /// <summary>Gets or sets the configuration key the credential source reads the password from.</summary>
    public string PasswordConfigKey { get; set; } = "Siara:Credentials:Password";

    /// <summary>
    /// Gets or sets how many consecutive login failures are tolerated before the circuit-breaker
    /// <strong>hard-stops</strong> and alerts a human (ADR-010 P3) — never a retry storm. Default 3.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;

    /// <summary>Gets or sets the backoff applied after the first failure; grows exponentially. Default 30s.</summary>
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the exponential growth factor applied to the backoff per failure. Default 2.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>Gets or sets the ceiling on the backoff window. Default 15 minutes.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// How <see cref="SiaraAuthMode.SessionPassthrough"/> attaches to the externally-authenticated context.
/// </summary>
public enum SiaraPassthroughTransport
{
    /// <summary>Import a captured storage-state (cookies/local-storage) into a fresh context. Default.</summary>
    StorageState = 0,

    /// <summary>Attach to a live, remote-debuggable browser over a CDP/WebSocket endpoint. Opt-in.</summary>
    Cdp = 1,
}

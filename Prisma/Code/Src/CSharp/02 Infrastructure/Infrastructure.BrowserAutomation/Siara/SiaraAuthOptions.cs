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

    /// <summary>Gets or sets the <see cref="SiaraAuthMode.SessionPassthrough"/> parameters.</summary>
    public SiaraPassthroughOptions Passthrough { get; set; } = new();

    /// <summary>Gets or sets the <see cref="SiaraAuthMode.InteractiveLogin"/> parameters.</summary>
    public SiaraInteractiveOptions Interactive { get; set; } = new();
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
/// How <see cref="SiaraAuthMode.SessionPassthrough"/> attaches to the externally-authenticated context.
/// </summary>
public enum SiaraPassthroughTransport
{
    /// <summary>Import a captured storage-state (cookies/local-storage) into a fresh context. Default.</summary>
    StorageState = 0,

    /// <summary>Attach to a live, remote-debuggable browser over a CDP/WebSocket endpoint. Opt-in.</summary>
    Cdp = 1,
}

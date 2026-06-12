namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// The P8 production-host guardrail for the SIARA login driver (ADR-010): decides whether a given target
/// URL is an allowed host to enter credentials against. The real SIARA production host is rejected
/// <strong>fail-closed</strong> unless a deployment has explicitly opted in via
/// <see cref="SiaraAuthOptions.AllowProductionHost"/> — so the fake-credential simulator and the automation
/// tooling, which never set that flag, cannot be repointed at the regulator's portal.
/// </summary>
/// <remarks>
/// <para>
/// This is a stateless, secret-free collaborator (it carries <em>no</em> credentials and reads only the
/// non-secret <see cref="SiaraAuthOptions.AllowProductionHost"/> flag), <strong>not</strong> a domain port —
/// mirroring <see cref="SiaraLoginCircuitBreaker"/>. Keeping it credential-free is what lets the login
/// driver depend on it without violating the P6 isolation (the login service must never gain raw
/// configuration access).
/// </para>
/// <para>
/// Enabling the production host is gated on written legal/compliance sign-off (ADR-010 P1); flipping
/// <see cref="SiaraAuthOptions.AllowProductionHost"/> to <c>true</c> is a deployment-time configuration
/// change, not a code change.
/// </para>
/// </remarks>
public sealed class SiaraHostPolicy
{
    /// <summary>The canonical real SIARA production host that is fail-closed by default (ADR-010 P8).</summary>
    public const string ProductionHost = "siara.cnbv.gob.mx";

    private readonly bool _allowProductionHost;
    private readonly ILogger<SiaraHostPolicy> _logger;

    /// <summary>Initializes a new instance of the <see cref="SiaraHostPolicy"/> class.</summary>
    /// <param name="options">The SIARA auth options; only the non-secret production-host flag is read.</param>
    /// <param name="logger">The logger instance.</param>
    public SiaraHostPolicy(IOptions<SiaraAuthOptions> options, ILogger<SiaraHostPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _allowProductionHost = options.Value.AllowProductionHost;
        _logger = logger;
    }

    /// <summary>
    /// Validates that credentials may be entered against the page at <paramref name="url"/>. Fails closed on
    /// the real SIARA production host (or any of its subdomains) unless the deployment opted in, and on any
    /// URL that cannot be parsed.
    /// </summary>
    /// <param name="url">The current page URL the login driver is about to authenticate against.</param>
    /// <returns>Success when the host is allowed; otherwise a failure describing why it is barred.</returns>
    public Result Validate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Result.WithFailure(
                "SIARA login host policy could not parse the target URL; refusing to enter credentials.");
        }

        if (!IsProductionHost(uri.Host))
        {
            return Result.Success();
        }

        if (_allowProductionHost)
        {
            _logger.LogWarning(
                "SIARA login is proceeding against the production host {Host} because AllowProductionHost is enabled.",
                uri.Host);
            return Result.Success();
        }

        _logger.LogError(
            "SIARA login refused against the production host {Host}: automated/credentialed access to the real " +
            "SIARA portal is disabled (AllowProductionHost is false). This is the P8 guardrail.",
            uri.Host);

        return Result.WithFailure(
            $"Refusing to enter credentials against the SIARA production host '{uri.Host}': automated access is " +
            "disabled. Enable Siara:AllowProductionHost only after legal/compliance sign-off (ADR-010 P1/P8).");
    }

    private static bool IsProductionHost(string host) =>
        string.Equals(host, ProductionHost, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + ProductionHost, StringComparison.OrdinalIgnoreCase);
}

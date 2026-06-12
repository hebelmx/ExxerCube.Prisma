using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// <see cref="SiaraAuthMode.AutomatedLogin"/> provider: drives SIARA's login form <strong>unattended</strong>
/// with credentials sourced transiently from a client secret store (<see cref="ISiaraCredentialSource"/>),
/// used once and discarded, producing a credential-free <see cref="SiaraSession"/>. Enables 24/7 operation
/// (ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <strong>Liability-gated.</strong> This is the mode whose lawfulness is gated on legal sign-off
/// (ADR-010 P1/P2/P4). Engineering preconditions honored here: the credentials are read transiently and
/// disposed immediately after the login (P5 — they are never persisted and the
/// <see cref="SiaraCredential"/> self-clears), and every attempt is gated by the
/// <see cref="SiaraLoginCircuitBreaker"/> (P3 — exponential backoff + hard stop-and-alert) so an unattended
/// loop can never become a retry storm that locks out the bank's real SIARA account.
/// </para>
/// <para>
/// Only a genuine login <em>rejection</em> (the form was submitted and SIARA refused) counts toward the
/// lockout threshold; infrastructure faults (browser launch, navigation, capture) fail closed without
/// tripping the breaker, because they never reach SIARA's auth.
/// </para>
/// </remarks>
public sealed class AutomatedLoginSiaraSessionProvider : ISiaraSessionProvider
{
    private readonly IBrowserAutomationAgent _agent;
    private readonly IBrowserSessionContext _sessionContext;
    private readonly ISiaraLoginService _loginService;
    private readonly ISiaraCredentialSource _credentialSource;
    private readonly SiaraLoginCircuitBreaker _circuitBreaker;
    private readonly SiaraAutomatedOptions _options;
    private readonly ILogger<AutomatedLoginSiaraSessionProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="AutomatedLoginSiaraSessionProvider"/> class.</summary>
    /// <param name="agent">The browser agent used to launch and navigate.</param>
    /// <param name="sessionContext">The browser session capability used to probe/capture/re-hydrate.</param>
    /// <param name="loginService">The form driver that fills and submits the login form.</param>
    /// <param name="credentialSource">The transient secret-store source for the credentials.</param>
    /// <param name="circuitBreaker">The P3 lockout-safety control gating each attempt.</param>
    /// <param name="options">The SIARA auth options (the automated section is read).</param>
    /// <param name="logger">The logger instance.</param>
    public AutomatedLoginSiaraSessionProvider(
        IBrowserAutomationAgent agent,
        IBrowserSessionContext sessionContext,
        ISiaraLoginService loginService,
        ISiaraCredentialSource credentialSource,
        SiaraLoginCircuitBreaker circuitBreaker,
        IOptions<SiaraAuthOptions> options,
        ILogger<AutomatedLoginSiaraSessionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(sessionContext);
        ArgumentNullException.ThrowIfNull(loginService);
        ArgumentNullException.ThrowIfNull(credentialSource);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = agent;
        _sessionContext = sessionContext;
        _loginService = loginService;
        _credentialSource = credentialSource;
        _circuitBreaker = circuitBreaker;
        _options = options.Value.Automated;
        _logger = logger;
    }

    /// <inheritdoc />
    public SiaraAuthMode Mode => SiaraAuthMode.AutomatedLogin;

    /// <inheritdoc />
    public async Task<Result<SiaraSession>> AcquireAsync(
        SiaraSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (request is null)
        {
            return Result<SiaraSession>.WithFailure("Session request cannot be null");
        }

        // P3: never drive the form during a backoff window or after a hard stop.
        var gate = _circuitBreaker.CheckCanAttempt();
        if (gate.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"SIARA automated login is not permitted right now: {gate.Error}");
        }

        // Source the credentials transiently from the client secret store. A missing secret is a
        // configuration fault, not a SIARA rejection, so it fails closed without tripping the breaker.
        var credentialResult = await _credentialSource.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (credentialResult.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (credentialResult.IsFailure || credentialResult.Value is null)
        {
            return Result<SiaraSession>.WithFailure($"Failed to obtain SIARA credentials: {credentialResult.Error}");
        }

        using var credential = credentialResult.Value;
        if (credential.IsEmpty)
        {
            return Result<SiaraSession>.WithFailure("Obtained SIARA credentials are empty.");
        }

        // Open the browser and the login page (infrastructure faults do not count toward lockout).
        var launch = await _agent.LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
        if (launch.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (launch.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to launch browser for automated login: {launch.Error}");
        }

        var navigate = await _agent.NavigateToAsync(_options.LoginUrl, cancellationToken).ConfigureAwait(false);
        if (navigate.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (navigate.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to open the SIARA login page: {navigate.Error}");
        }

        // Drive the form with the credentials revealed only for the duration of the login call, then they
        // are discarded by the using-scope dispose above.
        var login = await credential
            .UseAsync((username, password) => _loginService.LoginAsync(_agent, username, password, cancellationToken))
            .ConfigureAwait(false);

        if (login.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (login.IsFailure)
        {
            // A submitted-and-refused login counts toward the lockout threshold.
            _circuitBreaker.RecordFailure();
            return Result<SiaraSession>.WithFailure($"SIARA automated login was rejected: {login.Error}");
        }

        // Confirm fail-closed: a "successful" submit that did not actually authenticate is a rejection.
        var probe = await _sessionContext
            .IsAuthenticatedAsync(_options.PostLoginSelector, cancellationToken)
            .ConfigureAwait(false);

        if (probe.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (probe.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to confirm SIARA authentication: {probe.Error}");
        }

        if (!probe.Value)
        {
            _circuitBreaker.RecordFailure();
            return Result<SiaraSession>.WithFailure("SIARA automated login did not result in an authenticated session.");
        }

        // Capture a portable storage-state so the watch loop can re-hydrate headlessly.
        var export = await _sessionContext.ExportStorageStateAsync(cancellationToken).ConfigureAwait(false);
        if (export.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (export.IsFailure || string.IsNullOrWhiteSpace(export.Value))
        {
            return Result<SiaraSession>.WithFailure(
                $"Login succeeded but capturing the authenticated session failed: {export.Error}");
        }

        _circuitBreaker.RecordSuccess();

        var session = new SiaraSession
        {
            SessionId = $"automated-{Guid.NewGuid():N}",
            Mode = Mode,
            StorageStateRef = export.Value!,
            ExpiresAt = null, // unknown — valid until proven invalid by a probe
            AcquiredBy = request.RequestedBy,
        };

        _logger.LogInformation("Acquired automated-login SIARA session {SessionId}", session.SessionId);

        return Result<SiaraSession>.Success(session);
    }

    /// <inheritdoc />
    public async Task<Result<SiaraSession>> EnsureValidAsync(
        SiaraSession session,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (session is null)
        {
            return Result<SiaraSession>.WithFailure("Session cannot be null");
        }

        // Fail closed on a demonstrably expired session; the caller must request a fresh login via
        // AcquireAsync, which is itself circuit-gated (so re-auth can never storm).
        if (session.ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            return Result<SiaraSession>.WithFailure(
                "SIARA session has expired; a fresh automated login is required.");
        }

        var rehydrate = await _sessionContext
            .LoadStorageStateAsync(session.StorageStateRef, cancellationToken)
            .ConfigureAwait(false);

        if (rehydrate.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (rehydrate.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to re-hydrate SIARA session: {rehydrate.Error}");
        }

        var probe = await _sessionContext
            .IsAuthenticatedAsync(_options.PostLoginSelector, cancellationToken)
            .ConfigureAwait(false);

        if (probe.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (probe.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to re-validate SIARA session: {probe.Error}");
        }

        if (!probe.Value)
        {
            return Result<SiaraSession>.WithFailure(
                "SIARA session is no longer authenticated; a fresh automated login is required.");
        }

        return Result<SiaraSession>.Success(session);
    }

    /// <inheritdoc />
    public async Task<Result> ReleaseAsync(SiaraSession session, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (session is null)
        {
            return Result.WithFailure("Session cannot be null");
        }

        // Automated login owns the browser it launched, so release tears it down. Closing is best-effort
        // and idempotent: a failed/already-closed teardown still releases the session.
        var close = await _agent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
        if (close.IsFailure)
        {
            _logger.LogWarning(
                "Closing the browser while releasing SIARA session {SessionId} failed: {Error}",
                session.SessionId,
                close.Error);
        }

        _logger.LogInformation("Released automated-login SIARA session {SessionId}", session.SessionId);
        return Result.Success();
    }
}

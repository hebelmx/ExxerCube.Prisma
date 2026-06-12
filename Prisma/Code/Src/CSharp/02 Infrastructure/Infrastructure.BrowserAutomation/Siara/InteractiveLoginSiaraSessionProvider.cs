using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// <see cref="SiaraAuthMode.InteractiveLogin"/> provider: a human logs in <em>once</em> on SIARA's own
/// page in a headed browser, and Prisma captures the resulting authenticated context as a credential-free
/// <see cref="SiaraSession"/> the headless watch loop can keep warm (ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// Acquisition launches the (headed) browser, navigates to the configured SIARA login page, then waits up
/// to <see cref="SiaraSessionRequest.MaxWaitForHuman"/> (falling back to the configured default) for the
/// post-login selector to appear — i.e. for the human to finish whatever auth SIARA presents (password,
/// MFA, CAPTCHA). It <strong>never types credentials itself</strong>: the human does. Once the selector
/// appears, it captures a portable storage-state so the watch loop (MVP-PATH 1.2) can re-hydrate headlessly.
/// </para>
/// <para>
/// The headed launch option is an adapter/DI concern (S7); this provider only drives navigation, the
/// post-login wait, and the storage-state capture. The captured
/// <see cref="SiaraSession.StorageStateRef"/> is a bearer session secret (ADR-010 footnote 1) — never log
/// it in full. This provider carries no username/password.
/// </para>
/// </remarks>
public sealed class InteractiveLoginSiaraSessionProvider : ISiaraSessionProvider
{
    private readonly IBrowserAutomationAgent _agent;
    private readonly IBrowserSessionContext _sessionContext;
    private readonly SiaraInteractiveOptions _options;
    private readonly ILogger<InteractiveLoginSiaraSessionProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="InteractiveLoginSiaraSessionProvider"/> class.</summary>
    /// <param name="agent">The browser agent used to launch, navigate, and wait for the human login.</param>
    /// <param name="sessionContext">The browser session capability used to probe/capture/re-hydrate.</param>
    /// <param name="options">The SIARA auth options (the interactive section is read).</param>
    /// <param name="logger">The logger instance.</param>
    public InteractiveLoginSiaraSessionProvider(
        IBrowserAutomationAgent agent,
        IBrowserSessionContext sessionContext,
        IOptions<SiaraAuthOptions> options,
        ILogger<InteractiveLoginSiaraSessionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(sessionContext);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = agent;
        _sessionContext = sessionContext;
        _options = options.Value.Interactive;
        _logger = logger;
    }

    /// <inheritdoc />
    public SiaraAuthMode Mode => SiaraAuthMode.InteractiveLogin;

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

        // Open the (headed) browser so the human can log in on SIARA's own page. Never types credentials.
        var launch = await _agent.LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
        if (launch.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (launch.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to launch browser for interactive login: {launch.Error}");
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

        // Wait for the human to finish login: the post-login selector appears only once authenticated.
        var maxWait = request.MaxWaitForHuman ?? _options.MaxWaitForHuman;
        var waited = await _agent
            .WaitForSelectorAsync(_options.PostLoginSelector, (int)maxWait.TotalMilliseconds, cancellationToken)
            .ConfigureAwait(false);

        if (waited.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        // Fail closed: the human did not complete login within the allotted window.
        if (waited.IsFailure)
        {
            return Result<SiaraSession>.WithFailure(
                $"Human did not complete SIARA login within {maxWait}: {waited.Error}");
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
                $"Login completed but capturing the authenticated session failed: {export.Error}");
        }

        var session = new SiaraSession
        {
            SessionId = $"interactive-{Guid.NewGuid():N}",
            Mode = Mode,
            StorageStateRef = export.Value!,
            ExpiresAt = null, // unknown — valid until proven invalid by a probe
            AcquiredBy = request.RequestedBy,
        };

        _logger.LogInformation("Acquired interactive-login SIARA session {SessionId}", session.SessionId);

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

        // Fail closed on a demonstrably expired session; the caller must request a fresh one-time login.
        if (session.ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            return Result<SiaraSession>.WithFailure(
                "SIARA session has expired; a fresh interactive login is required.");
        }

        // Re-hydrate the captured state into a fresh context, then re-probe it.
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
                "SIARA session is no longer authenticated; a fresh interactive login is required.");
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

        // Interactive login owns the headed browser it launched, so release tears it down. Closing is
        // best-effort and idempotent: a failed/already-closed teardown still releases the session.
        var close = await _agent.CloseBrowserAsync(cancellationToken).ConfigureAwait(false);
        if (close.IsFailure)
        {
            _logger.LogWarning(
                "Closing the browser while releasing SIARA session {SessionId} failed: {Error}",
                session.SessionId,
                close.Error);
        }

        _logger.LogInformation("Released interactive-login SIARA session {SessionId}", session.SessionId);
        return Result.Success();
    }
}

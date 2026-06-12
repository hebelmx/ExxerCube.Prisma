using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// <see cref="SiaraAuthMode.SessionPassthrough"/> provider: rides a browser context that was
/// authenticated <em>outside</em> Prisma and produces a credential-free <see cref="SiaraSession"/>.
/// It <strong>never logs in</strong> (ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// Acquisition attaches to the external context per the configured
/// <see cref="SiaraPassthroughTransport"/> (import a storage-state, or attach over CDP), confirms the
/// context is actually authenticated with a post-login probe, then captures a portable storage-state so
/// the headless watch loop (MVP-PATH 1.2) can re-hydrate. Confirmation is fail-closed: an unauthenticated
/// or unverifiable context yields a failure so the system never scrapes unauthenticated.
/// </para>
/// <para>
/// The captured <see cref="SiaraSession.StorageStateRef"/> is a bearer session secret (ADR-010 footnote
/// 1), not a benign handle — never log it in full. This provider carries no username/password.
/// </para>
/// </remarks>
public sealed class SessionPassthroughSiaraSessionProvider : ISiaraSessionProvider
{
    private readonly IBrowserSessionContext _sessionContext;
    private readonly ISiaraActorIdentityProvider _actorIdentity;
    private readonly SiaraPassthroughOptions _options;
    private readonly ILogger<SessionPassthroughSiaraSessionProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionPassthroughSiaraSessionProvider"/> class.</summary>
    /// <param name="sessionContext">The browser session capability used to attach/probe/capture.</param>
    /// <param name="actorIdentity">The provider that resolves the trustworthy acquiring actor (ADR-010 P2).</param>
    /// <param name="options">The SIARA auth options (the passthrough section is read).</param>
    /// <param name="logger">The logger instance.</param>
    public SessionPassthroughSiaraSessionProvider(
        IBrowserSessionContext sessionContext,
        ISiaraActorIdentityProvider actorIdentity,
        IOptions<SiaraAuthOptions> options,
        ILogger<SessionPassthroughSiaraSessionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionContext);
        ArgumentNullException.ThrowIfNull(actorIdentity);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionContext = sessionContext;
        _actorIdentity = actorIdentity;
        _options = options.Value.Passthrough;
        _logger = logger;
    }

    /// <inheritdoc />
    public SiaraAuthMode Mode => SiaraAuthMode.SessionPassthrough;

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

        // Resolve the trustworthy acquiring actor before touching the browser (ADR-010 P2, fail-closed).
        var actor = await _actorIdentity.GetCurrentActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (actor.IsFailure)
        {
            return Result<SiaraSession>.WithFailure(
                $"Cannot acquire a SIARA session without a trustworthy actor identity: {actor.Error}");
        }

        // The external context reference comes from the request, falling back to a configured default.
        var contextRef = string.IsNullOrWhiteSpace(request.ExistingContextEndpoint)
            ? _options.StorageStateRef
            : request.ExistingContextEndpoint;

        if (string.IsNullOrWhiteSpace(contextRef))
        {
            return Result<SiaraSession>.WithFailure(
                "SessionPassthrough requires an existing-context reference (request endpoint or configured storage-state).");
        }

        // Attach to the externally-authenticated context using the configured transport. Never logs in.
        var attach = _options.Transport == SiaraPassthroughTransport.Cdp
            ? await _sessionContext.ConnectToExistingContextAsync(contextRef, cancellationToken).ConfigureAwait(false)
            : await _sessionContext.LoadStorageStateAsync(contextRef, cancellationToken).ConfigureAwait(false);

        if (attach.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (attach.IsFailure)
        {
            return Result<SiaraSession>.WithFailure($"Failed to attach to external SIARA context: {attach.Error}");
        }

        // Fail closed: confirm the handed-in context is really authenticated before we trust it.
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
            return Result<SiaraSession>.WithFailure("Handed-in SIARA context is not authenticated.");
        }

        // Capture a portable storage-state so the watch loop can re-hydrate headlessly. If capture is
        // unavailable, fall back to the imported reference (still a valid, non-empty bearer secret).
        var export = await _sessionContext.ExportStorageStateAsync(cancellationToken).ConfigureAwait(false);
        if (export.IsCancelled())
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        var storageStateRef = export.IsSuccess && !string.IsNullOrWhiteSpace(export.Value)
            ? export.Value!
            : contextRef;

        var session = new SiaraSession
        {
            SessionId = $"passthrough-{Guid.NewGuid():N}",
            Mode = Mode,
            StorageStateRef = storageStateRef,
            ExpiresAt = null, // unknown — valid until proven invalid by a probe
            AcquiredBy = actor.Value!,
        };

        _logger.LogInformation(
            "Acquired passthrough SIARA session {SessionId} via {Transport} for actor {ActorId}",
            session.SessionId,
            _options.Transport,
            actor.Value!.ActorId);

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

        // Passthrough cannot re-authenticate (the external owner does): a demonstrably expired session
        // fails closed.
        if (session.ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            return Result<SiaraSession>.WithFailure(
                "SIARA session has expired and passthrough cannot re-authenticate.");
        }

        // Re-probe the live context; fail closed if the external session has died underneath us.
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
            return Result<SiaraSession>.WithFailure("SIARA session is no longer authenticated.");
        }

        return Result<SiaraSession>.Success(session);
    }

    /// <inheritdoc />
    public Task<Result> ReleaseAsync(SiaraSession session, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled());
        }

        if (session is null)
        {
            return Task.FromResult(Result.WithFailure("Session cannot be null"));
        }

        // Passthrough does not own the external browser/context (it was handed in), so release is an
        // idempotent book-keeping no-op; the external owner tears the context down.
        _logger.LogInformation("Released passthrough SIARA session {SessionId}", session.SessionId);
        return Task.FromResult(Result.Success());
    }
}

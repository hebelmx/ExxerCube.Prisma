using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written, stateful in-memory reference fake of <see cref="ISiaraSessionProvider"/> (ADR-005 §6,
/// ADR-010): honest logic, no mocking framework, modelling the documented session lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// It tracks acquired and released sessions and issues deterministic, distinct session ids, so it
/// behaves correctly across calls. It performs no real browser work and holds no credentials, which is
/// exactly why it <strong>unblocks the downloader (MVP-PATH 1.1) and watch-loop (1.2) tests</strong>
/// without a live browser or a real SIARA. Construct one per test for isolation.
/// </para>
/// <para>
/// Expiry is honored but not auto-refreshed: an expired session fails closed
/// (<see cref="EnsureValidAsync"/> returns a failure), which the contract accepts as a valid
/// fail-closed outcome.
/// </para>
/// </remarks>
public sealed class FakeSiaraSessionProvider : ISiaraSessionProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SiaraSession> _acquired = new(StringComparer.Ordinal);
    private readonly HashSet<string> _released = new(StringComparer.Ordinal);
    private int _counter;

    /// <summary>Initializes the fake for the given auth mode.</summary>
    /// <param name="mode">The mode this provider reports. Defaults to
    /// <see cref="SiaraAuthMode.SessionPassthrough"/>.</param>
    public FakeSiaraSessionProvider(SiaraAuthMode mode = SiaraAuthMode.SessionPassthrough) => Mode = mode;

    /// <inheritdoc />
    public SiaraAuthMode Mode { get; }

    /// <summary>Gets the number of sessions acquired from this fake so far.</summary>
    public int AcquiredCount
    {
        get { lock (_gate) { return _acquired.Count; } }
    }

    /// <summary>Returns whether the session with the given id has been released.</summary>
    /// <param name="sessionId">The session correlation id.</param>
    /// <returns><see langword="true"/> if released; otherwise <see langword="false"/>.</returns>
    public bool IsReleased(string sessionId)
    {
        lock (_gate) { return _released.Contains(sessionId); }
    }

    /// <inheritdoc />
    public Task<Result<SiaraSession>> AcquireAsync(
        SiaraSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<SiaraSession>());
        }

        if (request is null)
        {
            return Task.FromResult(Result<SiaraSession>.WithFailure("Session request cannot be null"));
        }

        SiaraSession session;
        lock (_gate)
        {
            var n = ++_counter;
            session = new SiaraSession
            {
                SessionId = $"fake-session-{n}",
                Mode = Mode,
                StorageStateRef = $"fake-storage-state-{n}",
                ExpiresAt = null, // valid until proven invalid
                AcquiredBy = new SiaraActor
                {
                    ActorId = request.RequestedBy ?? "fake-actor",
                    ActorType = SiaraActorType.ServiceAccount,
                },
            };
            _acquired[session.SessionId] = session;
        }

        return Task.FromResult(Result<SiaraSession>.Success(session));
    }

    /// <inheritdoc />
    public Task<Result<SiaraSession>> EnsureValidAsync(
        SiaraSession session,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<SiaraSession>());
        }

        if (session is null)
        {
            return Task.FromResult(Result<SiaraSession>.WithFailure("Session cannot be null"));
        }

        // Fail closed on a demonstrably expired session (the fake does not re-authenticate).
        if (session.ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            return Task.FromResult(Result<SiaraSession>.WithFailure("Session has expired"));
        }

        lock (_gate)
        {
            if (_released.Contains(session.SessionId))
            {
                return Task.FromResult(Result<SiaraSession>.WithFailure("Session has been released"));
            }
        }

        return Task.FromResult(Result<SiaraSession>.Success(session));
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

        lock (_gate)
        {
            // Idempotent: marking an already-released session is a no-op success.
            _released.Add(session.SessionId);
        }

        return Task.FromResult(Result.Success());
    }
}

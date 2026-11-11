namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Strategy port that obtains and maintains a credential-free authenticated SIARA session.
/// </summary>
/// <remarks>
/// <para>
/// One port, two configuration-selected implementations (one per <see cref="SiaraAuthMode"/>);
/// a fail-closed <see cref="ISiaraSessionProviderResolver"/> picks the configured one. This is the
/// auth seam the real SIARA downloader (MVP-PATH 1.1) and its watch loop (1.2) consume. See ADR-010.
/// </para>
/// <para>
/// <strong>Contract:</strong> all methods return <see cref="Result"/>/<see cref="Result{T}"/> and
/// never throw for business outcomes; a pre-cancelled token yields a cancelled result; authentication
/// failures fail closed (a failure result, never silent success). No method accepts or persists raw
/// credentials.
/// </para>
/// </remarks>
public interface ISiaraSessionProvider
{
    /// <summary>Gets the authentication mode this provider implements (used by the resolver to honor config).</summary>
    SiaraAuthMode Mode { get; }

    /// <summary>
    /// Obtains an authenticated SIARA session for the given request. Never stores raw credentials.
    /// </summary>
    /// <param name="request">The acquisition request (fields read depend on the mode).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success result wrapping the acquired <see cref="SiaraSession"/>, or a failure.</returns>
    Task<Result<SiaraSession>> AcquireAsync(
        SiaraSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates, keeps warm, or refreshes a session so the headless watch loop can keep running.
    /// Fails closed when the session has expired and cannot be refreshed.
    /// </summary>
    /// <param name="session">The session to validate or refresh.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success result wrapping a still-valid (possibly refreshed) session, or a failure.</returns>
    Task<Result<SiaraSession>> EnsureValidAsync(
        SiaraSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the session and any browser context it owns. Idempotent: releasing an already-released
    /// session is not an error.
    /// </summary>
    /// <param name="session">The session to release.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success result, or a failure (for example, a null session).</returns>
    Task<Result> ReleaseAsync(
        SiaraSession session,
        CancellationToken cancellationToken = default);
}

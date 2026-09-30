namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Focused browser-session capability: capture, restore, and attach to an authenticated browser
/// context, plus probe authentication state. Companion to <see cref="IBrowserAutomationAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// Kept separate from <see cref="IBrowserAutomationAgent"/> (which is the navigation/discovery concern)
/// so the security-sensitive session-handling surface stays small and cohesive (ADR-010 §7). The
/// Playwright adapter implements both ports over the same live browser.
/// </para>
/// <para>
/// This is the net-new infrastructure capability the SIARA session providers depend on
/// (SIARA-AUTH-DESIGN S4): <see cref="SiaraAuthMode.SessionPassthrough"/> attaches via
/// <see cref="ConnectToExistingContextAsync"/>, while <see cref="SiaraAuthMode.InteractiveLogin"/>
/// captures via <see cref="ExportStorageStateAsync"/> and re-hydrates via
/// <see cref="LoadStorageStateAsync"/>. The exported reference is an <strong>opaque secret</strong>
/// (a storage-state handle, never raw credentials) and must never be logged in full.
/// </para>
/// <para>
/// <strong>Contract:</strong> all methods return <see cref="Result"/>/<see cref="Result{T}"/> and never
/// throw for business outcomes; a pre-cancelled token yields a cancelled result; null/empty inputs and
/// the absence of an established session fail closed.
/// </para>
/// </remarks>
public interface IBrowserSessionContext
{
    /// <summary>
    /// Captures the current authenticated browser context as an opaque storage-state reference.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping the opaque storage-state reference, or a failure when no session is
    /// established or the capture fails.
    /// </returns>
    Task<Result<string>> ExportStorageStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a previously-captured storage-state into a fresh browser context so subsequent
    /// navigation runs authenticated.
    /// </summary>
    /// <param name="storageStateRef">The opaque storage-state reference produced by
    /// <see cref="ExportStorageStateAsync"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success result, or a failure (for example, a null/empty reference or no browser).</returns>
    Task<Result> LoadStorageStateAsync(string storageStateRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches to an externally-provided, already-authenticated browser context (session passthrough),
    /// for example via a CDP/WebSocket endpoint.
    /// </summary>
    /// <param name="endpoint">The connect endpoint for the existing context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success result, or a failure (for example, a null/empty endpoint or a failed attach).</returns>
    Task<Result> ConnectToExistingContextAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Probes whether the current context is authenticated by checking for a known post-login element.
    /// </summary>
    /// <param name="postLoginSelector">A selector that is only present once authenticated.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping <see langword="true"/> when the selector is present, otherwise
    /// <see langword="false"/>; a failure for a null/empty selector, no session, or a probe error.
    /// </returns>
    Task<Result<bool>> IsAuthenticatedAsync(string postLoginSelector, CancellationToken cancellationToken = default);
}

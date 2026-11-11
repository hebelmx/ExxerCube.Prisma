namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Yields the trustworthy actor identity that will be recorded on an acquired SIARA session for
/// per-document non-repudiation (ADR-010 P2, ADR-010 audit trail Consequences).
/// </summary>
/// <remarks>
/// <para>
/// This port decouples the three ISiaraSessionProvider strategies from the source of actor identity.
/// The production implementation reads a deployment-configured service-account identity from options;
/// a future implementation can source the identity from an authenticated principal (e.g., the operator
/// who triggered the acquisition). The providers call this before touching the browser or credentials,
/// and fail closed if no trustworthy actor can be resolved.
/// </para>
/// <para>
/// <strong>Contract:</strong> returns Result and never throws for business outcomes; a pre-cancelled
/// token yields a cancelled result; a missing or misconfigured identity fails closed (the session must
/// not be acquired anonymously). Each resolution should be audited by the implementation.
/// </para>
/// </remarks>
public interface ISiaraActorIdentityProvider
{
    /// <summary>
    /// Resolves the trustworthy actor identity for the current acquisition context.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping the resolved ValueObjects.SiaraActor, or a failure result when no
    /// trustworthy actor can be determined (for example, when the service account is not configured).
    /// </returns>
    Task<Result<ValueObjects.SiaraActor>> GetCurrentActorAsync(CancellationToken cancellationToken = default);
}

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Provides access to the current user's identity information.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Retrieves the current authenticated user's identity asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The current user's identity, or null if not authenticated.</returns>
    Task<UserIdentity?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
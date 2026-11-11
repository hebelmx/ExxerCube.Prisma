namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Provides token generation and validation services for authentication.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates an authentication token for the specified user identity.
    /// </summary>
    /// <param name="identity">The user identity to create a token for.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An authentication token as a string.</returns>
    Task<string> CreateTokenAsync(UserIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an authentication token and extracts the user identity.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result indicating whether the token is valid and the associated identity.</returns>
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}
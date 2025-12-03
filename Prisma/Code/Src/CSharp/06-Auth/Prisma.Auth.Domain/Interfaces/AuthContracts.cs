using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Prisma.Auth.Domain.Interfaces;

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

/// <summary>
/// Provides synchronous access to the current user context.
/// </summary>
public interface IUserContextAccessor
{
    /// <summary>
    /// Gets the current user's identity synchronously.
    /// </summary>
    UserIdentity? Current { get; }
}

/// <summary>
/// Represents a user's identity with ID, username, and roles.
/// </summary>
/// <param name="UserId">The unique identifier for the user.</param>
/// <param name="UserName">The user's display name or username.</param>
/// <param name="Roles">A read-only collection of role names assigned to the user.</param>
public sealed record UserIdentity(string UserId, string UserName, IReadOnlyCollection<string> Roles);

/// <summary>
/// Represents the result of a token validation operation.
/// </summary>
/// <param name="IsValid">Indicates whether the token is valid.</param>
/// <param name="Identity">The user identity extracted from the token, if valid.</param>
/// <param name="Error">An optional error message if validation failed.</param>
public sealed record TokenValidationResult(bool IsValid, UserIdentity? Identity, string? Error = null);

namespace Prisma.Auth.Infrastructure;

/// <summary>
/// Configuration for EF Core Identity JWT token generation.
/// </summary>
public sealed record EfCoreIdentityConfiguration
{
    /// <summary>
    /// JWT secret key for signing tokens (must be at least 32 characters).
    /// </summary>
    public required string JwtSecret { get; init; }

    /// <summary>
    /// JWT token issuer (typically application name).
    /// </summary>
    public required string JwtIssuer { get; init; }

    /// <summary>
    /// JWT token audience (typically application users).
    /// </summary>
    public required string JwtAudience { get; init; }

    /// <summary>
    /// JWT token lifetime (default: 1 hour).
    /// </summary>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(1);
}

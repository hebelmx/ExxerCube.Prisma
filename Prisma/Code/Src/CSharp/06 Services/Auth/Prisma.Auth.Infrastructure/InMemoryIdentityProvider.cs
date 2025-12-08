using Prisma.Auth.Domain.Interfaces;

namespace Prisma.Auth.Infrastructure;

/// <summary>
/// Minimal in-memory identity provider for development/testing; replaceable with real IdP.
/// </summary>
public class InMemoryIdentityProvider : IIdentityProvider, ITokenService, IUserContextAccessor
{
    private UserIdentity? _current;

    /// <inheritdoc />
    public UserIdentity? Current => _current;

    /// <inheritdoc />
    public Task<UserIdentity?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_current);
    }

    /// <inheritdoc />
    public Task<string> CreateTokenAsync(UserIdentity identity, CancellationToken cancellationToken = default)
    {
        _current = identity;
        return Task.FromResult($"DEV-TOKEN-{identity.UserId}");
    }

    /// <inheritdoc />
    public Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (_current is null || token != $"DEV-TOKEN-{_current.UserId}")
        {
            return Task.FromResult(new TokenValidationResult(false, null, "Invalid token"));
        }

        return Task.FromResult(new TokenValidationResult(true, _current));
    }
}

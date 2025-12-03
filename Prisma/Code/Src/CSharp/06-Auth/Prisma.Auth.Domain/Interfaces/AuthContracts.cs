using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Prisma.Auth.Domain.Interfaces;

public interface IIdentityProvider
{
    Task<UserIdentity?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    Task<string> CreateTokenAsync(UserIdentity identity, CancellationToken cancellationToken = default);
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface IUserContextAccessor
{
    UserIdentity? Current { get; }
}

public sealed record UserIdentity(string UserId, string UserName, IReadOnlyCollection<string> Roles);

public sealed record TokenValidationResult(bool IsValid, UserIdentity? Identity, string? Error = null);

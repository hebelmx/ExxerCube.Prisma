using Microsoft.AspNetCore.Http;

namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// Concrete implementation of <see cref="IIdentityProvider"/> and <see cref="IUserContextAccessor"/>
/// backed by ASP.NET Core Identity and the current <see cref="IHttpContextAccessor"/>.
/// </summary>
/// <remarks>
/// Registered as <c>Scoped</c> via <see cref="PrismaIdentityExtensions.AddPrismaIdentity"/>.
/// The Web.UI cookie authentication scheme populates <c>HttpContext.User</c> before this
/// adapter reads it, so no JWT parsing is required in the cookie path.
/// </remarks>
public sealed class PrismaIdentityAdapter : IIdentityProvider, IUserContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PrismaIdentityAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PrismaIdentityAdapter"/>.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    /// <param name="logger">Logger instance.</param>
    public PrismaIdentityAdapter(
        IHttpContextAccessor httpContextAccessor,
        ILogger<PrismaIdentityAdapter> logger)
    {
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>null</c> when there is no active HTTP context or the user is not authenticated.
    /// Never throws; null is a valid return value per the interface contract.
    /// </remarks>
    public UserIdentity? Current
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            return ExtractIdentity(principal);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Async-reads roles from the claims principal already set by the cookie middleware.
    /// Does not hit the database; role claims are embedded in the cookie by
    /// <see cref="SignInManager{TUser}"/> at login time.
    /// Business errors are returned as <c>null</c> (not authenticated), never thrown.
    /// </remarks>
    public Task<UserIdentity?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("GetCurrentAsync cancelled");
            return Task.FromResult<UserIdentity?>(null);
        }

        var principal = _httpContextAccessor.HttpContext?.User;
        var identity = ExtractIdentity(principal);
        return Task.FromResult(identity);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static UserIdentity? ExtractIdentity(System.Security.Claims.ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = principal.FindFirstValue(ClaimTypes.Name)
                       ?? principal.FindFirstValue(ClaimTypes.Email);
        var roles = principal.FindAll(ClaimTypes.Role)
                             .Select(c => c.Value)
                             .ToArray();

        if (userId is null || userName is null)
            return null;

        return new UserIdentity(userId, userName, roles);
    }
}

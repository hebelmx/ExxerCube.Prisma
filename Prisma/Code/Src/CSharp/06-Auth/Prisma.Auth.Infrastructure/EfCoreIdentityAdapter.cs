using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Prisma.Auth.Domain.Interfaces;

namespace Prisma.Auth.Infrastructure;

/// <summary>
/// Adapter wrapping EF Core Identity (UserManager/SignInManager) to implement clean auth interfaces.
/// </summary>
/// <remarks>
/// This adapter separates the UI layer from concrete EF Identity implementation,
/// enabling swappable auth providers (in-memory, EF, Azure AD, etc.) with proper SRP.
/// </remarks>
/// <typeparam name="TUser">The Identity user type (e.g., ApplicationUser).</typeparam>
public sealed class EfCoreIdentityAdapter<TUser> : IIdentityProvider, ITokenService, IUserContextAccessor
    where TUser : IdentityUser
{
    private readonly UserManager<TUser> _userManager;
    private readonly SignInManager<TUser> _signInManager;
    private readonly EfCoreIdentityConfiguration _config;
    private readonly ILogger<EfCoreIdentityAdapter<TUser>> _logger;
    private HttpContext? _httpContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreIdentityAdapter{TUser}"/> class.
    /// </summary>
    public EfCoreIdentityAdapter(
        UserManager<TUser> userManager,
        SignInManager<TUser> signInManager,
        EfCoreIdentityConfiguration config,
        ILogger<EfCoreIdentityAdapter<TUser>> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the HTTP context for testing purposes (internal use only).
    /// </summary>
    internal void SetHttpContext(HttpContext context) => _httpContext = context;

    /// <inheritdoc />
    public UserIdentity? Current
    {
        get
        {
            var principal = _httpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = principal.FindFirstValue(ClaimTypes.Name);
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

            return userId != null && userName != null
                ? new UserIdentity(userId, userName, roles)
                : null;
        }
    }

    /// <inheritdoc />
    public async Task<UserIdentity?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserIdentity(user.Id, user.UserName!, roles.ToArray());
    }

    /// <inheritdoc />
    public async Task<string> CreateTokenAsync(UserIdentity identity, CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId),
            new(ClaimTypes.Name, identity.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(identity.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config.JwtIssuer,
            audience: _config.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_config.TokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config.JwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _config.JwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = principal.FindFirstValue(ClaimTypes.Name);
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

            if (userId == null || userName == null)
                return Task.FromResult(new TokenValidationResult(false, null, "Missing required claims"));

            var identity = new UserIdentity(userId, userName, roles);
            return Task.FromResult(new TokenValidationResult(true, identity));
        }
        catch (SecurityTokenExpiredException)
        {
            return Task.FromResult(new TokenValidationResult(false, null, "Token has expired"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return Task.FromResult(new TokenValidationResult(false, null, "Invalid token"));
        }
    }
}

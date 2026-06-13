using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;

/// <summary>
/// HMAC-SHA256 JWT implementation of <see cref="IProcessClearanceTokenService"/> (MVP-PATH 1.5, A5 DoD).
/// Modelled on <c>EfCoreIdentityAdapter.CreateTokenAsync / ValidateTokenAsync</c> — same symmetric-key
/// algorithm, same config structure — without pulling in <c>UserManager</c> or the Identity DB.
/// </summary>
/// <remarks>
/// <para>
/// <strong>MintAsync:</strong> builds a JWT with the claims listed in the design (§3): <c>sub</c>,
/// <c>actor_type</c>, <c>clearance</c>, <c>file_id</c>, <c>jti</c>, <c>iss</c>, <c>aud</c>, <c>exp</c>.
/// Signing uses HMAC-SHA256 over <see cref="ProcessIdentityOptions.JwtSecret"/>.
/// </para>
/// <para>
/// <strong>ValidateAsync:</strong> uses <see cref="JwtSecurityTokenHandler"/> with
/// <c>ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true</c> and a <c>ClockSkew</c> of 30 seconds
/// to tolerate minor NTP drift between containers (design R5). Fails closed on any error — exceptions are
/// caught and converted to a failure result; the caller never sees an exception.
/// </para>
/// <para>
/// Registered as a singleton: the service is stateless (reads options once, mints/validates per call).
/// </para>
/// </remarks>
public sealed class JwtProcessClearanceTokenService : IProcessClearanceTokenService
{
    private const string ClaimActorType = "actor_type";
    private const string ClaimClearance = "clearance";
    private const string ClaimFileId = "file_id";

    private readonly ProcessIdentityOptions _options;
    private readonly ILogger<JwtProcessClearanceTokenService> _logger;

    /// <summary>Initializes a new instance of the <see cref="JwtProcessClearanceTokenService"/> class.</summary>
    /// <param name="options">The process identity configuration (JWT secret, issuer, audience, lifetime).</param>
    /// <param name="logger">The logger instance.</param>
    public JwtProcessClearanceTokenService(
        IOptions<ProcessIdentityOptions> options,
        ILogger<JwtProcessClearanceTokenService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<string>> MintAsync(
        SiaraActor actor,
        ProcessClearance clearance,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }

        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(_options.JwtSecret))
        {
            return Task.FromResult(
                Result<string>.WithFailure(
                    "ProcessIdentity:JwtSecret is not configured. Set it via an environment variable or Key Vault config provider."));
        }

        try
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, actor.ActorId),
                new(ClaimActorType, actor.ActorType.ToString()),
                new(ClaimClearance, clearance.ToString()),
                new(ClaimFileId, fileId.ToString("D")),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.JwtIssuer,
                audience: _options.JwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_options.TokenLifetime),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogDebug(
                "Minted clearance token for actor {ActorId} clearance {Clearance} file {FileId}",
                actor.ActorId,
                clearance,
                fileId);

            return Task.FromResult(Result<string>.Success(tokenString));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mint clearance token for actor {ActorId}", actor.ActorId);
            return Task.FromResult(Result<string>.WithFailure($"Failed to mint clearance token: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<Result<ClearanceTokenClaims>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<ClearanceTokenClaims>());
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(
                Result<ClearanceTokenClaims>.WithFailure("Clearance token is null or empty."));
        }

        if (string.IsNullOrWhiteSpace(_options.JwtSecret))
        {
            return Task.FromResult(
                Result<ClearanceTokenClaims>.WithFailure(
                    "ProcessIdentity:JwtSecret is not configured — token validation cannot proceed."));
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            // Clear the default inbound claim-type mapping so the token's raw claim names
            // (e.g. "sub", "jti") are preserved as-is and FindFirst(JwtRegisteredClaimNames.Sub) works.
            tokenHandler.InboundClaimTypeMap.Clear();

            var key = Encoding.UTF8.GetBytes(_options.JwtSecret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _options.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _options.JwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30), // R5: tolerate minor NTP drift between containers
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            var actorId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var actorTypeStr = principal.FindFirst(ClaimActorType)?.Value;
            var clearanceStr = principal.FindFirst(ClaimClearance)?.Value;
            var fileIdStr = principal.FindFirst(ClaimFileId)?.Value;

            if (string.IsNullOrWhiteSpace(actorId)
                || string.IsNullOrWhiteSpace(actorTypeStr)
                || string.IsNullOrWhiteSpace(clearanceStr)
                || string.IsNullOrWhiteSpace(fileIdStr))
            {
                return Task.FromResult(
                    Result<ClearanceTokenClaims>.WithFailure("Clearance token is missing one or more required claims."));
            }

            if (!Enum.TryParse<SiaraActorType>(actorTypeStr, ignoreCase: true, out var actorType))
            {
                return Task.FromResult(
                    Result<ClearanceTokenClaims>.WithFailure($"Unrecognised actor_type claim value: '{actorTypeStr}'."));
            }

            if (!Enum.TryParse<ProcessClearance>(clearanceStr, ignoreCase: true, out var clearance))
            {
                return Task.FromResult(
                    Result<ClearanceTokenClaims>.WithFailure($"Unrecognised clearance claim value: '{clearanceStr}'."));
            }

            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                return Task.FromResult(
                    Result<ClearanceTokenClaims>.WithFailure($"Clearance token file_id claim is not a valid GUID: '{fileIdStr}'."));
            }

            var claims = new ClearanceTokenClaims
            {
                ActorId = actorId,
                ActorType = actorType,
                Clearance = clearance,
                FileId = fileId,
            };

            return Task.FromResult(Result<ClearanceTokenClaims>.Success(claims));
        }
        catch (SecurityTokenExpiredException)
        {
            return Task.FromResult(
                Result<ClearanceTokenClaims>.WithFailure("Clearance token has expired."));
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Clearance token validation failed (security token error)");
            return Task.FromResult(
                Result<ClearanceTokenClaims>.WithFailure($"Clearance token validation failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clearance token validation failed (unexpected error)");
            return Task.FromResult(
                Result<ClearanceTokenClaims>.WithFailure($"Clearance token validation failed: {ex.Message}"));
        }
    }
}

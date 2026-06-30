using System.Text;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExxerCube.Prisma.Veriqan.Worker;

/// <summary>
/// <see cref="IPostConfigureOptions{TOptions}"/> that populates
/// <see cref="TokenValidationParameters.IssuerSigningKey"/> on <see cref="JwtBearerOptions"/>
/// from the DI-registered <see cref="ISecretProvider"/> after the container is built.
/// </summary>
/// <remarks>
/// <para>
/// Resolving the JWT signing key through <see cref="ISecretProvider"/> (rather than reading
/// it directly from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>) means
/// that a custom secrets-manager adapter registered in DI before <c>AddVeriqan</c> is
/// automatically consulted for the JWT key too — the same seam covers AES, SMTP, and JWT.
/// </para>
/// <para>
/// <b>Fail-closed:</b> when the secret is absent or is still a placeholder, the
/// <see cref="TokenValidationParameters.IssuerSigningKey"/> is left <see langword="null"/>
/// and <see cref="TokenValidationParameters.ValidateIssuerSigningKey"/> remains
/// <see langword="true"/>.  The JWT bearer handler has no key to verify against, so every
/// token is rejected with 401 — the verification gate stays closed.  A loud warning is
/// logged so operators are alerted immediately on the first authenticated request.
/// </para>
/// <para>
/// Runs at the same path as <c>SmtpPasswordSecretPopulator</c> in the Orchestration
/// layer, but lives in the Worker because <c>JwtBearerOptions</c> is in
/// <c>Microsoft.AspNetCore.Authentication.JwtBearer</c>, which is only referenced by
/// the Worker (not by Orchestration).
/// </para>
/// </remarks>
internal sealed class JwtSigningKeySecretPostConfigure : IPostConfigureOptions<JwtBearerOptions>
{
    /// <summary>The logical name used to look up the JWT signing key in the secret provider.</summary>
    public const string SecretLogicalName = "Veriqan:Auth:Jwt:SigningKey";

    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<JwtSigningKeySecretPostConfigure> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="JwtSigningKeySecretPostConfigure"/>.
    /// </summary>
    /// <param name="secretProvider">Secret provider to consult for the JWT signing key.</param>
    /// <param name="logger">Logger for security warnings when the key is absent.</param>
    public JwtSigningKeySecretPostConfigure(
        ISecretProvider secretProvider,
        ILogger<JwtSigningKeySecretPostConfigure> logger)
    {
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Resolves the JWT signing key via <see cref="ISecretProvider"/> and sets
    /// <see cref="TokenValidationParameters.IssuerSigningKey"/> on the supplied
    /// <paramref name="options"/>.  When the secret is absent or is a placeholder the
    /// key is left <see langword="null"/> (fail-closed) and a security warning is logged.
    /// </remarks>
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Call async method synchronously — runs once per options-resolution (lazy singleton).
        // Blocking is acceptable in this startup path.
        var result = _secretProvider.GetSecretAsync(SecretLogicalName)
            .GetAwaiter().GetResult();

        var signingKey = result.IsSuccess ? result.Value?.Value : null;

        if (string.IsNullOrWhiteSpace(signingKey) ||
            signingKey.StartsWith("<REPLACE", StringComparison.Ordinal))
        {
            // Loud startup warning: every request to a protected endpoint will be rejected
            // with 401 until a real key is supplied.  This is intentional fail-closed behaviour.
            _logger.LogWarning(
                "SECURITY WARNING: {ConfigKey} is absent or contains a placeholder value. " +
                "JWT bearer validation will reject all tokens until a real key is supplied. " +
                "Set the key via an environment variable (Veriqan__Auth__Jwt__SigningKey) or Key Vault.",
                SecretLogicalName);

            // Leave IssuerSigningKey null — ValidateIssuerSigningKey = true (set in AddJwtBearer)
            // ensures the handler rejects every token when no key is available.
            return;
        }

        options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }
}

using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// Reads SIARA credentials from the client's configuration-backed secret store (env vars / Key Vault
/// configuration provider) at the moment of login, for <see cref="Domain.Enum.SiaraAuthMode.AutomatedLogin"/>
/// (ADR-010). Prisma never persists them in a strongly-typed field; they live only inside the returned,
/// self-clearing <see cref="SiaraCredential"/>.
/// </summary>
/// <remarks>
/// <para>
/// The credential values are read from <see cref="IConfiguration"/> by the configured keys
/// (<see cref="SiaraAutomatedOptions.UsernameConfigKey"/> / <see cref="SiaraAutomatedOptions.PasswordConfigKey"/>)
/// on each call — not bound into a long-lived options object — so the only place the secret resides is the
/// client's own configuration source. A missing/empty value fails closed.
/// </para>
/// <para>
/// Each read is audited (ADR-010 Consequences) — the log records that a read occurred and which keys were
/// used, never the values (P5). The vault-native source is a future swap behind this same port.
/// </para>
/// </remarks>
public sealed class ConfiguredSiaraCredentialSource : ISiaraCredentialSource
{
    private readonly IConfiguration _configuration;
    private readonly SiaraAutomatedOptions _options;
    private readonly ILogger<ConfiguredSiaraCredentialSource> _logger;

    /// <summary>Initializes a new instance of the <see cref="ConfiguredSiaraCredentialSource"/> class.</summary>
    /// <param name="configuration">The configuration-backed secret store.</param>
    /// <param name="options">The SIARA auth options (the automated section names the keys to read).</param>
    /// <param name="logger">The logger instance.</param>
    public ConfiguredSiaraCredentialSource(
        IConfiguration configuration,
        IOptions<SiaraAuthOptions> options,
        ILogger<ConfiguredSiaraCredentialSource> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _options = options.Value.Automated;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<SiaraCredential>> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<SiaraCredential>());
        }

        var username = _configuration[_options.UsernameConfigKey];
        var password = _configuration[_options.PasswordConfigKey];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            // Audit the failed read without revealing anything; fail closed.
            _logger.LogError(
                "SIARA credential read failed: no value at configured keys {UsernameKey}/{PasswordKey}.",
                _options.UsernameConfigKey,
                _options.PasswordConfigKey);
            return Task.FromResult(
                Result<SiaraCredential>.WithFailure("SIARA credentials are not configured in the secret store."));
        }

        // Audit the successful read (keys only, never values) per ADR-010.
        _logger.LogInformation(
            "SIARA credential read from configured key {UsernameKey} for automated login.",
            _options.UsernameConfigKey);

        var credential = new SiaraCredential(username.ToCharArray(), password.ToCharArray());
        return Task.FromResult(Result<SiaraCredential>.Success(credential));
    }
}

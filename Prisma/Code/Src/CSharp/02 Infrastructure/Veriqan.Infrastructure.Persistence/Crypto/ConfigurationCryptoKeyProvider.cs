using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;

/// <summary>
/// Resolves the AES-256 key for the legal-baseline store via <see cref="ISecretProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The logical secret name is <see cref="ConfigKeyName"/> (<c>Veriqan:LegalBaseline:EncryptionKey</c>).
/// The default <c>ConfigurationSecretProvider</c> reads this from application configuration
/// (appsettings.json, environment variables, etc.), so existing deployments are unchanged.
/// A future Key Vault implementation transparently routes the same logical name to the vault.
/// </para>
/// <para>
/// The secret value must be a Base64-encoded string representing exactly 32 bytes (AES-256).
/// </para>
/// <para>
/// For tests, supply a fixed Base64 test key via an <see cref="ISecretProvider"/> stub that
/// returns a <c>SecretValue</c> containing <c>AAAA...==</c> (32 zero-bytes in Base64) for the
/// <see cref="ConfigKeyName"/> logical name.
/// </para>
/// </remarks>
internal sealed class ConfigurationCryptoKeyProvider : ILegalBaselineCryptoKeyProvider
{
    /// <summary>Logical secret name / configuration key for the Base64-encoded AES-256 encryption key.</summary>
    public const string ConfigKeyName = "Veriqan:LegalBaseline:EncryptionKey";

    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationCryptoKeyProvider"/> by resolving
    /// the AES key from the supplied <see cref="ISecretProvider"/>.
    /// </summary>
    /// <param name="secretProvider">
    /// Secret provider used to look up <see cref="ConfigKeyName"/>. The
    /// <c>ConfigurationSecretProvider</c> default reads from <c>IConfiguration</c>;
    /// a Key Vault adapter will read from the vault instead.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the secret is absent, the value is not valid Base64, or the decoded byte
    /// array is not exactly 32 bytes.  This is a startup-time fail-loud guard — the host must
    /// never boot without a valid AES key.
    /// </exception>
    public ConfigurationCryptoKeyProvider(ISecretProvider secretProvider)
    {
        ArgumentNullException.ThrowIfNull(secretProvider);

        // Block once at startup (singleton construction). Blocking is acceptable here:
        // this runs exactly once during DI resolution, before traffic is served.
        var secretResult = secretProvider.GetSecretAsync(ConfigKeyName)
            .GetAwaiter().GetResult();

        if (!secretResult.IsSuccess)
            throw new InvalidOperationException(
                $"Secret '{ConfigKeyName}' could not be retrieved: {secretResult.Error}. " +
                "Supply a Base64-encoded 32-byte AES-256 key via configuration or your secrets provider.");

        var base64 = secretResult.Value?.Value;
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                $"Secret '{ConfigKeyName}' is missing or empty. " +
                "Supply a Base64-encoded 32-byte AES-256 key.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Secret '{ConfigKeyName}' is not valid Base64.", ex);
        }

        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                $"Secret '{ConfigKeyName}' decoded to {keyBytes.Length} bytes. " +
                "AES-256 requires exactly 32 bytes.");

        _key = keyBytes;
    }

    /// <inheritdoc />
    public byte[] GetKey() => _key;
}

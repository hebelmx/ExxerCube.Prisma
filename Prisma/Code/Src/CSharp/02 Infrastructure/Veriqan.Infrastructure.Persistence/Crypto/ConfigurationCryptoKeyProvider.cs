using System;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;

/// <summary>
/// Reads the AES-256 key for the legal-baseline store from application configuration.
/// </summary>
/// <remarks>
/// <para>
/// Configuration key: <c>Veriqan:LegalBaseline:EncryptionKey</c>.
/// The value must be a Base64-encoded string representing exactly 32 bytes.
/// </para>
/// <para>
/// For production, set this key in environment variables or Azure Key Vault — not in
/// <c>appsettings.json</c>. For tests, use an in-memory configuration builder with a
/// fixed test key (e.g. 32 zero-bytes encoded as Base64).
/// </para>
/// </remarks>
internal sealed class ConfigurationCryptoKeyProvider : ILegalBaselineCryptoKeyProvider
{
    /// <summary>Configuration key name for the Base64-encoded AES-256 encryption key.</summary>
    public const string ConfigKeyName = "Veriqan:LegalBaseline:EncryptionKey";

    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationCryptoKeyProvider"/>.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration key is missing or the decoded value is not 32 bytes.
    /// </exception>
    public ConfigurationCryptoKeyProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var base64 = configuration[ConfigKeyName];
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                $"Configuration key '{ConfigKeyName}' is missing or empty. " +
                "Supply a Base64-encoded 32-byte AES-256 key.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Configuration key '{ConfigKeyName}' is not valid Base64.", ex);
        }

        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                $"Configuration key '{ConfigKeyName}' decoded to {keyBytes.Length} bytes. " +
                "AES-256 requires exactly 32 bytes.");

        _key = keyBytes;
    }

    /// <inheritdoc />
    public byte[] GetKey() => _key;
}

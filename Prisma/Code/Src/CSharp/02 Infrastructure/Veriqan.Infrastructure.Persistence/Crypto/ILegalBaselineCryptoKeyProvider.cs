namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;

/// <summary>
/// Provides the AES-256 encryption key used to protect legal-baseline tolerance values
/// at rest in the <c>veriqan.LegalBaselineTolerances</c> table.
/// </summary>
/// <remarks>
/// <para>
/// The key is a 32-byte (256-bit) secret read from application configuration under the
/// key <c>"Veriqan:LegalBaseline:EncryptionKey"</c>, which must be a Base64-encoded string.
/// <b>Never hard-code the key in source.</b> For tests, supply a fixed Base64 test key
/// via <c>IConfiguration</c> or the options object.
/// </para>
/// <para>
/// In production, the key should be stored in Azure Key Vault, environment variables, or
/// a secrets manager — never in <c>appsettings.json</c> committed to source control.
/// </para>
/// </remarks>
public interface ILegalBaselineCryptoKeyProvider
{
    /// <summary>
    /// Returns the raw 32-byte AES-256 key material.
    /// </summary>
    /// <returns>A 32-byte array representing the encryption key.</returns>
    byte[] GetKey();
}

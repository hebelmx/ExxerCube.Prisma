// Intentionally lives in Application/Ports (not Infrastructure) so the MigrateCommand
// standalone path can resolve a default ISecretProvider without referencing Orchestration.
// A future Veriqan architecture test must grant this placement as an explicit exception.
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Default <see cref="ISecretProvider"/> implementation backed by <see cref="IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Reads secrets from the standard ASP.NET Core configuration system (appsettings.json,
/// environment variables, user secrets, etc.). The logical name is used directly as the
/// <c>IConfiguration</c> key path, so existing environment-variable overrides continue to
/// work unchanged (e.g. <c>Veriqan__LegalBaseline__EncryptionKey</c> for
/// <c>Veriqan:LegalBaseline:EncryptionKey</c>).
/// </para>
/// <para>
/// <b>KeyId/Version contract (rotation surface):</b>
/// <list type="bullet">
///   <item>
///     <see cref="SecretValue.KeyId"/> = <c>"config:&lt;logicalName&gt;"</c> — stable across
///     rotations; uniquely identifies which configuration key is being read.
///   </item>
///   <item>
///     <see cref="SecretValue.Version"/> = first 8 hex characters of
///     <c>SHA-256(UTF-8(value))</c> — a non-reversible fingerprint that changes when the
///     operator rotates the value. Callers can compare this between resolutions to detect a
///     rotation without re-reading the raw secret.
///   </item>
/// </list>
/// <b>The raw secret value is never included in <see cref="SecretValue.KeyId"/> or
/// <see cref="SecretValue.Version"/>.</b>
/// </para>
/// <para>
/// <b>Extension point:</b> to plug in Azure Key Vault, AWS KMS, or another secrets manager,
/// implement <see cref="ISecretProvider"/> and register it in DI
/// <em>before</em> calling <c>AddVeriqan</c>. <c>ConfigurationSecretProvider</c> is registered
/// with <c>TryAddSingleton</c>, so any earlier registration takes precedence automatically.
/// No cloud SDK is referenced here — that dependency is deferred until a business decision
/// on vendor choice is made.
/// </para>
/// </remarks>
public sealed class ConfigurationSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationSecretProvider"/>.
    /// </summary>
    /// <param name="configuration">Application configuration to read secrets from.</param>
    public ConfigurationSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public Task<Result<SecretValue>> GetSecretAsync(
        string logicalName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<SecretValue>());

        var rawValue = _configuration[logicalName];

        if (string.IsNullOrWhiteSpace(rawValue))
            return Task.FromResult(
                Result<SecretValue>.WithFailure(
                    $"Secret '{logicalName}' is absent or empty in the configuration. " +
                    "Supply the value via an environment variable or a secrets manager."));

        var keyId = $"config:{logicalName}";
        var version = ComputeFingerprint(rawValue);
        var secretValue = new SecretValue(rawValue, keyId, version, DateTimeOffset.UtcNow);

        return Task.FromResult(Result<SecretValue>.WithSuccess(secretValue));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first 8 hex characters of the SHA-256 hash of <paramref name="value"/>.
    /// This is a non-reversible fingerprint suitable for use as a rotation marker.
    /// The raw secret value is NEVER embedded in the output.
    /// </summary>
    private static string ComputeFingerprint(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        // Convert.ToHexString produces uppercase; lower-case for readability.
        return Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
    }
}

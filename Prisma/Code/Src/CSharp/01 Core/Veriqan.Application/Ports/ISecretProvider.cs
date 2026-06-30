using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Port for resolving named secrets from a backing store (configuration, environment variables,
/// Azure Key Vault, AWS KMS, HashiCorp Vault, etc.).
/// </summary>
/// <remarks>
/// <para>
/// The interface lives in the Application layer so all infrastructure consumers (Persistence,
/// Reporting, Worker composition root) can depend on it without violating the in-Veriqan
/// dependency direction (infra → application → domain).
/// </para>
/// <para>
/// <b>Logical name</b> — a stable, human-readable string that identifies a secret independently
/// of where it is stored. For the default <c>ConfigurationSecretProvider</c> the logical name
/// equals the <c>IConfiguration</c> key path (e.g.
/// <c>"Veriqan:LegalBaseline:EncryptionKey"</c>), so existing configuration and environment
/// variables continue to work unchanged.
/// </para>
/// <para>
/// <b>Key-ID / rotation surface</b> — the returned <see cref="SecretValue.KeyId"/> and
/// <see cref="SecretValue.Version"/> let callers detect rotation without re-reading the value
/// on every call. A future Key Vault implementation returns the provider's native version
/// (e.g. the Key Vault key-version GUID) in <see cref="SecretValue.Version"/>.
/// </para>
/// <para>
/// <b>Extension point</b> — to plug in a cloud Key Vault or KMS backend, implement this
/// interface and register it in DI before <c>AddVeriqan</c>. The default
/// <c>ConfigurationSecretProvider</c> is registered with <c>TryAddSingleton</c>, so any
/// prior registration takes precedence automatically. No cloud SDK is referenced by this
/// abstraction or by the default implementation — that is intentional (business-gated).
/// </para>
/// <para>
/// <b>Never throw for a missing secret.</b> Return <c>Result.WithFailure</c> instead so
/// callers can decide how to handle the absence (hard startup failure, warn-and-degrade, etc.).
/// </para>
/// </remarks>
public interface ISecretProvider
{
    /// <summary>
    /// Resolves the secret identified by <paramref name="logicalName"/>.
    /// </summary>
    /// <param name="logicalName">
    /// A stable, human-readable identifier for the secret — independent of where it is stored.
    /// For the default config-backed implementation this is the <c>IConfiguration</c> key path.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with a <see cref="SecretValue"/> when the secret is
    /// present and non-empty; a failure result when the secret is absent, empty, or cannot be
    /// retrieved. <b>Never throws for a missing secret.</b>
    /// </returns>
    Task<Result<SecretValue>> GetSecretAsync(
        string logicalName,
        CancellationToken cancellationToken = default);
}

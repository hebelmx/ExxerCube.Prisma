namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Port for retrieving secrets (API keys, connection strings, etc.) at runtime.
/// The default implementation resolves values from <c>IConfiguration</c> (user-secrets / env vars).
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Returns the secret value associated with <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The configuration key (e.g. <c>"Gemini:ApiKey"</c>).</param>
    /// <param name="cancellationToken">Cancellation token (reserved for future async stores).</param>
    /// <returns>
    /// A successful result containing the secret value, or a failure when the key is absent or empty.
    /// The value is NEVER logged.
    /// </returns>
    Task<Result<string>> GetSecretAsync(string key, CancellationToken cancellationToken = default);
}

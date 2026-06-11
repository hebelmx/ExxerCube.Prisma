namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Resolves the configured <see cref="ISiaraSessionProvider"/> for the deployment's chosen
/// <see cref="SiaraAuthMode"/>.
/// </summary>
/// <remarks>
/// The auth mode is the client's deployment-time, technical-legal choice (read from configuration).
/// The resolver is <strong>fail-closed</strong>: if the configured mode has no registered provider it
/// returns a failure result rather than throwing or silently defaulting, so the downloader never
/// proceeds unauthenticated. See ADR-010.
/// </remarks>
public interface ISiaraSessionProviderResolver
{
    /// <summary>
    /// Returns the provider registered for the configured <see cref="SiaraAuthMode"/>.
    /// </summary>
    /// <returns>
    /// A success result wrapping the matching provider, or a failure when no provider is registered
    /// for the configured mode.
    /// </returns>
    Result<ISiaraSessionProvider> Resolve();
}

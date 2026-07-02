namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Runtime registry of <see cref="ILlmProvider"/> implementations.
/// Allows callers to retrieve the currently-active provider or switch to a named one
/// without taking a direct dependency on infrastructure types.
/// </summary>
public interface ILlmProviderFactory
{
    /// <summary>
    /// Returns the currently-active provider.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when configuration names a provider that is not registered — this is a
    /// mis-configuration, not a business-logic error.
    /// </exception>
    ILlmProvider GetActive();

    /// <summary>
    /// Returns the provider with the given <paramref name="providerName"/>, or a failure result
    /// when no provider with that name is registered.
    /// </summary>
    Result<ILlmProvider> Get(string providerName);

    /// <summary>Gets the names of all registered providers.</summary>
    IReadOnlyList<string> RegisteredNames { get; }

    /// <summary>
    /// Switches the active provider to the one named by <paramref name="providerName"/>.
    /// Returns a failure result if <paramref name="providerName"/> is not registered.
    /// </summary>
    Result SetActive(string providerName);
}

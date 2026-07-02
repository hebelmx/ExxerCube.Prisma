using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.Classification.Llm;

/// <summary>
/// Singleton registry of all registered <see cref="ILlmProvider"/> implementations.
/// Allows runtime switching of the active provider without restarting the host.
/// </summary>
public sealed class LlmProviderFactory : ILlmProviderFactory
{
    private readonly Dictionary<string, ILlmProvider> _providers;
    private readonly IOptionsMonitor<LlmProvidersOptions> _options;

    /// Volatile so that a write in one thread is visible to reads in others without a lock.
    private volatile string? _activeOverride;

    /// <summary>
    /// Initializes a new instance of <see cref="LlmProviderFactory"/>.
    /// </summary>
    /// <param name="providers">All registered provider implementations (resolved by DI).</param>
    /// <param name="options">Live-reloadable options that carry the configured active name.</param>
    public LlmProviderFactory(
        IEnumerable<ILlmProvider> providers,
        IOptionsMonitor<LlmProvidersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _providers = providers.ToDictionary(
            p => p.Name,
            p => p,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RegisteredNames =>
        _providers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    /// <inheritdoc />
    public ILlmProvider GetActive()
    {
        var name = _activeOverride ?? _options.CurrentValue.Active;

        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"LLM provider '{name}' is not registered. " +
            $"Available providers: {string.Join(", ", RegisteredNames)}. " +
            "Check the LlmProviders:Active configuration value.");
    }

    /// <inheritdoc />
    public Result<ILlmProvider> Get(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return Result<ILlmProvider>.WithFailure("Provider name cannot be null or empty.");
        }

        return _providers.TryGetValue(providerName, out var provider)
            ? Result<ILlmProvider>.WithSuccess(provider)
            : Result<ILlmProvider>.WithFailure(
                $"LLM provider '{providerName}' is not registered. " +
                $"Available: {string.Join(", ", RegisteredNames)}.");
    }

    /// <inheritdoc />
    public Result SetActive(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return Result.WithFailure("Provider name cannot be null or empty.");
        }

        if (!_providers.ContainsKey(providerName))
        {
            return Result.WithFailure(
                $"Cannot set active provider to '{providerName}': not registered. " +
                $"Available: {string.Join(", ", RegisteredNames)}.");
        }

        _activeOverride = providerName;
        return Result.Success();
    }
}

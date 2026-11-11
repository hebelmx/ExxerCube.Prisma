using ExxerCube.Prisma.Domain.Enum;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// Fail-closed resolver that returns the <see cref="ISiaraSessionProvider"/> registered for the
/// deployment's configured <see cref="SiaraAuthMode"/> (ADR-010). The mode is the client's deployment-time
/// technical-legal choice, read from <see cref="IOptions{SiaraAuthOptions}"/>.
/// </summary>
/// <remarks>
/// Providers are registered keyed by <see cref="SiaraAuthMode"/>; this resolver looks up the keyed service
/// for the configured mode. If the configured mode is unknown or has no registered provider it returns a
/// failure result rather than throwing or silently defaulting, so the downloader
/// (MVP-PATH 1.1) can never proceed unauthenticated.
/// </remarks>
public sealed class SiaraSessionProviderResolver : ISiaraSessionProviderResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SiaraAuthOptions _options;
    private readonly ILogger<SiaraSessionProviderResolver> _logger;

    /// <summary>Initializes a new instance of the <see cref="SiaraSessionProviderResolver"/> class.</summary>
    /// <param name="serviceProvider">The scope's service provider used to resolve the keyed provider.</param>
    /// <param name="options">The SIARA auth options carrying the configured mode.</param>
    /// <param name="logger">The logger instance.</param>
    public SiaraSessionProviderResolver(
        IServiceProvider serviceProvider,
        IOptions<SiaraAuthOptions> options,
        ILogger<SiaraSessionProviderResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Result<ISiaraSessionProvider> Resolve()
    {
        var mode = _options.AuthMode;

        if (!System.Enum.IsDefined(mode))
        {
            _logger.LogError("Configured SIARA auth mode '{Mode}' is not a known mode.", mode);
            return Result<ISiaraSessionProvider>.WithFailure(
                $"Configured SIARA auth mode '{mode}' is not a known mode.");
        }

        var provider = _serviceProvider.GetKeyedService<ISiaraSessionProvider>(mode);
        if (provider is null)
        {
            _logger.LogError("No SIARA session provider is registered for auth mode '{Mode}'.", mode);
            return Result<ISiaraSessionProvider>.WithFailure(
                $"No SIARA session provider is registered for auth mode '{mode}'.");
        }

        return Result<ISiaraSessionProvider>.Success(provider);
    }
}

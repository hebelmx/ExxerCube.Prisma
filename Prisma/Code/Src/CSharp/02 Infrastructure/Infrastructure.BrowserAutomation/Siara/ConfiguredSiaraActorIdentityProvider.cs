using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.Siara;

/// <summary>
/// Configuration-backed implementation of ISiaraActorIdentityProvider that resolves the service-account
/// identity from SiaraAuthOptions.Actor at runtime (ADR-010 P2).
/// </summary>
/// <remarks>
/// <para>
/// This is the default, deployment-time actor source: the ActorId is set in the deployment configuration
/// (appsettings / Key Vault config provider) and stamped on every acquired SIARA session for audit and
/// per-document non-repudiation. A future implementation can source the identity from an authenticated
/// principal (for example, the operator who triggered an interactive acquisition) behind the same port.
/// </para>
/// <para>
/// Fail-closed: if ActorId is missing or empty, resolution fails and the caller must not proceed with
/// the acquisition. Only the ActorId is logged (never credentials or secrets). Registered as a singleton
/// because the configured identity is stateless and stable across requests.
/// </para>
/// </remarks>
public sealed class ConfiguredSiaraActorIdentityProvider : ISiaraActorIdentityProvider
{
    private readonly SiaraActorOptions _actorOptions;
    private readonly ILogger<ConfiguredSiaraActorIdentityProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="ConfiguredSiaraActorIdentityProvider"/> class.</summary>
    /// <param name="options">The SIARA auth options (the Actor sub-section is read).</param>
    /// <param name="logger">The logger instance.</param>
    public ConfiguredSiaraActorIdentityProvider(
        IOptions<SiaraAuthOptions> options,
        ILogger<ConfiguredSiaraActorIdentityProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _actorOptions = options.Value.Actor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<SiaraActor>> GetCurrentActorAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<SiaraActor>());
        }

        if (string.IsNullOrEmpty(_actorOptions.ActorId))
        {
            _logger.LogError(
                "SIARA actor identity resolution failed: Siara:Actor:ActorId is not configured. " +
                "Session acquisition will be denied (fail-closed).");

            return Task.FromResult(
                Result<SiaraActor>.WithFailure(
                    "SIARA actor identity is not configured. Set Siara:Actor:ActorId in the deployment configuration."));
        }

        _logger.LogInformation(
            "Resolved SIARA acquiring actor {ActorId} (ServiceAccount) from deployment configuration.",
            _actorOptions.ActorId);

        var actor = new SiaraActor
        {
            ActorId = _actorOptions.ActorId,
            ActorType = SiaraActorType.ServiceAccount,
            DisplayName = _actorOptions.DisplayName,
        };

        return Task.FromResult(Result<SiaraActor>.Success(actor));
    }
}

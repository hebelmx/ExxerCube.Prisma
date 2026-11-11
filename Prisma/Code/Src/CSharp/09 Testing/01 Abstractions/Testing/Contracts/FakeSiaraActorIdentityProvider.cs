using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written, stateful reference fake of ISiaraActorIdentityProvider (ADR-005 §6, ADR-010 P2):
/// honest logic, no mocking framework, modelling a deployment-configured service-account identity source.
/// </summary>
/// <remarks>
/// <para>
/// Returns a fixed, configurable actor on every call, tracking a read count so provider tests can assert
/// the actor identity path is exercised (ties to the ADR-010 audit requirement) without wiring up real
/// configuration or options. Construct one per test for isolation.
/// </para>
/// <para>
/// The fake never persists credentials and exposes no secret — it is a test double for the trustworthy
/// identity seam, not for the credential seam.
/// </para>
/// </remarks>
public sealed class FakeSiaraActorIdentityProvider : ISiaraActorIdentityProvider
{
    private readonly SiaraActor _actor;
    private readonly object _gate = new();
    private int _reads;

    /// <summary>Initializes the fake with the actor it should return.</summary>
    /// <param name="actorId">The actor identifier to return. Defaults to a non-empty placeholder.</param>
    /// <param name="actorType">The actor type to return. Defaults to ServiceAccount.</param>
    /// <param name="displayName">Optional display name. Defaults to null.</param>
    public FakeSiaraActorIdentityProvider(
        string actorId = "fake-siara-service",
        SiaraActorType actorType = SiaraActorType.ServiceAccount,
        string? displayName = null)
    {
        _actor = new SiaraActor
        {
            ActorId = actorId,
            ActorType = actorType,
            DisplayName = displayName,
        };
    }

    /// <summary>Gets the number of times the actor identity has been read from this fake.</summary>
    public int Reads
    {
        get { lock (_gate) { return _reads; } }
    }

    /// <inheritdoc />
    public Task<Result<SiaraActor>> GetCurrentActorAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<SiaraActor>());
        }

        lock (_gate)
        {
            _reads++;
        }

        return Task.FromResult(Result<SiaraActor>.Success(_actor));
    }
}

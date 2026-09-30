using System.Reflection;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for ISiaraActorIdentityProvider — every implementation (the mock blueprint, the
/// reference fake, and the production config-backed source) must pass these tests unchanged
/// (ADR-005, ADR-010 P2).
/// </summary>
/// <remarks>
/// Uses the ADR-005 §3 default injected-Sut mechanism. Scope (ADR-005 §5): Result semantics (failure not
/// throw), cancellation, and that a resolved actor is trustworthy — non-null, non-empty ActorId, and a
/// defined ActorType. The specifics of where the identity comes from stay in each implementation's own tests.
/// </remarks>
public abstract class SiaraActorIdentityProviderContract
{
    /// <summary>Initializes the contract with the provider under test.</summary>
    /// <param name="sut">The ISiaraActorIdentityProvider implementation to verify.</param>
    protected SiaraActorIdentityProviderContract(ISiaraActorIdentityProvider sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the provider under test.</summary>
    protected ISiaraActorIdentityProvider Sut { get; }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result — never an exception.</summary>
    [Fact]
    public async Task GetCurrentActorAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.GetCurrentActorAsync(cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a valid call yields a trustworthy actor with a non-empty ActorId and a defined ActorType.
    /// </summary>
    [Fact]
    public async Task GetCurrentActorAsync_ReturnsTrustworthyActor()
    {
        var result = await Sut.GetCurrentActorAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        SiaraActor actor = result.Value!;
        actor.ActorId.ShouldNotBeNullOrEmpty();
        System.Enum.IsDefined(actor.ActorType).ShouldBeTrue();
    }

    /// <summary>
    /// Contract: the <see cref="SiaraActor"/> type (reachable via SiaraSession.AcquiredBy) exposes no
    /// raw-credential members — the never-store-credentials guarantee, enforced by reflection (ADR-010).
    /// </summary>
    [Fact]
    public void ActorType_ExposesNoCredentialMembers()
    {
        string[] forbidden = ["password", "passwd", "pwd", "username", "userid", "credential", "secret"];

        var members = typeof(SiaraActor)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name);

        foreach (var name in members)
        {
            foreach (var token in forbidden)
            {
                name.Contains(token, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                    $"SiaraActor must not expose a credential-like member ('{name}' matched '{token}').");
            }
        }
    }
}

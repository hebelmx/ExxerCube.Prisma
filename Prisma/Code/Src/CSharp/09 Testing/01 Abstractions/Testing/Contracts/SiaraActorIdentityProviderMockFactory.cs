using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance of
/// SiaraActorIdentityProviderContract (ADR-005 §6, ADR-010 P2).
/// </summary>
/// <remarks>
/// The configuration is the executable design specification for ISiaraActorIdentityProvider: a
/// pre-cancelled token yields a cancelled result; otherwise the call returns a ServiceAccount actor with
/// a non-empty ActorId. The fuller stateful reference lives in FakeSiaraActorIdentityProvider.
/// </remarks>
public static class SiaraActorIdentityProviderMockFactory
{
    /// <summary>
    /// Creates an ISiaraActorIdentityProvider mock that satisfies every test in
    /// SiaraActorIdentityProviderContract.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static ISiaraActorIdentityProvider CreateContractConformingMock()
    {
        var mock = Substitute.For<ISiaraActorIdentityProvider>();

        mock.GetCurrentActorAsync(Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(0).IsCancellationRequested
                ? ResultExtensions.Cancelled<SiaraActor>()
                : Result<SiaraActor>.Success(new SiaraActor
                {
                    ActorId = "mock-actor",
                    ActorType = SiaraActorType.ServiceAccount,
                }));

        return mock;
    }
}

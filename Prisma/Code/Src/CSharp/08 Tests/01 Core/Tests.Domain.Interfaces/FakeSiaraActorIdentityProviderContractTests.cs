namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Reference-fake instance of <see cref="SiaraActorIdentityProviderContract"/> (ADR-005 §6): proves the
/// stateful <see cref="FakeSiaraActorIdentityProvider"/> honors the contract, so it is a trustworthy
/// stand-in for provider tests before a real deployment configuration exists.
/// </summary>
public sealed class FakeSiaraActorIdentityProviderContractTests : SiaraActorIdentityProviderContract
{
    /// <summary>Initializes the reference-fake instance.</summary>
    public FakeSiaraActorIdentityProviderContractTests()
        : base(new FakeSiaraActorIdentityProvider())
    {
    }
}

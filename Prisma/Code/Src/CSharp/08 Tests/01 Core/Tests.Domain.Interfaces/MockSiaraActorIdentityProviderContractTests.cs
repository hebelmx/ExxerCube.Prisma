namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="SiaraActorIdentityProviderContract"/> (ADR-005 §6): the mock-backed
/// inheritor that exists from design time. Kept forever.
/// </summary>
/// <remarks>
/// The SUT comes from <see cref="SiaraActorIdentityProviderMockFactory"/> — never from inline per-test
/// stubbing. A red blueprint means the design specification itself is incomplete; extend the factory in
/// the same change.
/// </remarks>
public sealed class MockSiaraActorIdentityProviderContractTests : SiaraActorIdentityProviderContract
{
    /// <summary>Initializes the blueprint instance with the contract-conforming mock.</summary>
    public MockSiaraActorIdentityProviderContractTests()
        : base(SiaraActorIdentityProviderMockFactory.CreateContractConformingMock())
    {
    }
}

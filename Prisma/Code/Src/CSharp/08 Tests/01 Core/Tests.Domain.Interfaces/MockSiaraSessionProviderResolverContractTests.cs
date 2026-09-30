using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="SiaraSessionProviderResolverContract"/> (ADR-005 §6): the mock-backed
/// inheritor that exists from design time. Kept forever.
/// </summary>
/// <remarks>
/// The SUT comes from <see cref="SiaraSessionProviderResolverMockFactory"/> — never from inline per-test
/// stubbing. A red blueprint means the design specification itself is incomplete; extend the factory in
/// the same change.
/// </remarks>
public sealed class MockSiaraSessionProviderResolverContractTests : SiaraSessionProviderResolverContract
{
    /// <summary>Initializes the blueprint instance with the contract-conforming mock.</summary>
    public MockSiaraSessionProviderResolverContractTests()
        : base(
            SiaraSessionProviderResolverMockFactory.CreateContractConformingMock(SiaraAuthMode.SessionPassthrough),
            SiaraAuthMode.SessionPassthrough)
    {
    }
}

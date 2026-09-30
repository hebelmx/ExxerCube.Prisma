namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="SiaraSessionProviderContract"/> (ADR-005 §6): the mock-backed
/// inheritor that exists from design time, before any production strategy. Kept forever.
/// </summary>
/// <remarks>
/// The SUT comes from <see cref="SiaraSessionProviderMockFactory"/> — never from inline per-test
/// stubbing. A red blueprint means the design specification itself is incomplete; extend the factory
/// in the same change.
/// </remarks>
public sealed class MockSiaraSessionProviderContractTests : SiaraSessionProviderContract
{
    /// <summary>Initializes the blueprint instance with the contract-conforming mock.</summary>
    public MockSiaraSessionProviderContractTests()
        : base(SiaraSessionProviderMockFactory.CreateContractConformingMock())
    {
    }
}

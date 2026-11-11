namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="PersonIdentityResolverContract"/> — the reference-fake-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Authored in Phase 5 of the ITDD refactor. It replaces the fake-green placeholder
/// <c>IPersonIdentityResolverContractExecutionTests</c> (whose body was <c>await Task.CompletedTask</c> —
/// it asserted nothing) and is the proper executable home for the design spec that the orphaned static
/// helper <c>IPersonIdentityResolverContractTests</c> only gestured at. Its SUT is the
/// <see cref="PersonIdentityResolverMockFactory"/> reference fake.
/// </remarks>
public sealed class MockPersonIdentityResolverContractTests : PersonIdentityResolverContract
{
    /// <summary>Initializes the blueprint instance with the reference-fake resolver.</summary>
    public MockPersonIdentityResolverContractTests()
        : base(PersonIdentityResolverMockFactory.CreateContractConformingMock())
    {
    }
}

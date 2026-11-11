namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="FieldMergeStrategyContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Converted from the original standalone <c>IFieldMergeStrategyContractTests</c>
/// (Tests.Domain) in Phase 1 of the ITDD refactor: its 16 design tests now live in the
/// contract base (lifted real bodies + restored checklist behaviors) and its inline
/// <c>.Returns(...)</c> stubbing moved into <see cref="FieldMergeStrategyMockFactory"/>.
/// Kept forever — a contract test this factory's mock cannot satisfy means the design
/// spec itself is incomplete.
/// </remarks>
public sealed class MockFieldMergeStrategyContractTests : FieldMergeStrategyContract
{
    /// <summary>
    /// Initializes the blueprint instance with the contract-conforming mock.
    /// </summary>
    public MockFieldMergeStrategyContractTests()
        : base(FieldMergeStrategyMockFactory.CreateContractConformingMock())
    {
    }
}

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="TemplateFieldMapperContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Converted from the original standalone <c>ITemplateFieldMapperContractTests</c>
/// (Tests.Domain) in Phase 2 of the ITDD refactor: its design tests now live in the
/// contract base (lifted from the zero-drift <c>TemplateFieldMapperTests</c> twin) and
/// its inline <c>.Returns(...)</c> stubbing moved into
/// <see cref="TemplateFieldMapperMockFactory"/>. Kept forever — a contract test this
/// factory's mock cannot satisfy means the design spec itself is incomplete.
/// </remarks>
public sealed class MockTemplateFieldMapperContractTests : TemplateFieldMapperContract
{
    /// <summary>
    /// Initializes the blueprint instance with the contract-conforming mock.
    /// </summary>
    public MockTemplateFieldMapperContractTests()
        : base(TemplateFieldMapperMockFactory.CreateContractConformingMock())
    {
    }
}

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="TemplateRepositoryContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Converted from the original standalone <c>ITemplateRepositoryContractTests</c>
/// (Tests.Domain) in Phase 2 of the ITDD refactor. Its <c>CreateSut()</c> returns a fresh
/// in-memory reference store from <see cref="TemplateRepositoryMockFactory"/> per test.
/// Kept forever — a contract test this factory's mock cannot satisfy means the design
/// spec itself is incomplete.
/// </remarks>
public sealed class MockTemplateRepositoryContractTests : TemplateRepositoryContract
{
    /// <inheritdoc />
    protected override ITemplateRepository CreateSut()
        => TemplateRepositoryMockFactory.CreateContractConformingMock();
}

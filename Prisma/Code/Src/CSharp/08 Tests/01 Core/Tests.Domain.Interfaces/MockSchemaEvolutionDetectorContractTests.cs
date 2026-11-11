namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="SchemaEvolutionDetectorContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Converted from the original standalone <c>ISchemaEvolutionDetectorContractTests</c>
/// (Tests.Domain) in Phase 2 of the ITDD refactor. The reference fake from
/// <see cref="SchemaEvolutionDetectorMockFactory"/> reads the active template from the
/// in-memory store this instance seeds via the contract's seed hook. Kept forever — a
/// contract test this factory's mock cannot satisfy means the design spec is incomplete.
/// </remarks>
public sealed class MockSchemaEvolutionDetectorContractTests : SchemaEvolutionDetectorContract
{
    private readonly List<TemplateDefinition> _templates = new();
    private readonly ISchemaEvolutionDetector _sut;

    /// <summary>
    /// Initializes the blueprint instance with the contract-conforming mock.
    /// </summary>
    public MockSchemaEvolutionDetectorContractTests()
    {
        _sut = SchemaEvolutionDetectorMockFactory.CreateContractConformingMock(_templates);
    }

    /// <inheritdoc />
    protected override ISchemaEvolutionDetector CreateSut() => _sut;

    /// <inheritdoc />
    protected override Task SeedActiveTemplateAsync(TemplateDefinition template, CancellationToken cancellationToken)
    {
        _templates.Add(template);
        return Task.CompletedTask;
    }
}

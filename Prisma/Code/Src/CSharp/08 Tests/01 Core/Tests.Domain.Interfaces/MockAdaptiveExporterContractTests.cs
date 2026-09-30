namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="AdaptiveExporterContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// Converted from the original standalone <c>IAdaptiveExporterContractTests</c>
/// (Tests.Domain) in Phase 2 of the ITDD refactor. The reference fake from
/// <see cref="AdaptiveExporterMockFactory"/> orchestrates the
/// <see cref="TemplateFieldMapperMockFactory"/> fake over the in-memory template store
/// this instance seeds via the contract's seed hook. Kept forever — a contract test this
/// factory's mock cannot satisfy means the design spec is incomplete.
/// </remarks>
public sealed class MockAdaptiveExporterContractTests : AdaptiveExporterContract
{
    private readonly List<TemplateDefinition> _templates = new();
    private readonly IAdaptiveExporter _sut;

    /// <summary>
    /// Initializes the blueprint instance with the contract-conforming mock.
    /// </summary>
    public MockAdaptiveExporterContractTests()
    {
        _sut = AdaptiveExporterMockFactory.CreateContractConformingMock(_templates);
    }

    /// <inheritdoc />
    protected override IAdaptiveExporter CreateSut() => _sut;

    /// <inheritdoc />
    protected override Task SeedTemplateAsync(TemplateDefinition template, CancellationToken cancellationToken)
    {
        _templates.Add(template);
        return Task.CompletedTask;
    }
}

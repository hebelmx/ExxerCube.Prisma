namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Blueprint instance of <see cref="AdaptiveDocxExtractorContract"/> — the mock-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// <para>
/// Converted from the original standalone <c>IAdaptiveDocxExtractorContractTests</c>
/// (Tests.Domain) in Phase 3 of the ITDD refactor: its design tests now live in the contract
/// base (run here against <see cref="AdaptiveDocxExtractorMockFactory"/>).
/// </para>
/// <para>
/// The two tests below are design notes the contract base does not pin:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ExtractAsync_ShouldDeduplicateFields_WhenMergeAllUsed"/> —
///   a MergeAll deduplication detail; the base's <c>MergeAll</c> test asserts the combined
///   result, not the dedup count.</description></item>
///   <item><description><see cref="GetStrategyConfidencesAsync_ShouldReturnEmptyList_WhenNoStrategiesAvailable"/>
///   — the blueprint's design ideal (an empty list). The production orchestrator instead
///   returns one zero-valued entry per strategy; the validated real contract (empty
///   <em>or</em> all-zero) is pinned in the base by
///   <c>GetStrategyConfidencesAsync_ShouldReturnEmptyOrZeroConfidences_WhenDocumentIsEmpty_Liskov</c>.
///   Kept here as a mock-only design note.</description></item>
/// </list>
/// </remarks>
public sealed class MockAdaptiveDocxExtractorContractTests : AdaptiveDocxExtractorContract
{
    private const string ValidDocxText = @"
        Expediente No.: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: PGR
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio
    ";

    /// <inheritdoc />
    protected override IAdaptiveDocxExtractor CreateSut()
        => AdaptiveDocxExtractorMockFactory.CreateContractConformingMock();

    //
    // Blueprint-only design notes (see remarks)
    //

    [Fact]
    public async Task ExtractAsync_ShouldDeduplicateFields_WhenMergeAllUsed()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var mergedFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Fechas = new List<string> { "15/11/2025" }, // Deduplicated
            Montos = new List<AmountData>
            {
                new("MXN", 100000m, "$100,000.00 MXN")
            }
        };

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.MergeAll,
            null,
            Arg.Any<CancellationToken>())
            .Returns(mergedFields);

        // Act
        var result = await extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.MergeAll,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Collections should be deduplicated
        result.ShouldNotBeNull();
        result.Fechas.Count.ShouldBe(1);
        result.Montos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnEmptyList_WhenNoStrategiesAvailable()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        extractor.GetStrategyConfidencesAsync(EmptyDocx, Arg.Any<CancellationToken>())
            .Returns(new List<StrategyConfidence>());

        // Act
        var result = await extractor.GetStrategyConfidencesAsync(
            EmptyDocx,
            TestContext.Current.CancellationToken);

        // Assert - Contract: Should return empty list (not null) when no strategies
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}

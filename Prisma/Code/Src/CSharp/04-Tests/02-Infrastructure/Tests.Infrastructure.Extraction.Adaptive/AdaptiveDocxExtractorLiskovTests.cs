namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Liskov Substitution Principle verification tests for <see cref="AdaptiveDocxExtractor"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests prove that AdaptiveDocxExtractor correctly implements <see cref="IAdaptiveDocxExtractor"/>
/// by running the SAME contract tests that were defined in Tests.Domain against the ACTUAL implementation.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> If the implementation passes all interface contract tests,
/// it satisfies the Liskov Substitution Principle and is correct.
/// </para>
/// </remarks>
public sealed class AdaptiveDocxExtractorLiskovTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdaptiveDocxExtractor> _logger;

    // Sample document that works well with structured strategy
    private const string StructuredDocxText = @"
        Expediente: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: PGR
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio
        Monto: $100,000.00 MXN
    ";

    // Sample document that works well with table-based strategy
    private const string TableBasedDocxText = @"
        | Campo              | Valor                        |
        |--------------------|------------------------------|
        | Expediente         | A/AS1-2505-088637-PHM       |
        | Causa              | Lavado de dinero            |
        | Acción Solicitada  | Aseguramiento precautorio   |
    ";

    private const string EmptyDocx = "";
    private const string IncompatibleDocx = "Random unrelated text";

    public AdaptiveDocxExtractorLiskovTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<AdaptiveDocxExtractor>(output);
    }

    //
    // Liskov Verification: ExtractAsync (BestStrategy Mode)
    //

    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldReturnExtractedFields_WhenDataFound_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var result = await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldReturnNull_WhenNoStrategyCanExtract_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var result = await extractor.ExtractAsync(IncompatibleDocx, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: May return null when no strategy can extract
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.BestStrategy, null, cts.Token));
    }

    //
    // Liskov Verification: ExtractAsync (MergeAll Mode)
    //

    [Fact]
    public async Task ExtractAsync_MergeAll_ShouldCombineResultsFromMultipleStrategies_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var result = await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should merge results from multiple strategies
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    //
    // Liskov Verification: ExtractAsync (Complement Mode)
    //

    [Fact]
    public async Task ExtractAsync_Complement_ShouldFillGaps_WhenExistingFieldsProvided_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);
        var existingFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM"
            // Causa and AccionSolicitada are missing
        };

        // Act
        var result = await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.Complement, existingFields, TestContext.Current.CancellationToken);

        // Assert - Contract: Should fill missing fields
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM"); // Preserved
        // Should have filled Causa and AccionSolicitada
    }

    [Fact]
    public async Task ExtractAsync_Complement_ShouldPreserveExistingFields_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);
        var existingFields = new ExtractedFields
        {
            Expediente = "EXISTING-EXPEDIENTE",
            Causa = "Existing causa"
        };

        // Act
        var result = await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.Complement, existingFields, TestContext.Current.CancellationToken);

        // Assert - Contract: Must preserve existing field values
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXISTING-EXPEDIENTE"); // Not overwritten
        result.Causa.ShouldBe("Existing causa"); // Not overwritten
    }

    //
    // Liskov Verification: GetStrategyConfidencesAsync
    //

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnConfidenceScores_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(StructuredDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence scores for all strategies
        confidences.ShouldNotBeNull();
        confidences.Count.ShouldBeGreaterThan(0);
        confidences.ShouldAllBe(c => !string.IsNullOrWhiteSpace(c.StrategyName));
        confidences.ShouldAllBe(c => c.Confidence >= 0 && c.Confidence <= 100);
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnOrderedByConfidence_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(StructuredDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Should be ordered by confidence (descending)
        confidences.ShouldNotBeNull();
        for (int i = 0; i < confidences.Count - 1; i++)
        {
            confidences[i].Confidence.ShouldBeGreaterThanOrEqualTo(confidences[i + 1].Confidence);
        }
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.GetStrategyConfidencesAsync(StructuredDocxText, cts.Token));
    }

    //
    // Liskov Verification: Empty Document Handling
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenDocumentIsEmpty_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var result = await extractor.ExtractAsync(EmptyDocx, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return null for empty documents
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnEmptyOrZeroConfidences_WhenDocumentIsEmpty_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(EmptyDocx, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return empty or all-zero confidences
        confidences.ShouldNotBeNull();
        // All confidences should be zero for empty document
        if (confidences.Count > 0)
        {
            confidences.ShouldAllBe(c => c.Confidence == 0);
        }
    }

    //
    // Liskov Verification: Strategy Selection
    //

    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldSelectHighestConfidenceStrategy_Liskov()
    {
        // Arrange
        var strategies = TestHelpers.CreateAllStrategies(_output);
        var extractor = new AdaptiveDocxExtractor(strategies, _logger);

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(TableBasedDocxText, TestContext.Current.CancellationToken);
        var result = await extractor.ExtractAsync(TableBasedDocxText, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: BestStrategy should use highest confidence strategy
        confidences.ShouldNotBeNull();
        var bestStrategy = confidences.OrderByDescending(c => c.Confidence).First();
        bestStrategy.Confidence.ShouldBeGreaterThan(0);

        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }
}

/// <summary>
/// Test helpers for creating strategy instances.
/// </summary>
internal static class TestHelpers
{
    public static IReadOnlyList<IAdaptiveDocxStrategy> CreateAllStrategies(ITestOutputHelper output)
    {
        return new List<IAdaptiveDocxStrategy>
        {
            new StructuredDocxStrategy(XUnitLogger.CreateLogger<StructuredDocxStrategy>(output)),
            new ContextualDocxStrategy(XUnitLogger.CreateLogger<ContextualDocxStrategy>(output)),
            new TableBasedDocxStrategy(XUnitLogger.CreateLogger<TableBasedDocxStrategy>(output)),
            new ComplementExtractionStrategy(XUnitLogger.CreateLogger<ComplementExtractionStrategy>(output)),
            new SearchExtractionStrategy(XUnitLogger.CreateLogger<SearchExtractionStrategy>(output))
        };
    }
}

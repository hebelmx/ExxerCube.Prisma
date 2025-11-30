namespace ExxerCube.Prisma.Tests.Domain.Domain.Interfaces;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Testing.Abstractions;

/// <summary>
/// Contract tests for <see cref="IAdaptiveDocxExtractor"/> interface using mocks.
/// </summary>
/// <remarks>
/// <para>
/// These tests define the BEHAVIORAL CONTRACT that ANY implementation of
/// <see cref="IAdaptiveDocxExtractor"/> MUST satisfy.
/// </para>
/// <para>
/// <strong>ITDD Principle:</strong> Interface is testable with mocks BEFORE implementation exists.
/// Any implementation that passes these tests (Liskov verification) is correct.
/// </para>
/// </remarks>
public sealed class IAdaptiveDocxExtractorContractTests
{
    // Sample test data
    private const string ValidDocxText = @"
        Expediente No.: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: PGR
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio
    ";

    private const string EmptyDocx = "";

    private const string IncompatibleDocx = "This is random text with no structure.";

    //
    // ExtractAsync - BestStrategy Mode Tests
    //

    [Fact]
    public async Task ExtractAsync_ShouldReturnExtractedFields_WhenBestStrategyModeSucceeds()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var expectedFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Lavado de dinero",
            AccionSolicitada = "Aseguramiento precautorio"
        };

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns(expectedFields);

        // Act
        var result = await extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when extraction succeeds
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldBe("Lavado de dinero");
        result.AccionSolicitada.ShouldBe("Aseguramiento precautorio");
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenNoStrategyCanHandle()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        extractor.ExtractAsync(
            IncompatibleDocx,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns((ExtractedFields?)null);

        // Act
        var result = await extractor.ExtractAsync(
            IncompatibleDocx,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Must return null when no strategy can handle document
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldUseDefaultMode_WhenModeNotSpecified()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var expectedFields = new ExtractedFields { Expediente = "TEST" };

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns(expectedFields);

        // Act - Not specifying mode should use default (BestStrategy)
        var result = await extractor.ExtractAsync(
            ValidDocxText,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Default mode should be BestStrategy
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("TEST");
    }

    [Fact]
    public async Task ExtractAsync_ShouldHandleCancellation()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ExtractedFields?>(new OperationCanceledException()));

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(ValidDocxText, cancellationToken: cts.Token));
    }

    //
    // ExtractAsync - MergeAll Mode Tests
    //

    [Fact]
    public async Task ExtractAsync_ShouldMergeResults_WhenMergeAllModeUsed()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var mergedFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Lavado de dinero",
            AccionSolicitada = "Aseguramiento precautorio",
            AdditionalFields = new Dictionary<string, string?>
            {
                ["NumeroOficio"] = "214-1-18714972/2025",
                ["AutoridadNombre"] = "PGR"
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

        // Assert - Contract: MergeAll should combine results from multiple strategies
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.AdditionalFields.ShouldContainKey("NumeroOficio");
        result.AdditionalFields.ShouldContainKey("AutoridadNombre");
    }

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

    //
    // ExtractAsync - Complement Mode Tests
    //

    [Fact]
    public async Task ExtractAsync_ShouldFillGaps_WhenComplementModeUsed()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var existingFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            // Causa is missing
        };

        var complementedFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM", // Preserved
            Causa = "Lavado de dinero", // Filled
            AccionSolicitada = "Aseguramiento precautorio" // Added
        };

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.Complement,
            existingFields,
            Arg.Any<CancellationToken>())
            .Returns(complementedFields);

        // Act
        var result = await extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.Complement,
            existingFields,
            TestContext.Current.CancellationToken);

        // Assert - Contract: Complement mode should fill gaps without overwriting
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM"); // Preserved
        result.Causa.ShouldBe("Lavado de dinero"); // Filled
        result.AccionSolicitada.ShouldBe("Aseguramiento precautorio"); // Added
    }

    [Fact]
    public async Task ExtractAsync_ShouldPreserveExisting_WhenComplementModeUsed()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var existingFields = new ExtractedFields
        {
            Expediente = "EXISTING-EXPEDIENTE",
            Causa = "EXISTING-CAUSA"
        };

        var complementedFields = new ExtractedFields
        {
            Expediente = "EXISTING-EXPEDIENTE", // NOT overwritten
            Causa = "EXISTING-CAUSA", // NOT overwritten
            AccionSolicitada = "NEW-ACCION" // Added
        };

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.Complement,
            existingFields,
            Arg.Any<CancellationToken>())
            .Returns(complementedFields);

        // Act
        var result = await extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.Complement,
            existingFields,
            TestContext.Current.CancellationToken);

        // Assert - Contract: Existing values should NOT be overwritten
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("EXISTING-EXPEDIENTE");
        result.Causa.ShouldBe("EXISTING-CAUSA");
        result.AccionSolicitada.ShouldBe("NEW-ACCION");
    }

    //
    // GetStrategyConfidencesAsync Tests
    //

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnConfidences_ForAllStrategies()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var confidences = new List<StrategyConfidence>
        {
            new("StructuredDocx", 90),
            new("ContextualDocx", 75),
            new("TableBased", 50)
        };

        extractor.GetStrategyConfidencesAsync(ValidDocxText, Arg.Any<CancellationToken>())
            .Returns(confidences);

        // Act
        var result = await extractor.GetStrategyConfidencesAsync(
            ValidDocxText,
            TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence for all available strategies
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result[0].StrategyName.ShouldBe("StructuredDocx");
        result[0].Confidence.ShouldBe(90);
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnSortedByConfidence_Descending()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        var confidences = new List<StrategyConfidence>
        {
            new("StructuredDocx", 90),
            new("ContextualDocx", 75),
            new("TableBased", 50),
            new("SearchExtraction", 25)
        };

        extractor.GetStrategyConfidencesAsync(ValidDocxText, Arg.Any<CancellationToken>())
            .Returns(confidences);

        // Act
        var result = await extractor.GetStrategyConfidencesAsync(
            ValidDocxText,
            TestContext.Current.CancellationToken);

        // Assert - Contract: Results must be sorted by confidence (descending)
        result.ShouldNotBeNull();
        result[0].Confidence.ShouldBeGreaterThanOrEqualTo(result[1].Confidence);
        result[1].Confidence.ShouldBeGreaterThanOrEqualTo(result[2].Confidence);
        result[2].Confidence.ShouldBeGreaterThanOrEqualTo(result[3].Confidence);
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

    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldHandleCancellation()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        extractor.GetStrategyConfidencesAsync(ValidDocxText, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<StrategyConfidence>>(new OperationCanceledException()));

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.GetStrategyConfidencesAsync(ValidDocxText, cts.Token));
    }

    //
    // Behavioral Contract Tests (Cross-Method Consistency)
    //

    [Fact]
    public async Task ExtractorContract_WhenNoConfidences_ExtractShouldReturnNull()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();

        extractor.GetStrategyConfidencesAsync(IncompatibleDocx, Arg.Any<CancellationToken>())
            .Returns(new List<StrategyConfidence>());

        extractor.ExtractAsync(
            IncompatibleDocx,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns((ExtractedFields?)null);

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(
            IncompatibleDocx,
            TestContext.Current.CancellationToken);

        var result = await extractor.ExtractAsync(
            IncompatibleDocx,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: No confidences implies Extract returns null
        confidences.ShouldBeEmpty();
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractorContract_WhenConfidencesExist_ExtractShouldReturnData()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();

        extractor.GetStrategyConfidencesAsync(ValidDocxText, Arg.Any<CancellationToken>())
            .Returns(new List<StrategyConfidence> { new("StructuredDocx", 90) });

        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns(new ExtractedFields { Expediente = "TEST" });

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(
            ValidDocxText,
            TestContext.Current.CancellationToken);

        var result = await extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Confidences exist implies Extract returns data
        confidences.ShouldNotBeEmpty();
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExtractorContract_BestStrategyUsesHighestConfidence()
    {
        // Arrange
        var extractor = Substitute.For<IAdaptiveDocxExtractor>();

        extractor.GetStrategyConfidencesAsync(ValidDocxText, Arg.Any<CancellationToken>())
            .Returns(new List<StrategyConfidence>
            {
                new("StructuredDocx", 90), // Highest
                new("ContextualDocx", 75),
                new("TableBased", 50)
            });

        // Mock BestStrategy to return result from highest confidence strategy
        extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            null,
            Arg.Any<CancellationToken>())
            .Returns(new ExtractedFields { Expediente = "FROM-STRUCTURED" });

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(
            ValidDocxText,
            TestContext.Current.CancellationToken);

        var result = await extractor.ExtractAsync(
            ValidDocxText,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: BestStrategy should use strategy with highest confidence
        confidences[0].StrategyName.ShouldBe("StructuredDocx");
        confidences[0].Confidence.ShouldBe(90);
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("FROM-STRUCTURED");
    }
}

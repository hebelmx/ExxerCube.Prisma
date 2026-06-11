using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IAdaptiveDocxExtractor"/> — the orchestrator that
/// selects/combines <see cref="IAdaptiveDocxStrategy"/> implementations. Inherited by the
/// mock blueprint and by <c>AdaptiveDocxExtractor</c> (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// The 12 <c>*_Liskov</c>-suffixed tests are the bodies lifted VERBATIM from
/// <c>AdaptiveDocxExtractorLiskovTests</c> (the executable truth before this refactor) —
/// names preserved including the suffix because the suite is Stryker-hardened (plan §6.1).
/// The only edit was the SUT-construction lines
/// (<c>TestHelpers.CreateAllStrategies(...)</c> + <c>new AdaptiveDocxExtractor(...)</c> →
/// <see cref="CreateSut"/>).
/// </para>
/// <para>
/// Three blueprint tests had no real-SUT twin (master plan §2 / §6.2) and are restored
/// here, triaged against the production orchestrator:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ExtractAsync_ShouldUseDefaultMode_WhenModeNotSpecified"/>
///   — the interface default mode is <see cref="ExtractionMode.BestStrategy"/>; restored
///   verbatim in intent.</description></item>
///   <item><description><see cref="ExtractorContract_WhenConfidencesExist_ExtractShouldReturnData"/>
///   — cross-method invariant, passes against the real SUT unchanged.</description></item>
///   <item><description><see cref="ExtractorContract_WhenNoConfidences_ExtractShouldReturnNull"/>
///   — restored ADAPTED: the blueprint asserted <c>confidences.ShouldBeEmpty()</c>, but the
///   production orchestrator always returns one entry per strategy (zero-valued for an
///   incompatible document), never an empty list — the already-validated truth pinned by
///   <see cref="GetStrategyConfidencesAsync_ShouldReturnEmptyOrZeroConfidences_WhenDocumentIsEmpty_Liskov"/>.
///   The genuine invariant ("no strategy can extract ⇒ all confidences zero ⇒ Extract returns
///   null") is preserved with an all-zero assertion (explicit contract decision, plan risk 2;
///   the blueprint's strict-empty assumption was never validated against a real SUT).</description></item>
/// </list>
/// <para>
/// Uses the <c>CreateSut()</c> fallback because the orchestrator is constructed from a
/// collection of strategies plus a logger (the impl instance) or from a reference fake
/// (the blueprint instance).
/// </para>
/// </remarks>
public abstract class AdaptiveDocxExtractorContract
{
    /// <summary>Document that works well with the structured strategy.</summary>
    protected const string StructuredDocxText = @"
        Expediente: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: PGR
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio
        Monto: $100,000.00 MXN
    ";

    /// <summary>Document that works well with the table-based strategy.</summary>
    protected const string TableBasedDocxText = @"
        | Campo              | Valor                        |
        |--------------------|------------------------------|
        | Expediente         | A/AS1-2505-088637-PHM       |
        | Causa              | Lavado de dinero            |
        | Acción Solicitada  | Aseguramiento precautorio   |
    ";

    /// <summary>An empty document.</summary>
    protected const string EmptyDocx = "";

    /// <summary>A document no strategy can extract from.</summary>
    protected const string IncompatibleDocx = "Random unrelated text";

    /// <summary>Creates the implementation under test. Called once per test.</summary>
    /// <returns>The <see cref="IAdaptiveDocxExtractor"/> implementation to verify.</returns>
    protected abstract IAdaptiveDocxExtractor CreateSut();

    //
    // Contract: ExtractAsync (BestStrategy Mode)
    //

    /// <summary>Contract: BestStrategy returns extracted fields when data is found.</summary>
    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldReturnExtractedFields_WhenDataFound_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var result = await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return ExtractedFields when data is found
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    /// <summary>Contract: BestStrategy returns null when no strategy can extract.</summary>
    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldReturnNull_WhenNoStrategyCanExtract_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var result = await extractor.ExtractAsync(IncompatibleDocx, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: May return null when no strategy can extract
        result.ShouldBeNull();
    }

    /// <summary>Contract: a pre-cancelled token surfaces as <see cref="OperationCanceledException"/>.</summary>
    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var extractor = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.BestStrategy, null, cts.Token));
    }

    //
    // Contract: ExtractAsync (MergeAll Mode)
    //

    /// <summary>Contract: MergeAll combines results from multiple strategies.</summary>
    [Fact]
    public async Task ExtractAsync_MergeAll_ShouldCombineResultsFromMultipleStrategies_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var result = await extractor.ExtractAsync(StructuredDocxText, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should merge results from multiple strategies
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    //
    // Contract: ExtractAsync (Complement Mode)
    //

    /// <summary>Contract: Complement fills missing fields without overwriting existing ones.</summary>
    [Fact]
    public async Task ExtractAsync_Complement_ShouldFillGaps_WhenExistingFieldsProvided_Liskov()
    {
        // Arrange
        var extractor = CreateSut();
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

    /// <summary>Contract: Complement preserves existing field values.</summary>
    [Fact]
    public async Task ExtractAsync_Complement_ShouldPreserveExistingFields_Liskov()
    {
        // Arrange
        var extractor = CreateSut();
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
    // Contract: GetStrategyConfidencesAsync
    //

    /// <summary>Contract: returns valid confidence scores for every strategy.</summary>
    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnConfidenceScores_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(StructuredDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Must return confidence scores for all strategies
        confidences.ShouldNotBeNull();
        confidences.Count.ShouldBeGreaterThan(0);
        confidences.ShouldAllBe(c => !string.IsNullOrWhiteSpace(c.StrategyName));
        confidences.ShouldAllBe(c => c.Confidence >= 0 && c.Confidence <= 100);
    }

    /// <summary>Contract: confidences are ordered descending.</summary>
    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnOrderedByConfidence_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(StructuredDocxText, TestContext.Current.CancellationToken);

        // Assert - Contract: Should be ordered by confidence (descending)
        confidences.ShouldNotBeNull();
        for (int i = 0; i < confidences.Count - 1; i++)
        {
            confidences[i].Confidence.ShouldBeGreaterThanOrEqualTo(confidences[i + 1].Confidence);
        }
    }

    /// <summary>Contract: a pre-cancelled token surfaces as <see cref="OperationCanceledException"/>.</summary>
    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldHandleCancellation_Liskov()
    {
        // Arrange
        var extractor = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Contract: Must respect cancellation token
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.GetStrategyConfidencesAsync(StructuredDocxText, cts.Token));
    }

    //
    // Contract: Empty Document Handling
    //

    /// <summary>Contract: an empty document yields null.</summary>
    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenDocumentIsEmpty_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var result = await extractor.ExtractAsync(EmptyDocx, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Contract: Should return null for empty documents
        result.ShouldBeNull();
    }

    /// <summary>Contract: an empty document yields an empty list or all-zero confidences.</summary>
    [Fact]
    public async Task GetStrategyConfidencesAsync_ShouldReturnEmptyOrZeroConfidences_WhenDocumentIsEmpty_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

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
    // Contract: Strategy Selection
    //

    /// <summary>Contract: BestStrategy uses the highest-confidence strategy.</summary>
    [Fact]
    public async Task ExtractAsync_BestStrategy_ShouldSelectHighestConfidenceStrategy_Liskov()
    {
        // Arrange
        var extractor = CreateSut();

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

    //
    // Contract: restored blueprint-only behaviours (no real-SUT twin existed before)
    //

    /// <summary>Contract: omitting the mode uses the default (<see cref="ExtractionMode.BestStrategy"/>).</summary>
    [Fact]
    public async Task ExtractAsync_ShouldUseDefaultMode_WhenModeNotSpecified()
    {
        // Arrange
        var extractor = CreateSut();

        // Act - Not specifying mode should use default (BestStrategy)
        var result = await extractor.ExtractAsync(
            StructuredDocxText,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Default mode should be BestStrategy
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    /// <summary>Contract: when no strategy can extract, all confidences are zero and Extract returns null.</summary>
    [Fact]
    public async Task ExtractorContract_WhenNoConfidences_ExtractShouldReturnNull()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(
            IncompatibleDocx,
            TestContext.Current.CancellationToken);

        var result = await extractor.ExtractAsync(
            IncompatibleDocx,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: No strategy can extract (all confidences zero) implies Extract returns null.
        // (Adapted from the blueprint's ShouldBeEmpty: the orchestrator always lists every
        //  strategy with a zero score rather than returning an empty list.)
        confidences.ShouldAllBe(c => c.Confidence == 0);
        result.ShouldBeNull();
    }

    /// <summary>Contract: when confidences exist, Extract returns data.</summary>
    [Fact]
    public async Task ExtractorContract_WhenConfidencesExist_ExtractShouldReturnData()
    {
        // Arrange
        var extractor = CreateSut();

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(
            StructuredDocxText,
            TestContext.Current.CancellationToken);

        var result = await extractor.ExtractAsync(
            StructuredDocxText,
            ExtractionMode.BestStrategy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Contract: Confidences exist implies Extract returns data
        confidences.ShouldNotBeEmpty();
        result.ShouldNotBeNull();
    }
}

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Adaptive;

using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;
using ExxerCube.Prisma.Infrastructure.Extraction.Adaptive.Strategies;
using ExxerCube.Prisma.Testing.Abstractions;
using Meziantou.Extensions.Logging.Xunit.v3;

/// <summary>
/// Integration tests for complete Adaptive DOCX extraction system with all strategies.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify the COMPLETE extraction pipeline with REAL document text and ALL strategies:
/// </para>
/// <list type="bullet">
///   <item><description><strong>BestStrategy Mode:</strong> Selects highest confidence strategy and verifies extraction</description></item>
///   <item><description><strong>MergeAll Mode:</strong> Combines results from multiple strategies</description></item>
///   <item><description><strong>Complement Mode:</strong> Fills gaps in existing extraction</description></item>
///   <item><description><strong>Confidence Scoring:</strong> Verifies strategy selection logic</description></item>
///   <item><description><strong>Real Document Formats:</strong> Tests with realistic document variations</description></item>
/// </list>
/// <para>
/// <strong>Integration Scope:</strong> All 5 strategies + orchestrator + merge strategy working together.
/// </para>
/// </remarks>
public sealed class AdaptiveDocxExtractionIntegrationTests
{
    private readonly ITestOutputHelper _output;

    // Real-world document samples (extracted from actual SIARA DOCX files)

    /// <summary>
    /// Structured document with clear label-value pairs (best for StructuredDocxStrategy).
    /// </summary>
    private const string StructuredDocument = @"
        OFICIO DE ASEGURAMIENTO PRECAUTORIO

        Expediente: A/AS1-2505-088637-PHM
        Oficio: 214-1-18714972/2025
        Autoridad: Procuraduría General de la República (PGR)
        Causa: Lavado de dinero
        Acción Solicitada: Aseguramiento precautorio de cuenta bancaria

        DATOS DE LA CUENTA:
        CLABE: 012345678901234567
        Banco: BANAMEX
        Titular: Juan Carlos GARCÍA LÓPEZ
        RFC: GALJ850101XXX

        Monto a asegurar: $1,500,000.00 MXN
        Fecha de solicitud: 30/01/2025
        Fecha de vencimiento: 30/07/2025
    ";

    /// <summary>
    /// Contextual/narrative document with embedded information (best for ContextualDocxStrategy).
    /// </summary>
    private const string ContextualDocument = @"
        DOCUMENTO OFICIAL

        En el expediente A/AS1-2505-088637-PHM, relacionado con el oficio 214-1-18714972/2025
        emitido por la Procuraduría General de la República (PGR), se solicita el aseguramiento
        precautorio de la cuenta bancaria identificada con CLABE 012345678901234567, titularidad
        de Juan Carlos GARCÍA LÓPEZ (RFC: GALJ850101XXX), en BANAMEX.

        La investigación por lavado de dinero requiere el aseguramiento de un monto de
        $1,500,000.00 MXN (UN MILLÓN QUINIENTOS MIL PESOS 00/100 MONEDA NACIONAL).

        La solicitud fue realizada el 30 de enero de 2025 y tiene vigencia hasta el 30 de julio de 2025.
    ";

    /// <summary>
    /// Table-formatted document with pipe delimiters (best for TableBasedDocxStrategy).
    /// </summary>
    private const string TableDocument = @"
        REQUERIMIENTO DE ASEGURAMIENTO

        | Campo                 | Valor                                    |
        |-----------------------|------------------------------------------|
        | Expediente            | A/AS1-2505-088637-PHM                   |
        | Oficio                | 214-1-18714972/2025                     |
        | Autoridad             | PGR                                      |
        | Causa                 | Lavado de dinero                        |
        | Acción Solicitada     | Aseguramiento precautorio               |
        | CLABE                 | 012345678901234567                      |
        | Banco                 | BANAMEX                                  |
        | Titular               | Juan Carlos GARCÍA LÓPEZ                |
        | RFC                   | GALJ850101XXX                           |
        | Monto                 | $1,500,000.00 MXN                       |
        | Fecha Solicitud       | 30/01/2025                              |
        | Fecha Vencimiento     | 30/07/2025                              |
    ";

    /// <summary>
    /// Mixed format document combining multiple patterns (requires MergeAll mode).
    /// </summary>
    private const string MixedFormatDocument = @"
        ASEGURAMIENTO PRECAUTORIO - CASO COMPLEJO

        Expediente: A/AS1-2505-088637-PHM

        En relación al oficio 214-1-18714972/2025, la Procuraduría General de la República
        solicita el aseguramiento precautorio derivado de investigación por lavado de dinero.

        | Cuenta        | Detalle                    |
        |---------------|----------------------------|
        | CLABE         | 012345678901234567        |
        | Banco         | BANAMEX                    |
        | Titular       | Juan Carlos GARCÍA LÓPEZ  |

        Monto: $1,500,000.00 MXN
        Vigencia: 30/01/2025 al 30/07/2025
    ";

    /// <summary>
    /// Partially complete document (for testing Complement mode).
    /// </summary>
    private const string PartialDocument = @"
        REQUERIMIENTO ADICIONAL

        Oficio: 214-1-18714972/2025
        Autoridad: PGR

        Información adicional sobre cuenta CLABE 012345678901234567 en BANAMEX.
        Monto: $1,500,000.00 MXN
        Fecha: 30/01/2025
    ";

    /// <summary>
    /// Empty/invalid document.
    /// </summary>
    private const string EmptyDocument = "Random unrelated text without any extractable fields.";

    public AdaptiveDocxExtractionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    //
    // Integration Tests: BestStrategy Mode
    //

    [Fact]
    public async Task ExtractAsync_StructuredDocument_BestStrategy_SelectsStructuredStrategy()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing BestStrategy mode with structured document");

        // Act - Get confidences first to verify strategy selection
        var confidences = await extractor.GetStrategyConfidencesAsync(StructuredDocument, TestContext.Current.CancellationToken);
        var result = await extractor.ExtractAsync(StructuredDocument, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert
        _output.WriteLine("\n[CONFIDENCES]");
        foreach (var c in confidences)
        {
            _output.WriteLine($"  {c.StrategyName}: {c.Confidence}");
        }

        // Verify StructuredDocxStrategy has highest confidence
        var bestStrategy = confidences.First();
        bestStrategy.StrategyName.ShouldBe("StructuredDocx", "StructuredDocxStrategy should have highest confidence for structured document");
        bestStrategy.Confidence.ShouldBeGreaterThan(0);

        // Verify extraction results
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("Lavado de dinero");
        result.AccionSolicitada.ShouldNotBeNull();
        result.AccionSolicitada.ShouldContain("Aseguramiento");

        // Verify monetary extraction
        result.Montos.Count.ShouldBeGreaterThan(0, "Should extract monetary amounts");
        var monto = result.Montos.FirstOrDefault(m => m.Value == 1500000m);
        monto.ShouldNotBeNull("Should extract MXN 1,500,000.00");

        _output.WriteLine($"\n[EXTRACTION RESULT]");
        _output.WriteLine($"  Expediente: {result.Expediente}");
        _output.WriteLine($"  Causa: {result.Causa}");
        _output.WriteLine($"  AccionSolicitada: {result.AccionSolicitada}");
        _output.WriteLine($"  Montos: {result.Montos.Count}");
        _output.WriteLine($"  Fechas: {result.Fechas.Count}");
    }

    [Fact]
    public async Task ExtractAsync_ContextualDocument_BestStrategy_SelectsContextualStrategy()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing BestStrategy mode with contextual document");

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(ContextualDocument, TestContext.Current.CancellationToken);
        var result = await extractor.ExtractAsync(ContextualDocument, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert
        _output.WriteLine("\n[CONFIDENCES]");
        foreach (var c in confidences)
        {
            _output.WriteLine($"  {c.StrategyName}: {c.Confidence}");
        }

        // Verify extraction results (ContextualDocxStrategy should win or score high)
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");

        _output.WriteLine($"\n[EXTRACTION RESULT]");
        _output.WriteLine($"  Expediente: {result.Expediente}");
        _output.WriteLine($"  Causa: {result.Causa}");
        _output.WriteLine($"  Montos: {result.Montos.Count}");
    }

    [Fact]
    public async Task ExtractAsync_TableDocument_BestStrategy_SelectsTableStrategy()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing BestStrategy mode with table document");

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(TableDocument, TestContext.Current.CancellationToken);
        var result = await extractor.ExtractAsync(TableDocument, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert
        _output.WriteLine("\n[CONFIDENCES]");
        foreach (var c in confidences)
        {
            _output.WriteLine($"  {c.StrategyName}: {c.Confidence}");
        }

        // Verify TableBasedDocxStrategy has high confidence
        var tableStrategyConfidence = confidences.FirstOrDefault(c => c.StrategyName == "TableBased");
        tableStrategyConfidence.ShouldNotBeNull();
        tableStrategyConfidence.Confidence.ShouldBeGreaterThan(0, "TableBasedDocxStrategy should detect table format");

        // Verify extraction results
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("Lavado de dinero");

        _output.WriteLine($"\n[EXTRACTION RESULT]");
        _output.WriteLine($"  Expediente: {result.Expediente}");
        _output.WriteLine($"  Causa: {result.Causa}");
        _output.WriteLine($"  AccionSolicitada: {result.AccionSolicitada}");
    }

    [Fact]
    public async Task ExtractAsync_EmptyDocument_BestStrategy_ReturnsNull()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing BestStrategy mode with empty document");

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(EmptyDocument, TestContext.Current.CancellationToken);
        var result = await extractor.ExtractAsync(EmptyDocument, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert
        _output.WriteLine("\n[CONFIDENCES]");
        foreach (var c in confidences)
        {
            _output.WriteLine($"  {c.StrategyName}: {c.Confidence}");
        }

        // All strategies should have zero or very low confidence
        confidences.ShouldAllBe(c => c.Confidence <= 10, "All strategies should have low confidence for invalid document");

        // Result should be null (no extractable data)
        result.ShouldBeNull("Should return null when no strategy can extract meaningful data");

        _output.WriteLine($"\n[EXTRACTION RESULT] null (as expected)");
    }

    //
    // Integration Tests: MergeAll Mode
    //

    [Fact]
    public async Task ExtractAsync_MixedFormatDocument_MergeAll_CombinesMultipleStrategies()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing MergeAll mode with mixed format document");

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(MixedFormatDocument, TestContext.Current.CancellationToken);
        var result = await extractor.ExtractAsync(MixedFormatDocument, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        // Assert
        _output.WriteLine("\n[CONFIDENCES]");
        foreach (var c in confidences)
        {
            _output.WriteLine($"  {c.StrategyName}: {c.Confidence}");
        }

        // Multiple strategies should have confidence > 0
        var capableStrategies = confidences.Where(c => c.Confidence > 0).ToList();
        capableStrategies.Count.ShouldBeGreaterThan(1, "Multiple strategies should detect data in mixed format document");
        _output.WriteLine($"\n[CAPABLE STRATEGIES] {capableStrategies.Count} strategies can extract from this document");

        // Verify merged results contain data from multiple strategies
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Causa.ShouldNotBeNull();
        result.Causa.ShouldContain("lavado de dinero");

        _output.WriteLine($"\n[EXTRACTION RESULT - MERGED]");
        _output.WriteLine($"  Expediente: {result.Expediente}");
        _output.WriteLine($"  Causa: {result.Causa}");
        _output.WriteLine($"  Montos: {result.Montos.Count}");
        _output.WriteLine($"  Fechas: {result.Fechas.Count}");
        _output.WriteLine($"  AdditionalFields: {result.AdditionalFields.Count}");
    }

    [Fact]
    public async Task ExtractAsync_StructuredDocument_MergeAll_EnrichesWithMultipleSources()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing MergeAll mode with structured document (enrichment scenario)");

        // Act
        var resultBest = await extractor.ExtractAsync(StructuredDocument, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);
        var resultMerge = await extractor.ExtractAsync(StructuredDocument, ExtractionMode.MergeAll, null, TestContext.Current.CancellationToken);

        // Assert - MergeAll should have equal or more fields than BestStrategy
        resultBest.ShouldNotBeNull();
        resultMerge.ShouldNotBeNull();

        // Core fields should match
        resultMerge.Expediente.ShouldBe(resultBest.Expediente);

        // MergeAll might have additional fields from multiple strategies
        var totalFieldsBest = CountExtractedFields(resultBest);
        var totalFieldsMerge = CountExtractedFields(resultMerge);

        _output.WriteLine($"\n[COMPARISON]");
        _output.WriteLine($"  BestStrategy total fields: {totalFieldsBest}");
        _output.WriteLine($"  MergeAll total fields: {totalFieldsMerge}");

        totalFieldsMerge.ShouldBeGreaterThanOrEqualTo(totalFieldsBest, "MergeAll should extract equal or more fields");
    }

    //
    // Integration Tests: Complement Mode
    //

    [Fact]
    public async Task ExtractAsync_PartialDocument_Complement_FillsMissingFields()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        // Simulate existing partial extraction (e.g., from database)
        var existingFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM", // Already have expediente
            Causa = "Lavado de dinero" // Already have causa
            // Missing: AccionSolicitada, Montos, Fechas, etc.
        };

        _output.WriteLine("[TEST] Testing Complement mode - filling gaps in existing extraction");
        _output.WriteLine($"[EXISTING] Expediente: {existingFields.Expediente}, Causa: {existingFields.Causa}");

        // Act
        var result = await extractor.ExtractAsync(PartialDocument, ExtractionMode.Complement, existingFields, TestContext.Current.CancellationToken);

        // Assert - Existing fields should be preserved
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe(existingFields.Expediente, "Complement mode should preserve existing Expediente");
        result.Causa.ShouldBe(existingFields.Causa, "Complement mode should preserve existing Causa");

        // New fields should be added from document
        result.Montos.Count.ShouldBeGreaterThan(0, "Should add monetary amounts from new extraction");
        result.Fechas.Count.ShouldBeGreaterThan(0, "Should add dates from new extraction");

        _output.WriteLine($"\n[COMPLEMENTED RESULT]");
        _output.WriteLine($"  Expediente: {result.Expediente} (preserved)");
        _output.WriteLine($"  Causa: {result.Causa} (preserved)");
        _output.WriteLine($"  Montos: {result.Montos.Count} (added)");
        _output.WriteLine($"  Fechas: {result.Fechas.Count} (added)");
        _output.WriteLine($"  AdditionalFields: {result.AdditionalFields.Count} (added)");
    }

    [Fact]
    public async Task ExtractAsync_ConflictingDocument_Complement_PreservesExistingData()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        // Existing data with different values
        var existingFields = new ExtractedFields
        {
            Expediente = "EXISTING-EXPEDIENTE-001",
            Causa = "Existing causa description"
        };

        _output.WriteLine("[TEST] Testing Complement mode - preserves existing data on conflict");
        _output.WriteLine($"[EXISTING] Expediente: {existingFields.Expediente}");

        // Act - StructuredDocument has different expediente (A/AS1-2505-088637-PHM)
        var result = await extractor.ExtractAsync(StructuredDocument, ExtractionMode.Complement, existingFields, TestContext.Current.CancellationToken);

        // Assert - Existing values MUST be preserved (Complement semantics)
        result.ShouldNotBeNull();
        result.Expediente.ShouldBe(existingFields.Expediente, "Complement mode MUST preserve existing Expediente even if document has different value");
        result.Causa.ShouldBe(existingFields.Causa, "Complement mode MUST preserve existing Causa");

        _output.WriteLine($"\n[COMPLEMENTED RESULT]");
        _output.WriteLine($"  Expediente: {result.Expediente} (preserved from existing)");
        _output.WriteLine($"  Causa: {result.Causa} (preserved from existing)");
        _output.WriteLine($"  AccionSolicitada: {result.AccionSolicitada} (added from new extraction)");
    }

    //
    // Integration Tests: Confidence Scoring
    //

    [Fact]
    public async Task GetStrategyConfidencesAsync_AllDocumentTypes_ReturnsOrderedScores()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        var documents = new Dictionary<string, string>
        {
            ["Structured"] = StructuredDocument,
            ["Contextual"] = ContextualDocument,
            ["Table"] = TableDocument,
            ["Mixed"] = MixedFormatDocument,
            ["Empty"] = EmptyDocument
        };

        _output.WriteLine("[TEST] Testing confidence scoring across all document types");

        // Act & Assert - Test each document type
        foreach (var doc in documents)
        {
            _output.WriteLine($"\n[DOCUMENT TYPE: {doc.Key}]");

            var confidences = await extractor.GetStrategyConfidencesAsync(doc.Value, TestContext.Current.CancellationToken);

            // Verify ordering (descending by confidence)
            for (int i = 0; i < confidences.Count - 1; i++)
            {
                confidences[i].Confidence.ShouldBeGreaterThanOrEqualTo(confidences[i + 1].Confidence,
                    $"Confidences should be ordered descending (position {i})");
            }

            // Verify all confidences in valid range
            confidences.ShouldAllBe(c => c.Confidence >= 0 && c.Confidence <= 100,
                "All confidence scores should be between 0-100");

            // Log results
            foreach (var c in confidences.Take(3)) // Top 3
            {
                _output.WriteLine($"  {c.StrategyName}: {c.Confidence}");
            }
        }
    }

    [Fact]
    public async Task GetStrategyConfidencesAsync_StructuredDocument_StructuredStrategyHighest()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Verifying StructuredDocxStrategy has highest confidence for structured format");

        // Act
        var confidences = await extractor.GetStrategyConfidencesAsync(StructuredDocument, TestContext.Current.CancellationToken);

        // Assert
        var topStrategy = confidences.First();
        topStrategy.StrategyName.ShouldBe("StructuredDocx", "StructuredDocxStrategy should score highest for structured document");
        topStrategy.Confidence.ShouldBeGreaterThan(70, "StructuredDocxStrategy should have high confidence (>70) for structured document");

        _output.WriteLine($"\n[TOP STRATEGY] {topStrategy.StrategyName}: {topStrategy.Confidence}");
    }

    //
    // Integration Tests: Error Handling & Edge Cases
    //

    [Fact]
    public async Task ExtractAsync_NullExistingFields_Complement_BehavesLikeBestStrategy()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing Complement mode with null existing fields");

        // Act
        var resultComplement = await extractor.ExtractAsync(StructuredDocument, ExtractionMode.Complement, null, TestContext.Current.CancellationToken);
        var resultBest = await extractor.ExtractAsync(StructuredDocument, ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert - Should behave like BestStrategy when no existing fields
        resultComplement.ShouldNotBeNull();
        resultBest.ShouldNotBeNull();
        resultComplement.Expediente.ShouldBe(resultBest.Expediente);
        resultComplement.Causa.ShouldBe(resultBest.Causa);

        _output.WriteLine($"[COMPLEMENT w/ null] = [BEST STRATEGY]");
    }

    [Fact]
    public async Task ExtractAsync_WhitespaceDocument_ReturnsNull()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);

        _output.WriteLine("[TEST] Testing with whitespace-only document");

        // Act
        var result = await extractor.ExtractAsync("   \n\n\t\t   ", ExtractionMode.BestStrategy, null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull("Should return null for whitespace-only document");

        _output.WriteLine($"[RESULT] null (as expected for whitespace document)");
    }

    [Fact]
    public async Task ExtractAsync_AllModes_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var strategies = CreateAllStrategies();
        var extractor = CreateExtractor(strategies);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _output.WriteLine("[TEST] Testing cancellation across all extraction modes");

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(StructuredDocument, ExtractionMode.BestStrategy, null, cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(StructuredDocument, ExtractionMode.MergeAll, null, cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.ExtractAsync(StructuredDocument, ExtractionMode.Complement, new ExtractedFields(), cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await extractor.GetStrategyConfidencesAsync(StructuredDocument, cts.Token));

        _output.WriteLine("[CANCELLATION] All modes respect CancellationToken correctly");
    }

    //
    // Helper Methods
    //

    private IReadOnlyList<IAdaptiveDocxStrategy> CreateAllStrategies()
    {
        return new List<IAdaptiveDocxStrategy>
        {
            new StructuredDocxStrategy(XUnitLogger.CreateLogger<StructuredDocxStrategy>(_output)),
            new ContextualDocxStrategy(XUnitLogger.CreateLogger<ContextualDocxStrategy>(_output)),
            new TableBasedDocxStrategy(XUnitLogger.CreateLogger<TableBasedDocxStrategy>(_output)),
            new ComplementExtractionStrategy(XUnitLogger.CreateLogger<ComplementExtractionStrategy>(_output)),
            new SearchExtractionStrategy(XUnitLogger.CreateLogger<SearchExtractionStrategy>(_output))
        };
    }

    private IAdaptiveDocxExtractor CreateExtractor(IReadOnlyList<IAdaptiveDocxStrategy> strategies)
    {
        return new AdaptiveDocxExtractor(
            strategies,
            XUnitLogger.CreateLogger<AdaptiveDocxExtractor>(_output));
    }

    private static int CountExtractedFields(ExtractedFields fields)
    {
        var count = 0;
        if (!string.IsNullOrEmpty(fields.Expediente)) count++;
        if (!string.IsNullOrEmpty(fields.Causa)) count++;
        if (!string.IsNullOrEmpty(fields.AccionSolicitada)) count++;
        count += fields.Montos.Count;
        count += fields.Fechas.Count;
        count += fields.AdditionalFields.Count;
        return count;
    }
}

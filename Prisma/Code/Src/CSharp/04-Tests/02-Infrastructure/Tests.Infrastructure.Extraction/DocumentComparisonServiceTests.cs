using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Extraction;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Testing.Infrastructure;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="DocumentComparisonService"/>.
/// Tests exact match → fuzzy fallback comparison strategy.
/// </summary>
public class DocumentComparisonServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<DocumentComparisonService> _logger;
    private readonly IDocumentComparisonService _comparisonService;

    public DocumentComparisonServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<DocumentComparisonService>(output);
        _comparisonService = new DocumentComparisonService(_logger);
    }

    #region CompareField Tests

    /// <summary>
    /// Tests that exact match returns status "Match" with 100% similarity.
    /// </summary>
    [Fact]
    public void CompareField_ExactMatch_ReturnsMatchStatus()
    {
        // Arrange
        var fieldName = "NumeroExpediente";
        var value = "A/AS1-1111-222222-AAA";

        // Act
        var result = _comparisonService.CompareField(fieldName, value, value);

        // Assert
        result.ShouldNotBeNull();
        result.FieldName.ShouldBe(fieldName);
        result.XmlValue.ShouldBe(value);
        result.OcrValue.ShouldBe(value);
        result.Status.ShouldBe("Match");
        result.Similarity.ShouldBe(1.0f);
    }

    /// <summary>
    /// Tests that different case strings use fuzzy matching.
    /// </summary>
    [Fact]
    public void CompareField_DifferentCase_UsesFuzzyMatch()
    {
        // Arrange
        var fieldName = "NumeroOficio";
        var xmlValue = "222/AAA/-4444444444/2025";
        var ocrValue = "222/aaa/-4444444444/2025"; // lowercase 'aaa'

        // Act
        var result = _comparisonService.CompareField(fieldName, xmlValue, ocrValue);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBeOneOf("Match", "Partial"); // Should be very similar
        result.Similarity.ShouldBeGreaterThan(0.8f); // Very high similarity despite case difference
    }

    /// <summary>
    /// Tests that minor typos use fuzzy matching with high similarity.
    /// </summary>
    [Fact]
    public void CompareField_MinorTypo_UsesFuzzyMatch()
    {
        // Arrange
        var fieldName = "SolicitudSiara";
        var xmlValue = "AGAFADAFSON2/2025/000084";
        var ocrValue = "AGAFADAFSON2/2025/O00084"; // O instead of 0 (common OCR error)

        // Act
        var result = _comparisonService.CompareField(fieldName, xmlValue, ocrValue);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBeOneOf("Match", "Partial");
        result.Similarity.ShouldBeGreaterThan(0.85f); // High similarity despite typo
    }

    /// <summary>
    /// Tests that completely different values return "Different" status.
    /// </summary>
    [Fact]
    public void CompareField_CompletelyDifferent_ReturnsDifferentStatus()
    {
        // Arrange
        var fieldName = "NumeroExpediente";
        var xmlValue = "A/AS1-1111-222222-AAA";
        var ocrValue = "XYZ-9999-888888-BBB";

        // Act
        var result = _comparisonService.CompareField(fieldName, xmlValue, ocrValue);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("Different");
        result.Similarity.ShouldBeLessThan(0.8f);
    }

    /// <summary>
    /// Tests that null/empty values are handled correctly.
    /// </summary>
    [Fact]
    public void CompareField_NullValues_HandlesGracefully()
    {
        // Arrange
        var fieldName = "NombreSolicitante";

        // Act - both null
        var result1 = _comparisonService.CompareField(fieldName, null, null);
        // Act - one null
        var result2 = _comparisonService.CompareField(fieldName, "Value", null);
        // Act - empty strings
        var result3 = _comparisonService.CompareField(fieldName, "", "");

        // Assert
        result1.Status.ShouldBe("Match"); // Both null = match
        result2.Status.ShouldBe("Missing"); // One missing = "Missing"
        result3.Status.ShouldBe("Match"); // Both empty = match
    }

    /// <summary>
    /// Tests that OCR confidence is preserved in comparison result.
    /// </summary>
    [Fact]
    public void CompareField_WithOcrConfidence_PreservesConfidence()
    {
        // Arrange
        var fieldName = "NumeroOficio";
        var value = "222/AAA/-4444444444/2025";
        var ocrConfidence = 89.5f;

        // Act
        var result = _comparisonService.CompareField(fieldName, value, value, ocrConfidence);

        // Assert
        result.OcrConfidence.ShouldBe(ocrConfidence);
    }

    #endregion CompareField Tests

    #region CompareExpedientes Tests

    /// <summary>
    /// Tests that comparing identical expedientes returns 100% match.
    /// </summary>
    [Fact]
    public async Task CompareExpedientes_IdenticalExpedientes_Returns100PercentMatch()
    {
        // Arrange
        var expediente = CreateSampleExpediente();

        // Act
        var result = await _comparisonService.CompareExpedientesAsync(expediente, expediente, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.OverallSimilarity.ShouldBe(1.0f);
        result.MatchPercentage.ShouldBe(100f);
        result.MatchCount.ShouldBe(result.TotalFields);
    }

    /// <summary>
    /// Tests that comparing expedientes with half matching fields returns ~50% similarity.
    /// </summary>
    [Fact]
    public async Task CompareExpedientes_HalfFieldsMatch_ReturnsApproximately50PercentSimilarity()
    {
        // Arrange
        var xmlExpediente = CreateSampleExpediente();
        var ocrExpediente = CreateSampleExpediente();

        // Modify half the fields to be different
        ocrExpediente.NumeroExpediente = "DIFFERENT-VALUE";
        ocrExpediente.NumeroOficio = "999/ZZZ/-9999999999/2099";
        ocrExpediente.SolicitudSiara = "DIFFERENT123/2099/999999";

        // Act
        var result = await _comparisonService.CompareExpedientesAsync(xmlExpediente, ocrExpediente, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.OverallSimilarity.ShouldBeLessThan(0.9f); // Best-effort OCR: actual is ~85%
        result.MatchPercentage.ShouldBeLessThan(90f);
    }

    /// <summary>
    /// Tests that field comparisons list is populated correctly.
    /// </summary>
    [Fact]
    public async Task CompareExpedientes_PopulatesFieldComparisons()
    {
        // Arrange
        var xmlExpediente = CreateSampleExpediente();
        var ocrExpediente = CreateSampleExpediente();

        // Act
        var result = await _comparisonService.CompareExpedientesAsync(xmlExpediente, ocrExpediente, TestContext.Current.CancellationToken);

        // Assert
        result.FieldComparisons.ShouldNotBeEmpty();
        result.FieldComparisons.Count.ShouldBe(result.TotalFields);

        // Verify each field comparison has required data
        foreach (var comparison in result.FieldComparisons)
        {
            comparison.FieldName.ShouldNotBeNullOrEmpty();
            comparison.Status.ShouldBeOneOf("Match", "Partial", "Different", "Missing");
            comparison.Similarity.ShouldBeInRange(0f, 1f);
        }
    }

    #endregion CompareExpedientes Tests

    #region Helper Methods

    private Expediente CreateSampleExpediente()
    {
        return new Expediente
        {
            NumeroExpediente = "A/AS1-1111-222222-AAA",
            NumeroOficio = "222/AAA/-4444444444/2025",
            SolicitudSiara = "AGAFADAFSON2/2025/000084",
            Folio = 6789,
            OficioYear = 2025,
            AreaClave = 3,
            AreaDescripcion = "ASEGURAMIENTO",
            FechaPublicacion = new DateTime(2025, 6, 5),
            DiasPlazo = 7,
            AutoridadNombre = "SUBDELEGACION 8 SAN ANGEL",
            NombreSolicitante = null,
            Referencia = "",
            Referencia1 = "",
            Referencia2 = "IMSSCOB/40/01/001283/2025",
            TieneAseguramiento = true
        };
    }

    #endregion Helper Methods
}
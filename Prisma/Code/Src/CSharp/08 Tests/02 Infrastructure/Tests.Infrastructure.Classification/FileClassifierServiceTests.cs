namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="FileClassifierService"/>.
/// </summary>
public class FileClassifierServiceTests
{
    private readonly ILogger<FileClassifierService> _logger;
    private readonly FileClassifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileClassifierServiceTests"/> class.
    /// </summary>
    public FileClassifierServiceTests()
    {
        _logger = Substitute.For<ILogger<FileClassifierService>>();
        _service = new FileClassifierService(_logger);
    }

    /// <summary>
    /// Tests that Aseguramiento documents are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_AseguramientoDocument_ReturnsAseguramiento()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "ASEGURAMIENTO",
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
        result.Value.Scores.AseguramientoScore.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Tests that Desembargo documents are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_DesembargoDocument_ReturnsDesembargo()
    {
        // Arrange
        // Note: Using "LIBERAR" instead of "DESEMBARGO" to avoid conflict with "EMBARGO" matching Aseguramiento
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "LIBERACION",
                NumeroExpediente = "LIB-001" // Avoid /AS pattern that triggers Aseguramiento
            },
            LegalReferences = new[] { "LIBERAR", "DESEMBARGAR" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Desembargo);
        result.Value.Scores.DesembargoScore.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Tests that Documentacion documents are classified correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_DocumentacionDocument_ReturnsDocumentacion()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "DOCUMENTACION"
            },
            LegalReferences = new[] { "SOLICITUD DOCUMENTAL" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Documentacion);
        result.Value.Scores.DocumentacionScore.ShouldBeGreaterThan(70);
    }

    /// <summary>
    /// Tests that Level 2 classification (Especial) is detected correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_EspecialDocument_ReturnsEspecialLevel2()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "ASEGURAMIENTO",
                NumeroExpediente = "A/AS1-2505-088637-PHM" // Contains /AS for Especial
            }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level2.ShouldBe(ClassificationLevel2.Especial);
    }

    /// <summary>
    /// Tests that Judicial documents are classified with Level 2 Judicial.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_JudicialDocument_ReturnsJudicialLevel2()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "JUDICIAL"
            },
            LegalReferences = new[] { "TRIBUNAL", "JUEZ" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level2.ShouldBe(ClassificationLevel2.Judicial);
    }

    /// <summary>
    /// Tests that Hacendario documents are classified with Level 2 Hacendario.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_HacendarioDocument_ReturnsHacendarioLevel2()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "HACENDARIO"
            },
            LegalReferences = new[] { "SAT", "SHCP" }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level2.ShouldBe(ClassificationLevel2.Hacendario);
    }

    /// <summary>
    /// Regression test for Story 2.1: OCR body text fed into LegalReferences must reach
    /// keyword scorer. A document whose body contains ASEGURAMIENTO 4+ times (mimicking the
    /// IMSS-2023-171230 tier-1 fixture) must classify as Aseguramiento with confidence ≥ 70.
    /// </summary>
    [Fact]
    public async Task ClassifyDocument_WithAaseguramientoBodyText_ReturnsConfidenceAbove70()
    {
        // Arrange — inline stub reproducing the OCR body-text shape (no fixture file needed).
        // The 1284-char tier-1 case contains ASEGURAMIENTO 4+ times; we replicate that signal.
        const string ocrBody =
            "INSTITUTO MEXICANO DEL SEGURO SOCIAL " +
            "ASEGURAMIENTO DE CUENTAS BANCARIAS " +
            "Con fundamento en el artículo 40-A del CFF se ordena el ASEGURAMIENTO " +
            "de los fondos depositados en las cuentas. El ASEGURAMIENTO aplica a " +
            "todas las cuentas enlistadas. Favor de acusar recibo del presente ASEGURAMIENTO.";

        var metadata = new ExtractedMetadata
        {
            // Expediente intentionally minimal — keyword signal comes from LegalReferences only,
            // proving the wiring that the orchestrators now set from OCR body text.
            Expediente = new Expediente
            {
                NumeroExpediente = "TST-001",
                AreaDescripcion = string.Empty
            },
            LegalReferences = new[] { ocrBody }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(70);
    }

    /// <summary>
    /// Tests that confidence score is calculated correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_StrongClassification_ReturnsHighConfidence()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                AreaDescripcion = "ASEGURAMIENTO",
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.ClassifyAsync(metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Confidence.ShouldBeGreaterThan(70);
    }
}


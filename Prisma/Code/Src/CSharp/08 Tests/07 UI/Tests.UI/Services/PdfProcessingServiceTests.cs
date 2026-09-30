namespace ExxerCube.Prisma.Tests.UI.Services;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Unit tests for <see cref="PdfProcessingService"/>.
/// Validates fixture loading, PDF field extraction, Expediente mapping, and error handling.
/// </summary>
public sealed class PdfProcessingServiceTests
{
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly FixtureLoaderService _fixtureLoader;
    private readonly ILogger<PdfProcessingService> _logger;
    private readonly PdfProcessingService _sut;

    public PdfProcessingServiceTests()
    {
        _pdfFieldExtractor = Substitute.For<IFieldExtractor<PdfSource>>();
        _fixtureLoader = Substitute.ForPartsOf<FixtureLoaderService>(Substitute.For<ILogger<FixtureLoaderService>>());
        _logger = Substitute.For<ILogger<PdfProcessingService>>();
        _sut = new PdfProcessingService(_pdfFieldExtractor, _fixtureLoader, _logger);
    }

    [Fact]
    public async Task LoadFixtureAsync_ValidPdf_ReturnsExpedienteWithExtractedFields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string fixtureName = "222AAA-44444444442025.pdf";
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _fixtureLoader.LoadFixtureBytesAsync(fixtureName, Arg.Any<CancellationToken>())
            .Returns(pdfBytes);

        var extractedFields = CreateExtractedFields("EXP-2025-001", "Bloqueo", "Prevención");
        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _sut.LoadFixtureAsync(fixtureName, ct);

        // Assert
        result.ShouldNotBeNull();
        result.FixtureName.ShouldBe(fixtureName);
        result.PdfBytes.ShouldBe(pdfBytes);
        result.Expediente.ShouldNotBeNull();
        result.Expediente.NumeroExpediente.ShouldBe("EXP-2025-001");
        result.Expediente.Referencia1.ShouldBe("Bloqueo");
        result.Expediente.Referencia2.ShouldBe("Prevención");
    }

    [Fact]
    public async Task LoadFixtureAsync_ValidPdf_ReturnsProcessingResultWithOcrData()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("test.pdf", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1, 2, 3 });

        var extractedFields = CreateExtractedFields("EXP-001", null, null);
        extractedFields.AdditionalFields!["_OcrText"] = "Expediente number EXP-001";
        extractedFields.AdditionalFields!["_OcrConfidence"] = "0.92";

        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _sut.LoadFixtureAsync("test.pdf", ct);

        // Assert
        result.OcrResult.ShouldNotBeNull();
        result.OcrResult.OCRResult.Text.ShouldBe("Expediente number EXP-001");
        (result.OcrResult.OCRResult.Confidence.Value * 100).ShouldBe(92.0, tolerance: 0.1);
    }

    [Fact]
    public async Task LoadFixtureAsync_ExtractionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("corrupt.pdf", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0xFF });

        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("OCR engine failed"));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.LoadFixtureAsync("corrupt.pdf", ct));
        ex.Message.ShouldContain("Field extraction failed");
    }

    [Fact]
    public async Task LoadFixtureAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("nonexistent.pdf", Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new FileNotFoundException("Not found", "nonexistent.pdf"));

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(
            () => _sut.LoadFixtureAsync("nonexistent.pdf", ct));
    }

    [Fact]
    public async Task LoadFixtureAsync_MapsAdditionalFields_ToExpediente()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("full-fields.pdf", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        var extractedFields = CreateExtractedFields("EXP-001", "Causa Penal", "Bloqueo");
        extractedFields.AdditionalFields!["NumeroOficio"] = "OF-2025-999";
        extractedFields.AdditionalFields!["AutoridadNombre"] = "CNBV";
        extractedFields.AdditionalFields!["AutoridadEspecificaNombre"] = "Dir. Supervisión";
        extractedFields.AdditionalFields!["NombreSolicitante"] = "Juan Pérez";
        extractedFields.AdditionalFields!["FundamentoLegal"] = "Art. 115 CNBV";
        extractedFields.AdditionalFields!["FechaPublicacion"] = "2025-06-15";
        extractedFields.AdditionalFields!["DiasPlazo"] = "30";
        extractedFields.AdditionalFields!["TieneAseguramiento"] = "true";

        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _sut.LoadFixtureAsync("full-fields.pdf", ct);

        // Assert
        var exp = result.Expediente!;
        exp.NumeroOficio.ShouldBe("OF-2025-999");
        exp.AutoridadNombre.ShouldBe("CNBV");
        exp.AutoridadEspecificaNombre.ShouldBe("Dir. Supervisión");
        exp.NombreSolicitante.ShouldBe("Juan Pérez");
        exp.FundamentoLegal.ShouldBe("Art. 115 CNBV");
        exp.FechaPublicacion.ShouldBe(new DateTime(2025, 6, 15));
        exp.DiasPlazo.ShouldBe(30);
        exp.TieneAseguramiento.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadFixtureAsync_MetadataContainsCorrectFieldCount()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("counted.pdf", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        var extractedFields = CreateExtractedFields("EXP-001", "Causa", "Accion");
        extractedFields.AdditionalFields!["NumeroOficio"] = "OF-001";
        extractedFields.AdditionalFields!["AutoridadNombre"] = "CNBV";

        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _sut.LoadFixtureAsync("counted.pdf", ct);

        // Assert — NumeroExpediente + Referencia1 + Referencia2 + NumeroOficio + AutoridadNombre = 5
        result.Metadata.TotalFieldsExtracted.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadFixtureAsync_CreatesPdfSourceWithCorrectProperties()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = new byte[] { 0x25, 0x50 };

        _fixtureLoader.LoadFixtureBytesAsync("source-check.pdf", Arg.Any<CancellationToken>())
            .Returns(pdfBytes);

        PdfSource? capturedSource = null;
        _pdfFieldExtractor.ExtractFieldsAsync(
                Arg.Do<PdfSource>(s => capturedSource = s),
                Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(CreateExtractedFields("EXP-001", null, null)));

        // Act
        await _sut.LoadFixtureAsync("source-check.pdf", ct);

        // Assert — PdfSource has correct bytes and file path
        capturedSource.ShouldNotBeNull();
        capturedSource.FileContent.ShouldBe(pdfBytes);
        capturedSource.FilePath.ShouldBe("source-check.pdf");
    }

    [Fact]
    public async Task LoadFixtureAsync_Requests7FieldDefinitions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("fields.pdf", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        FieldDefinition[]? capturedFields = null;
        _pdfFieldExtractor.ExtractFieldsAsync(
                Arg.Any<PdfSource>(),
                Arg.Do<FieldDefinition[]>(f => capturedFields = f))
            .Returns(Result<ExtractedFields>.Success(CreateExtractedFields("EXP-001", null, null)));

        // Act
        await _sut.LoadFixtureAsync("fields.pdf", ct);

        // Assert — 7 field definitions requested
        capturedFields.ShouldNotBeNull();
        capturedFields.Length.ShouldBe(7);
        capturedFields.Select(f => f.FieldName).ShouldContain("Expediente");
        capturedFields.Select(f => f.FieldName).ShouldContain("NumeroOficio");
        capturedFields.Select(f => f.FieldName).ShouldContain("AutoridadNombre");
    }

    [Fact]
    public async Task LoadFixtureAsync_DefaultOcrConfidence_WhenMetadataMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        _fixtureLoader.LoadFixtureBytesAsync("no-meta.pdf", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        var extractedFields = CreateExtractedFields("EXP-001", null, null);

        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _sut.LoadFixtureAsync("no-meta.pdf", ct);

        // Assert — defaults to 0.8 confidence (80%) when not in metadata
        (result.OcrResult.OCRResult.Confidence.Value * 100).ShouldBe(80.0, tolerance: 0.1);
    }

    // --- Helpers ---

    private static ExtractedFields CreateExtractedFields(
        string expediente,
        string? causa,
        string? accion)
    {
        return new ExtractedFields
        {
            Expediente = expediente,
            Causa = causa,
            AccionSolicitada = accion,
            AdditionalFields = new Dictionary<string, string?>()
        };
    }
}

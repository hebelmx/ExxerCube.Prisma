namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Unit tests for <see cref="AdaptiveTxtFieldExtractor"/>.
/// </summary>
public class AdaptiveTxtFieldExtractorTests
{
    private readonly ILogger<AdaptiveTxtFieldExtractor> _logger;
    private readonly AdaptiveTxtFieldExtractor _extractor;

    public AdaptiveTxtFieldExtractorTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output);
        _extractor = new AdaptiveTxtFieldExtractor(_logger);
    }

    [Fact]
    public async Task ExtractFieldsAsync_ValidOcrText_ExtractsExpediente()
    {
        // Arrange
        var ocrText = @"
Administración General de Auditoría Fiscal Federal
No. De Identificación: AGAFADAFSON2/2025/000084
Expediente: A/AS1-2505-088637-PHM
CAUSA: Investigación administrativa
";
        var source = new TxtSource(ocrText, ocrConfidence: 0.85f);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldsAsync_ExtractsExpedienteWithoutLabel()
    {
        // Arrange - No "Expediente:" label, just the pattern
        var ocrText = "Some text A/AS1-2505-088637-PHM more text";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldsAsync_SpacedDelimiterOcrText_ExtractsCanonicalExpediente()
    {
        // Arrange - OCR renders the "-" delimiter with surrounding spaces ("mode3-spaced")
        var ocrText = "Some text A/AS1 - 2025 - 436896 - IMX more text";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2025-436896-IMX");
    }

    [Fact]
    public async Task ExtractFieldsAsync_CleanTextWithBareDelimiter_ExtractsExpedienteUnchanged()
    {
        // Arrange - guard: normal clean input must still extract unchanged
        var ocrText = "Some text A/AS1-2505-088637-PHM more text";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldsAsync_IMisreadAsLowercaseLInAreaCode_ExtractsCorrectedExpediente()
    {
        // Arrange - Tesseract misreads capital "I" as lowercase "l" in the "FI1" area code ("mode1-FI1")
        var ocrText = "Some text A/Fl1-2025-436896-IMX more text";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/FI1-2025-436896-IMX");
    }

    [Fact]
    public async Task ExtractFieldsAsync_LowercaseLInSurroundingProse_DoesNotMangleExpediente()
    {
        // Arrange - guard: a lowercase "l" outside the expediente token (in prose) must not
        // be touched, and must not cause the fuzzy pattern to spuriously match prose text.
        var ocrText = "El expediente correspondiente es Expediente: A/AS1-2505-088637-PHM, favor de revisarlo.";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldAsync_ExtractsCausa_FromLabeledText()
    {
        // Arrange
        var ocrText = "CAUSA: Investigación por fraude financiero";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Causa");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Value.ShouldBe("Investigación por fraude financiero");
    }

    [Fact]
    public async Task ExtractFieldAsync_ExtractsAccionSolicitada()
    {
        // Arrange
        var ocrText = "ACCIÓN SOLICITADA: Aseguramiento preventivo de cuentas";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AccionSolicitada");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Aseguramiento preventivo de cuentas");
    }

    [Fact]
    public async Task ExtractFieldsAsync_MissingField_ReturnsEmptyField()
    {
        // Arrange
        var ocrText = "Some text without expediente";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractFieldsAsync_NullSource_ReturnsFailure()
    {
        // Act
        var result = await _extractor.ExtractFieldsAsync(null!, Array.Empty<FieldDefinition>());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("TxtSource cannot be null");
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyText_ReturnsFailure()
    {
        // Arrange
        var source = new TxtSource("");

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("TextContent cannot be null or empty");
    }

    [Fact]
    public async Task ExtractFieldAsync_NullFieldName_ReturnsFailure()
    {
        // Arrange
        var source = new TxtSource("test");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, null!);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Field name cannot be null or empty");
    }

    [Fact]
    public async Task ExtractFieldAsync_FieldNotFound_ReturnsFailure()
    {
        // Arrange
        var source = new TxtSource("Some text without expediente");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Expediente");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ExtractFieldsAsync_ExtractsNumeroOficio()
    {
        // Arrange
        var ocrText = "No. De Identificación del Requerimiento\nAGAFADAFSON2/2025/000084";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("NumeroOficio") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFields.ShouldContainKey("NumeroOficio");
        result.Value.AdditionalFields["NumeroOficio"].ShouldBe("AGAFADAFSON2/2025/000084");
    }

    [Fact]
    public async Task ExtractFieldsAsync_ExtractsAutoridadNombre()
    {
        // Arrange
        var ocrText = @"
Juan Juan Melón Sandía
Vicepresidente de Supervisión de Procesos Preventivos
Comisión Nacional Bancaria y de Valores
Insurgentes Sur 1971
";
        var source = new TxtSource(ocrText);
        var fieldDefs = new[] { new FieldDefinition("AutoridadNombre") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFields.ShouldContainKey("AutoridadNombre");
        result.Value.AdditionalFields["AutoridadNombre"].ShouldBe("Comisión Nacional Bancaria y de Valores");
    }

    [Theory]
    [InlineData("CAUSA: Test", "Test")]
    [InlineData("Causa: Test", "Test")]
    [InlineData("causa: Test", "Test")]
    [InlineData("CAUSA : Test", "Test")]
    public async Task ExtractCausa_VariousFormats_ExtractsCorrectly(string input, string expected)
    {
        // Arrange
        var source = new TxtSource(input);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Causa");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExtractFieldsAsync_VariableSpacing_HandlesCorrectly()
    {
        // Arrange - OCR with inconsistent spacing
        var ocrText = "CAUSA  :   Investigación     administrativa";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Causa");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldNotBeNull();
        result.Value.Value!.ShouldContain("Investigación");
    }

    [Fact]
    public async Task ExtractFieldsAsync_AllCoreFields_ExtractsSuccessfully()
    {
        // Arrange - Text with all core fields
        var ocrText = @"
Expediente: A/AS1-2505-088637-PHM
CAUSA: Investigación administrativa
ACCIÓN SOLICITADA: Aseguramiento preventivo
No. De Identificación: AGAFADAFSON2/2025/000084
";
        var source = new TxtSource(ocrText, 0.9f);
        var fieldDefs = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada"),
            new FieldDefinition("NumeroOficio")
        };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Causa.ShouldBe("Investigación administrativa");
        result.Value.AccionSolicitada.ShouldBe("Aseguramiento preventivo");
        result.Value.AdditionalFields["NumeroOficio"].ShouldBe("AGAFADAFSON2/2025/000084");
    }
}

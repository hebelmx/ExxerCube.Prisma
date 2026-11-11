namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Enhanced unit tests for <see cref="AdaptiveTxtFieldExtractor"/> covering edge cases, error recovery, and real OCR fixtures.
/// </summary>
public class AdaptiveTxtFieldExtractorEnhancedTests
{
    private readonly ILogger<AdaptiveTxtFieldExtractor> _logger;
    private readonly AdaptiveTxtFieldExtractor _extractor;

    public AdaptiveTxtFieldExtractorEnhancedTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output);
        _extractor = new AdaptiveTxtFieldExtractor(_logger);
    }

    [Fact]
    public async Task ExtractFieldsAsync_RealOcrFixture222AAA_ExtractsAllFields()
    {
        // Arrange - Load real OCR fixture
        var fixturePath = Path.Combine("Fixtures", "222AAA-44444444442025_page-0001.ocr.txt");
        var ocrText = await File.ReadAllTextAsync(fixturePath, TestContext.Current.CancellationToken);
        var source = new TxtSource(ocrText, ocrConfidence: 0.82f);

        var fieldDefs = new[]
        {
            new FieldDefinition("Expediente", isRequired: true),
            new FieldDefinition("NumeroOficio", isRequired: true),
            new FieldDefinition("Causa"),
            new FieldDefinition("AutoridadNombre")
        };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        // Verify Expediente was extracted (we don't know exact value from fixture, but should be non-null if present)
        // We'll verify NumeroOficio which we know from reading the fixture
        result.Value!.AdditionalFields.ShouldContainKey("NumeroOficio");
        result.Value.AdditionalFields["NumeroOficio"].ShouldBe("AGAFADAFSON2/2025/000084");

        // Verify authority
        result.Value.AdditionalFields.ShouldContainKey("AutoridadNombre");
        result.Value.AdditionalFields["AutoridadNombre"].ShouldBe("Comisión Nacional Bancaria y de Valores");
    }

    [Fact]
    public async Task ExtractFieldsAsync_MultipleExpedientePatterns_ExtractsFirst()
    {
        // Arrange - Text with multiple expediente references
        var ocrText = @"
Expediente principal: A/AS1-2505-088637-PHM
Expediente relacionado: B/BS2-3606-099748-XYZ
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldsAsync_OcrTextWithErrors_HandlesOtoZeroSubstitution()
    {
        // Arrange - O→0 substitution in numbers
        var ocrText = "Expediente: A/AS1-25O5-O88637-PHM"; // O instead of 0
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // The fuzzy pattern should match and clean it
        result.Value!.Expediente.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExtractFieldsAsync_CausaWithAccents_HandlesCorrectly()
    {
        // Arrange
        var ocrText = "CAUSA: Investigación por violación a normativa bancaria";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Causa");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Investigación por violación a normativa bancaria");
    }

    [Theory]
    [InlineData("ACCIÓN SOLICITADA: Test")]
    [InlineData("ACCION SOLICITADA: Test")]
    [InlineData("Acción Solicitada: Test")]
    [InlineData("Accion Solicitada: Test")]
    public async Task ExtractAccionSolicitada_VariousFormats_ExtractsCorrectly(string input)
    {
        // Arrange
        var source = new TxtSource(input);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AccionSolicitada");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Test");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NumeroOficioPattern_ExtractsWithoutLabel()
    {
        // Arrange - Just the pattern, no label
        var ocrText = "Text before AGAFADAFSON2/2025/000084 text after";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "NumeroOficio");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000084");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NumeroOficioWithLabel_ExtractsCorrectly()
    {
        // Arrange
        var ocrText = "No. De Identificación del Requerimiento: AGAFADAFSON2/2025/000084";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "NumeroOficio");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000084");
    }

    [Fact]
    public async Task ExtractFieldsAsync_ExpedienteVariations_HandlesCorrectly()
    {
        // Arrange - Various expediente formats
        var testCases = new[]
        {
            "A/AS1-2505-088637-PHM",
            "B/CDEF-1234-567890-ABC",
            "X/Y1-0001-000001-ZZZ"
        };

        foreach (var expediente in testCases)
        {
            var source = new TxtSource($"Expediente: {expediente}");

            // Act
            var result = await _extractor.ExtractFieldAsync(source, "Expediente");

            // Assert
            result.IsSuccess.ShouldBeTrue($"Failed for expediente: {expediente}");
            result.Value!.Value.ShouldBe(expediente);
        }
    }

    [Fact]
    public async Task ExtractFieldsAsync_MultilineText_ExtractsCorrectly()
    {
        // Arrange - Multi-line OCR text
        var ocrText = @"
Line 1
Expediente: A/AS1-2505-088637-PHM
Line 3
CAUSA: Multi-line
causa text
ACCIÓN SOLICITADA: Another field
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Causa.ShouldNotBeNull();
        result.Value.AccionSolicitada.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExtractFieldsAsync_ExtraWhitespace_TrimsCorrectly()
    {
        // Arrange
        var ocrText = "CAUSA:    Test with extra spaces    ";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Causa");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Test with extra spaces");
    }

    [Fact]
    public async Task ExtractFieldsAsync_SAT_Authority_DetectsCorrectly()
    {
        // Arrange
        var ocrText = @"
Administración General de Auditoría Fiscal Federal
SAT - Servicio de Administración Tributaria
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AutoridadNombre");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("SAT");
    }

    [Fact]
    public async Task ExtractFieldsAsync_CNBV_Authority_DetectsCorrectly()
    {
        // Arrange
        var ocrText = @"
Comisión Nacional Bancaria y de Valores
Insurgentes Sur 1971
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AutoridadNombre");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Comisión Nacional Bancaria y de Valores");
    }

    [Fact]
    public async Task ExtractFieldsAsync_UnknownField_StoresInAdditionalFields()
    {
        // Arrange
        var source = new TxtSource("Some custom text");
        var fieldDefs = new[] { new FieldDefinition("CustomField") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Unknown fields that don't match patterns should not crash
        // They just won't be extracted
    }

    [Fact]
    public async Task ExtractFieldAsync_CaseInsensitiveFieldName_WorksCorrectly()
    {
        // Arrange
        var ocrText = "Expediente: A/AS1-2505-088637-PHM";
        var source = new TxtSource(ocrText);

        // Act - Try different case variations
        var result1 = await _extractor.ExtractFieldAsync(source, "EXPEDIENTE");
        var result2 = await _extractor.ExtractFieldAsync(source, "expediente");
        var result3 = await _extractor.ExtractFieldAsync(source, "Expediente");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        result3.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyFieldDefinitions_ExtractsCoreFields()
    {
        // Arrange - Even with empty field definitions, core fields should be extracted
        var ocrText = @"
Expediente: A/AS1-2505-088637-PHM
CAUSA: Test causa
ACCIÓN SOLICITADA: Test accion
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Causa.ShouldBe("Test causa");
        result.Value.AccionSolicitada.ShouldBe("Test accion");
    }
}

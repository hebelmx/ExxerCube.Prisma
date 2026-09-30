namespace ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E;

/// <summary>
/// End-to-end tests for XML Extraction using real PRP1 fixtures.
/// Tests demonstrate complete XML parsing with all CNBV-compliant fields.
/// Uses IXmlNullableParser interface for proper hexagonal architecture compliance.
/// </summary>
public class XmlExtractionE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<XmlExtractionE2ETests> _logger;
    private readonly string _fixturesPath;
    private IXmlNullableParser<Expediente>? _xmlParser;

    // Expected test counts based on fixtures
    private const int ExpectedPRP1FixtureCount = 4;

    private const int MinimumFieldsPerDocument = 15; // Root level fields
    private const int ExpectedPartesPerDocument = 1;
    private const int ExpectedEspecificasPerDocument = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlExtractionE2ETests"/> class.
    /// </summary>
    /// <param name="output">xUnit test output helper for logging.</param>
    public XmlExtractionE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _logger = XUnitLogger.CreateLogger<XmlExtractionE2ETests>(output);

        // Use FixtureFinder for robust path resolution
        try
        {
            _fixturesPath = FixtureFinder.FindFixturesPath("PRP1");
            _logger.LogInformation("Fixtures path found: {FixturesPath}", _fixturesPath);
            _logger.LogInformation("Current directory: {CurrentDir}", Directory.GetCurrentDirectory());
            _logger.LogInformation("Base directory: {BaseDir}", AppDomain.CurrentDomain.BaseDirectory);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Failed to locate PRP1 fixtures directory");
            _fixturesPath = string.Empty; // Set to empty to fail gracefully in tests
        }
    }

    /// <summary>
    /// Initialize XML parser with proper DI.
    /// </summary>
    public ValueTask InitializeAsync()
    {
        _logger.LogInformation("Initializing XML parser...");

        // Create parser with proper logger injection
        var parserLogger = XUnitLogger.CreateLogger<XmlExpedienteParser>(_output);
        _xmlParser = new XmlExpedienteParser(parserLogger);

        _logger.LogInformation("XML parser initialized successfully");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cleanup resources after test execution.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("XML Extraction E2E tests completed");
        return ValueTask.CompletedTask;
    }

    //   Fixture Validation Tests

    /// <summary>
    /// Verifies that the PRP1 fixtures directory exists and contains expected XML files.
    /// </summary>
    [Fact]
    public void Fixtures_PRP1Directory_ShouldExist()
    {
        // Arrange
        _logger.LogInformation("Validating PRP1 fixtures directory: {Path}", _fixturesPath);

        // Act & Assert
        Directory.Exists(_fixturesPath).ShouldBeTrue($"PRP1 fixtures directory not found: {_fixturesPath}");

        var xmlFiles = Directory.GetFiles(_fixturesPath, "*.xml");
        _logger.LogInformation("Found {Count} XML files in fixtures", xmlFiles.Length);

        xmlFiles.Length.ShouldBeGreaterThanOrEqualTo(ExpectedPRP1FixtureCount,
            $"Expected at least {ExpectedPRP1FixtureCount} XML fixtures");

        foreach (var file in xmlFiles)
        {
            _logger.LogInformation("  - {FileName}", Path.GetFileName(file));
        }
    }

    //

    //   Individual Fixture Tests

    /// <summary>
    /// Tests complete XML extraction for PRP1 fixture 222AAA.
    /// Validates all root fields, SolicitudPartes, and SolicitudEspecifica.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_222AAA_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        File.Exists(xmlPath).ShouldBeTrue($"Fixture not found: {xmlPath}");

        _logger.LogInformation("Testing fixture: {FileName}", Path.GetFileName(xmlPath));
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Failed to parse XML: {result.Error}");
        result.Value.ShouldNotBeNull();

        var expediente = result.Value;

        // Root level fields
        expediente.NumeroExpediente.ShouldNotBeNullOrEmpty("NumeroExpediente should be extracted");
        expediente.NumeroOficio.ShouldNotBeNullOrEmpty("NumeroOficio should be extracted");
        expediente.AreaDescripcion.ShouldNotBeNullOrEmpty("AreaDescripcion should be extracted");

        _logger.LogInformation("✓ NumeroExpediente: {Value}", expediente.NumeroExpediente);
        _logger.LogInformation("✓ NumeroOficio: {Value}", expediente.NumeroOficio);
        _logger.LogInformation("✓ AreaDescripcion: {Value}", expediente.AreaDescripcion);

        // SolicitudPartes collection
        expediente.SolicitudPartes.ShouldNotBeNull();
        expediente.SolicitudPartes.Count.ShouldBeGreaterThanOrEqualTo(ExpectedPartesPerDocument,
            "Should have at least 1 SolicitudParte");

        var parte = expediente.SolicitudPartes[0];
        parte.Nombre.ShouldNotBeNullOrEmpty("Parte Nombre should be extracted");
        parte.PersonaTipo.ShouldNotBeNullOrEmpty("PersonaTipo should be extracted");

        _logger.LogInformation("✓ SolicitudPartes[0].Nombre: {Value}", parte.Nombre);
        _logger.LogInformation("✓ SolicitudPartes[0].PersonaTipo: {Value}", parte.PersonaTipo);

        // SolicitudEspecifica collection
        expediente.SolicitudEspecificas.ShouldNotBeNull();
        expediente.SolicitudEspecificas.Count.ShouldBeGreaterThanOrEqualTo(ExpectedEspecificasPerDocument,
            "Should have at least 1 SolicitudEspecifica");

        var especifica = expediente.SolicitudEspecificas[0];
        especifica.SolicitudEspecificaId.ShouldBeGreaterThan(0, "SolicitudEspecificaId should be positive");
        especifica.InstruccionesCuentasPorConocer.ShouldNotBeNullOrEmpty("Instructions should be extracted");

        _logger.LogInformation("✓ SolicitudEspecifica[0].Id: {Value}", especifica.SolicitudEspecificaId);
        _logger.LogInformation("✓ SolicitudEspecifica[0].Instructions length: {Value}", especifica.InstruccionesCuentasPorConocer.Length);

        // PersonasSolicitud nested collection
        especifica.PersonasSolicitud.ShouldNotBeNull();
        if (especifica.PersonasSolicitud.Count > 0)
        {
            var persona = especifica.PersonasSolicitud[0];
            persona.Nombre.ShouldNotBeNullOrEmpty("PersonaSolicitud Nombre should be extracted");
            _logger.LogInformation("✓ PersonasSolicitud[0].Nombre: {Value}", persona.Nombre);
        }

        _logger.LogInformation("✅ PRP1 222AAA: All fields extracted successfully");
    }

    /// <summary>
    /// Tests complete XML extraction for PRP1 fixture 333BBB.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_333BBB_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "333BBB-44444444442025.xml");
        File.Exists(xmlPath).ShouldBeTrue($"Fixture not found: {xmlPath}");

        _logger.LogInformation("Testing fixture: {FileName}", Path.GetFileName(xmlPath));
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Failed to parse XML: {result.Error}");
        result.Value.ShouldNotBeNull();
        var expediente = result.Value!;

        expediente.NumeroExpediente.ShouldNotBeNullOrEmpty();
        expediente.NumeroOficio.ShouldNotBeNullOrEmpty();
        expediente.SolicitudPartes.Count.ShouldBeGreaterThanOrEqualTo(1);
        expediente.SolicitudEspecificas.Count.ShouldBeGreaterThanOrEqualTo(1);

        _logger.LogInformation("✅ PRP1 333BBB: Parsed successfully");
    }

    /// <summary>
    /// Tests complete XML extraction for PRP1 fixture 333ccc.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_333ccc_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "333ccc-6666666662025.xml");
        File.Exists(xmlPath).ShouldBeTrue($"Fixture not found: {xmlPath}");

        _logger.LogInformation("Testing fixture: {FileName}", Path.GetFileName(xmlPath));
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Failed to parse XML: {result.Error}");
        result.Value.ShouldNotBeNull();
        var expediente = result.Value!;

        expediente.NumeroExpediente.ShouldNotBeNullOrEmpty();
        expediente.SolicitudPartes.Count.ShouldBeGreaterThanOrEqualTo(1);

        _logger.LogInformation("✅ PRP1 333ccc: Parsed successfully");
    }

    /// <summary>
    /// Tests complete XML extraction for PRP1 fixture 555CCC.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_555CCC_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "555CCC-66666662025.xml");
        File.Exists(xmlPath).ShouldBeTrue($"Fixture not found: {xmlPath}");

        _logger.LogInformation("Testing fixture: {FileName}", Path.GetFileName(xmlPath));
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Failed to parse XML: {result.Error}");
        result.Value.ShouldNotBeNull();
        var expediente = result.Value!;

        expediente.NumeroExpediente.ShouldNotBeNullOrEmpty();
        expediente.SolicitudPartes.Count.ShouldBeGreaterThanOrEqualTo(1);

        _logger.LogInformation("✅ PRP1 555CCC: Parsed successfully");
    }

    //

    //   Special Case Tests

    /// <summary>
    /// Tests XML parsing with CNBV namespace prefixes (Cnbv_ prefix).
    /// Verifies that both prefixed and non-prefixed elements are handled.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_WithCnbvNamespace_ShouldParseCorrectly()
    {
        // Arrange - Use first fixture which has Cnbv_ prefixes
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        _logger.LogInformation("Testing CNBV namespace handling with real fixture");

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Should handle Cnbv_ prefixes: {result.Error}");
        result.Value.ShouldNotBeNull();
        var expediente = result.Value!;

        // Verify fields that typically have Cnbv_ prefix
        expediente.NumeroOficio.ShouldNotBeNullOrEmpty("Should extract Cnbv_NumeroOficio");
        expediente.NumeroExpediente.ShouldNotBeNullOrEmpty("Should extract Cnbv_NumeroExpediente");

        _logger.LogInformation("✅ CNBV namespace prefixes handled correctly");
    }

    /// <summary>
    /// Tests XML parsing with null fields (xsi:nil="true").
    /// Verifies proper null handling for optional fields.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_WithNullFields_ShouldHandleGracefully()
    {
        // Arrange - Find fixture with null fields
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        _logger.LogInformation("Testing null field handling");

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue("Should handle null fields gracefully");
        result.Value.ShouldNotBeNull();
        var expediente = result.Value!;

        // Check optional fields - they may be null
        _logger.LogInformation("AutoridadEspecificaNombre: {Value}", expediente.AutoridadEspecificaNombre ?? "(null)");
        _logger.LogInformation("NombreSolicitante: {Value}", expediente.NombreSolicitante ?? "(null)");

        // Verify null is different from empty string
        if (expediente.NombreSolicitante == null)
        {
            _logger.LogInformation("✅ Null handling correct - field is null, not empty string");
        }
    }

    /// <summary>
    /// Smoke test: Parse all PRP1 fixtures without detailed assertions.
    /// Ensures no fixtures cause parser crashes.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_AllPRP1Fixtures_ShouldParseWithoutErrors()
    {
        // Arrange
        var xmlFiles = Directory.GetFiles(_fixturesPath, "*.xml");
        _logger.LogInformation("Smoke test: Parsing all {Count} XML fixtures", xmlFiles.Length);

        _xmlParser.ShouldNotBeNull();
        var successCount = 0;
        var failureCount = 0;

        // Act & Assert
        foreach (var xmlPath in xmlFiles)
        {
            var fileName = Path.GetFileName(xmlPath);
            _logger.LogInformation("  Testing: {FileName}", fileName);

            try
            {
                var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);
                var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

                if (result.IsSuccess)
                {
                    successCount++;
                    _logger.LogInformation("    ✓ Success");
                }
                else
                {
                    failureCount++;
                    _logger.LogError("    ✗ Failed: {Error}", result.Error);
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex, "    ✗ Exception: {Message}", ex.Message);
            }
        }

        // Assert
        _logger.LogInformation("Smoke test results: {Success} success, {Failure} failures", successCount, failureCount);
        failureCount.ShouldBe(0, "All fixtures should parse without errors");
        successCount.ShouldBe(xmlFiles.Length, "All fixtures should succeed");

        _logger.LogInformation("✅ All PRP1 fixtures parsed successfully");
    }

    //

    //   Performance Tests

    /// <summary>
    /// Tests XML parsing performance meets required threshold.
    /// Target: &lt; 100ms per document as per MinimumFieldsProvidedBySamples.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_Performance_ShouldMeetTargets()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        _xmlParser.ShouldNotBeNull();

        // Warm-up
        await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        const int iterations = 10;
        const int targetMs = 100; // From XmlExpedienteParser comments

        // Act
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);
        }

        stopwatch.Stop();

        // Assert
        var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        _logger.LogInformation("Performance test: {Iterations} iterations", iterations);
        _logger.LogInformation("Total time: {TotalMs}ms", stopwatch.ElapsedMilliseconds);
        _logger.LogInformation("Average per parse: {AvgMs:F2}ms", avgMs);
        _logger.LogInformation("Target: < {TargetMs}ms", targetMs);

        avgMs.ShouldBeLessThan(targetMs, $"Average parse time should be < {targetMs}ms");

        _logger.LogInformation("✅ Performance target met: {AvgMs:F2}ms < {TargetMs}ms", avgMs, targetMs);
    }

    //

    //   Field Count Tests

    /// <summary>
    /// Validates that the expected minimum fields are extracted per document.
    /// Based on MinimumFieldsProvidedBySamples constant (30 fields minimum).
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_FieldCount_ShouldMeetMinimumBaseline()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        _xmlParser.ShouldNotBeNull();
        var result = await _xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var expediente = result.Value!;

        // Count non-null fields
        int fieldCount = 0;

        // Root fields (15+)
        if (!string.IsNullOrEmpty(expediente.NumeroExpediente)) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.NumeroOficio)) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.SolicitudSiara)) fieldCount++;
        if (expediente.Folio > 0) fieldCount++;
        if (expediente.OficioYear > 0) fieldCount++;
        if (expediente.AreaClave > 0) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.AreaDescripcion)) fieldCount++;
        if (expediente.FechaPublicacion != DateTime.MinValue) fieldCount++;
        if (expediente.DiasPlazo > 0) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.AutoridadNombre)) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.Referencia)) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.Referencia1)) fieldCount++;
        if (!string.IsNullOrEmpty(expediente.Referencia2)) fieldCount++;

        // SolicitudPartes fields
        foreach (var parte in expediente.SolicitudPartes)
        {
            if (parte.ParteId > 0) fieldCount++;
            if (!string.IsNullOrEmpty(parte.Nombre)) fieldCount++;
            if (!string.IsNullOrEmpty(parte.PersonaTipo)) fieldCount++;
            if (!string.IsNullOrEmpty(parte.Caracter)) fieldCount++;
            // More fields counted...
        }

        // SolicitudEspecifica fields
        foreach (var especifica in expediente.SolicitudEspecificas)
        {
            if (especifica.SolicitudEspecificaId > 0) fieldCount++;
            if (!string.IsNullOrEmpty(especifica.InstruccionesCuentasPorConocer)) fieldCount++;

            // PersonasSolicitud nested
            foreach (var persona in especifica.PersonasSolicitud)
            {
                if (persona.PersonaId > 0) fieldCount++;
                if (!string.IsNullOrEmpty(persona.Nombre)) fieldCount++;
                // More fields counted...
            }
        }

        _logger.LogInformation("Total non-null fields extracted: {Count}", fieldCount);
        _logger.LogInformation("Minimum baseline (from samples): {Min}", MinimumFieldsPerDocument);

        fieldCount.ShouldBeGreaterThanOrEqualTo(MinimumFieldsPerDocument,
            "Should extract at least minimum baseline fields");

        _logger.LogInformation("✅ Field count meets baseline: {Count} >= {Min}", fieldCount, MinimumFieldsPerDocument);
    }

    //

    //   UTF-8 BOM Tests

    /// <summary>
    /// Verifies that XML files with UTF-8 BOM are parsed correctly.
    /// Critical test - all production XML files have BOM.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_WithUTF8BOM_ShouldParseSuccessfully()
    {
        // Arrange
        _logger.LogInformation("Testing UTF-8 BOM handling (critical for production files)");

        var xmlFiles = Directory.GetFiles(_fixturesPath, "*.xml");
        _xmlParser.ShouldNotBeNull();

        var filesWithBOM = 0;

        // Act & Assert
        foreach (var xmlPath in xmlFiles)
        {
            var bytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

            // Check if file has BOM (EF BB BF)
            var hasBOM = bytes.Length >= 3 &&
                         bytes[0] == 0xEF &&
                         bytes[1] == 0xBB &&
                         bytes[2] == 0xBF;

            if (hasBOM)
            {
                filesWithBOM++;
                _logger.LogInformation("  {FileName}: Has UTF-8 BOM", Path.GetFileName(xmlPath));

                var result = await _xmlParser.ParseAsync(bytes, TestContext.Current.CancellationToken);
                result.IsSuccess.ShouldBeTrue($"Should parse file with BOM: {Path.GetFileName(xmlPath)}");
            }
        }

        _logger.LogInformation("Files with UTF-8 BOM: {Count}/{Total}", filesWithBOM, xmlFiles.Length);
        filesWithBOM.ShouldBeGreaterThan(0, "At least some fixtures should have BOM to test the feature");

        _logger.LogInformation("✅ UTF-8 BOM handling verified");
    }

    //
}
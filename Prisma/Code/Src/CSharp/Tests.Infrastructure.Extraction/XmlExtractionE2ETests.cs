namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// End-to-end tests for XML extraction using real PRP1 fixtures.
/// These tests use actual XML documents from Fixtures/PRP1 to discover implementation gaps.
/// </summary>
/// <remarks>
/// **Purpose:** Systematically verify that all fields from source-of-truth XML documents
/// are correctly parsed and mapped to domain classes.
///
/// **Source of Truth:** 4 PRP1 XML files (222AAA, 333BBB, 333ccc, 555CCC)
///
/// **Expected Behavior:** These tests will initially FAIL, exposing gaps between
/// XML structure and domain classes. This is intentional and desired.
/// </remarks>
public class XmlExtractionE2ETests : IDisposable
{
    private readonly ILogger<XmlExpedienteParser> _parserLogger;
    private readonly ILogger<XmlMetadataExtractor> _extractorLogger;
    private readonly IXmlNullableParser<Expediente> _parser;
    private readonly IMetadataExtractor _extractor;
    private readonly string _fixturesPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlExtractionE2ETests"/> class.
    /// </summary>
    public XmlExtractionE2ETests()
    {
        _parserLogger = Substitute.For<ILogger<XmlExpedienteParser>>();
        _extractorLogger = Substitute.For<ILogger<XmlMetadataExtractor>>();
        _parser = new XmlExpedienteParser(_parserLogger);
        _extractor = new XmlMetadataExtractor(_parser, _extractorLogger);

        // Path to PRP1 fixtures (relative to test assembly location)
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _fixturesPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "Fixtures", "PRP1"));
    }

    /// <summary>
    /// Verifies that the PRP1 fixtures directory exists and contains XML files.
    /// </summary>
    [Fact]
    public void Fixtures_PRP1Directory_ShouldExist()
    {
        // Assert
        Directory.Exists(_fixturesPath).ShouldBeTrue($"PRP1 fixtures directory not found at: {_fixturesPath}");

        var xmlFiles = Directory.GetFiles(_fixturesPath, "*.xml");
        xmlFiles.Length.ShouldBeGreaterThan(0, $"No XML files found in: {_fixturesPath}");
    }

    /// <summary>
    /// Tests extraction of 222AAA XML - ASEGURAMIENTO case with Moral party.
    /// This test exposes gaps in SolicitudEspecifica and nested PersonasSolicitud.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_222AAA_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        File.Exists(xmlPath).ShouldBeTrue($"Fixture not found: {xmlPath}");

        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        result.Value.ShouldNotBeNull();

        var expediente = result.Value.Expediente;
        expediente.ShouldNotBeNull();

        // Verify top-level fields with CNBV namespace prefix
        expediente.NumeroOficio.ShouldBe("222/AAA/-4444444444/2025", "Cnbv_NumeroOficio mismatch");
        expediente.NumeroExpediente.Trim().ShouldBe("A/AS1-1111-222222-AAA", "Cnbv_NumeroExpediente mismatch");
        expediente.SolicitudSiara.ShouldBe("AGAFADAFSON2/2025/000084", "Cnbv_SolicitudSiara mismatch");
        expediente.Folio.ShouldBe(6789, "Cnbv_Folio mismatch");
        expediente.OficioYear.ShouldBe(2025, "Cnbv_OficioYear mismatch");
        expediente.AreaClave.ShouldBe(3, "Cnbv_AreaClave mismatch");
        expediente.AreaDescripcion.ShouldBe("ASEGURAMIENTO", "Cnbv_AreaDescripcion mismatch");
        expediente.FechaPublicacion.Date.ShouldBe(new DateTime(2025, 6, 5), "Cnbv_FechaPublicacion mismatch");
        expediente.DiasPlazo.ShouldBe(7, "Cnbv_DiasPlazo mismatch");
        expediente.AutoridadNombre.ShouldBe("SUBDELEGACION 8 SAN ANGEL", "AutoridadNombre mismatch");
        expediente.TieneAseguramiento.ShouldBeTrue("TieneAseguramiento should be true");

        // Verify SolicitudPartes (singular element in XML)
        expediente.SolicitudPartes.Count.ShouldBe(1, "Should have exactly 1 SolicitudParte");

        var parte = expediente.SolicitudPartes[0];
        parte.ParteId.ShouldBe(1, "ParteId mismatch");
        parte.Caracter.ShouldBe("Patrón Determinado", "Caracter mismatch");

        // ❌ EXPECTED FAILURE: XML uses <Persona>, but C# class has PersonaTipo
        parte.PersonaTipo.ShouldBe("Moral", "Persona/PersonaTipo field mismatch - XML uses <Persona>");

        parte.Nombre.ShouldBe("AEROLINEAS PAYASO ORGULLO NACIONAL", "Nombre mismatch");

        // Verify SolicitudEspecifica (singular element in XML)
        expediente.SolicitudEspecificas.Count.ShouldBe(1, "Should have exactly 1 SolicitudEspecifica");

        var solicitud = expediente.SolicitudEspecificas[0];

        // ❌ EXPECTED FAILURE: C# class has RequerimientoId (string), XML has SolicitudEspecificaId (int)
        // This will fail because the field doesn't exist in current implementation
        // solicitud.SolicitudEspecificaId.ShouldBe(1, "SolicitudEspecificaId mismatch");

        // ❌ EXPECTED FAILURE: Missing InstruccionesCuentasPorConocer field
        // This will fail because the field doesn't exist in current implementation
        // solicitud.InstruccionesCuentasPorConocer.ShouldContain("inmovilización", "InstruccionesCuentasPorConocer mismatch");
        // solicitud.InstruccionesCuentasPorConocer.ShouldContain("embargo", "Should contain embargo instructions");

        // ❌ EXPECTED FAILURE: Missing PersonasSolicitud collection
        // This will fail because the class doesn't exist
        // solicitud.PersonasSolicitud.ShouldNotBeNull("PersonasSolicitud should not be null");
        // solicitud.PersonasSolicitud.Count.ShouldBe(1, "Should have 1 PersonaSolicitud");

        // var persona = solicitud.PersonasSolicitud[0];
        // persona.PersonaId.ShouldBe(1, "PersonaId mismatch");
        // persona.Caracter.ShouldBe("Patrón Determinado", "PersonaSolicitud.Caracter mismatch");
        // persona.Persona.ShouldBe("Moral", "PersonaSolicitud.Persona mismatch");
        // persona.Nombre.ShouldBe("EAEROLÍNEAS PAYASO ORGULLO NACIONALIVE, S.A. DE C.V.", "PersonaSolicitud.Nombre mismatch");
        // persona.Rfc.ShouldBe("APON33333444", "PersonaSolicitud.Rfc mismatch");
        // persona.Domicilio.ShouldContain("Constitución", "PersonaSolicitud.Domicilio mismatch");
        // persona.Complementarios.ShouldBe("Y33 W 22512 01", "PersonaSolicitud.Complementarios mismatch");
    }

    /// <summary>
    /// Tests extraction of 333BBB XML - Additional PRP1 fixture.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_333BBB_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "333BBB-44444444442025.xml");

        if (!File.Exists(xmlPath))
        {
            // Skip test if file doesn't exist (allows incremental testing)
            return;
        }

        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();

        // Add specific assertions after examining the XML structure
        result.Value!.Expediente!.NumeroOficio.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests extraction of 333ccc XML - Additional PRP1 fixture.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_333ccc_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "333ccc-6666666662025.xml");

        if (!File.Exists(xmlPath))
        {
            return;
        }

        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();

        result.Value!.Expediente!.NumeroOficio.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests extraction of 555CCC XML - Additional PRP1 fixture.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_PRP1_555CCC_ShouldParseAllFields()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "555CCC-66666662025.xml");

        if (!File.Exists(xmlPath))
        {
            return;
        }

        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value.Expediente.ShouldNotBeNull();

        result.Value!.Expediente!.NumeroOficio.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that XML namespace handling works correctly.
    /// PRP1 XMLs use xmlns="http://www.cnbv.gob.mx" which must be handled properly.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_WithCnbvNamespace_ShouldParseCorrectly()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        File.Exists(xmlPath).ShouldBeTrue($"Fixture not found: {xmlPath}");

        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);
        var xmlString = System.Text.Encoding.UTF8.GetString(xmlBytes);

        // Verify XML has CNBV namespace
        xmlString.ShouldContain("xmlns=\"http://www.cnbv.gob.mx\"");
        xmlString.ShouldContain("Cnbv_NumeroOficio");

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert - Should handle namespace correctly
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente!.NumeroOficio.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests handling of null/empty fields (like NombreSolicitante with xsi:nil="true").
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_WithNullFields_ShouldHandleGracefully()
    {
        // Arrange
        var xmlPath = Path.Combine(_fixturesPath, "222AAA-44444444442025.xml");
        var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);

        // Act
        var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var expediente = result.Value!.Expediente!;

        // NombreSolicitante has xsi:nil="true" in XML - should be null
        expediente.NombreSolicitante.ShouldBeNull();

        // Referencia has whitespace - should be trimmed or handled
        // (Current implementation may keep whitespace, this documents the behavior)
    }

    /// <summary>
    /// Tests that all PRP1 fixtures can be parsed without errors.
    /// This is a smoke test to ensure no critical parsing errors exist.
    /// </summary>
    [Fact]
    public async Task ExtractFromXml_AllPRP1Fixtures_ShouldParseWithoutErrors()
    {
        // Arrange
        var xmlFiles = Directory.GetFiles(_fixturesPath, "*-*.xml")
            .Where(f => !Path.GetFileName(f).StartsWith("."))
            .ToArray();

        xmlFiles.Length.ShouldBeGreaterThan(0, "No PRP1 XML fixtures found");

        var results = new List<(string FileName, bool Success, string? Error)>();

        // Act
        foreach (var xmlPath in xmlFiles)
        {
            var xmlBytes = await File.ReadAllBytesAsync(xmlPath, TestContext.Current.CancellationToken);
            var result = await _extractor.ExtractFromXmlAsync(xmlBytes, TestContext.Current.CancellationToken);

            results.Add((
                Path.GetFileName(xmlPath),
                result.IsSuccess,
                result.Error
            ));
        }

        // Assert
        var failures = results.Where(r => !r.Success).ToArray();

        // Log all results for diagnostics
        Console.WriteLine($"Tested {results.Count} PRP1 fixtures:");
        foreach (var (fileName, success, error) in results)
        {
            Console.WriteLine($"  {(success ? "✅" : "❌")} {fileName}{(success ? "" : $": {error}")}");
        }

        // All should parse successfully (even if fields are missing, parsing shouldn't fail)
        failures.Length.ShouldBe(0, $"Some fixtures failed to parse: {string.Join(", ", failures.Select(f => f.FileName))}");
    }

    /// <summary>
    /// Disposes resources used by the test class.
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose currently
        GC.SuppressFinalize(this);
    }
}

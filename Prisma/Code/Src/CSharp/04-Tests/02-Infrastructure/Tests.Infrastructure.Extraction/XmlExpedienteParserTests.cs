using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="XmlExpedienteParser"/>.
/// </summary>
public class XmlExpedienteParserTests
{
    private readonly ILogger<XmlExpedienteParser> _logger;
    private readonly XmlExpedienteParser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlExpedienteParserTests"/> class.
    /// </summary>
    public XmlExpedienteParserTests()
    {
        _logger = Substitute.For<ILogger<XmlExpedienteParser>>();
        _parser = new XmlExpedienteParser(_logger);
    }

    /// <summary>
    /// Tests that valid XML expediente is parsed correctly.
    /// </summary>
    [Fact]
    public async Task ParseAsync_ValidXml_ReturnsExpediente()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <NumeroOficio>214-1-18714972/2025</NumeroOficio>
    <SolicitudSiara>SIARA-12345</SolicitudSiara>
    <Folio>123</Folio>
    <OficioYear>2025</OficioYear>
    <AreaClave>1</AreaClave>
    <AreaDescripcion>ASEGURAMIENTO</AreaDescripcion>
    <FechaPublicacion>2025-01-15</FechaPublicacion>
    <DiasPlazo>30</DiasPlazo>
    <AutoridadNombre>CNBV</AutoridadNombre>
    <Referencia>REF-001</Referencia>
    <TieneAseguramiento>true</TieneAseguramiento>
</Expediente>";
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);

        // Act
        var result = await _parser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.NumeroOficio.ShouldBe("214-1-18714972/2025");
        result.Value.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
        result.Value.TieneAseguramiento.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that XML with SolicitudPartes is parsed correctly.
    /// Updated to match real PRP1 XML structure (no <Parte> wrapper).
    /// </summary>
    [Fact]
    public async Task ParseAsync_XmlWithPartes_ParsesPartes()
    {
        // Arrange - Updated to match real CNBV XML structure
        var xml = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <SolicitudPartes>
        <ParteId>1</ParteId>
        <Caracter>Contribuyente</Caracter>
        <Persona>Fisica</Persona>
        <Nombre>Juan</Nombre>
        <Paterno>Perez</Paterno>
        <Rfc>PERJ800101ABC</Rfc>
    </SolicitudPartes>
</Expediente>";
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);

        // Act
        var result = await _parser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.SolicitudPartes.ShouldNotBeNull();
        result.Value.SolicitudPartes.Count.ShouldBe(1);
        result.Value.SolicitudPartes[0].Nombre.ShouldBe("Juan");
        result.Value.SolicitudPartes[0].PersonaTipo.ShouldBe("Fisica"); // Now correctly mapped from <Persona>
        result.Value.SolicitudPartes[0].Rfc.ShouldBe("PERJ800101ABC");
    }

    /// <summary>
    /// Tests that invalid XML returns failure.
    /// </summary>
    [Fact]
    public async Task ParseAsync_InvalidXml_ReturnsFailure()
    {
        // Arrange
        var invalidXml = "<Invalid><Unclosed>";
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(invalidXml);

        // Act
        var result = await _parser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that XML without root element returns failure.
    /// </summary>
    [Fact]
    public async Task ParseAsync_XmlWithoutRoot_ReturnsFailure()
    {
        // Arrange
        var xml = "<?xml version=\"1.0\"?>";
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);

        // Act
        var result = await _parser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
        // Error message may vary, just check it contains relevant keywords
        (result.Error.Contains("root", StringComparison.OrdinalIgnoreCase) ||
         result.Error.Contains("element", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that best-effort extraction populates LawMandatedFields from XML data.
    /// Verifies fields available in XML (SourceAuthorityCode, RequirementType, RequirementTypeCode)
    /// are populated, while bank-system fields remain null.
    /// </summary>
    [Fact]
    public async Task ParseAsync_ValidXml_PopulatesLawMandatedFields()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <AutoridadNombre>SUBDELEGACION 8 SAN ANGEL</AutoridadNombre>
    <AreaDescripcion>ASEGURAMIENTO</AreaDescripcion>
    <AreaClave>3</AreaClave>
    <TieneAseguramiento>true</TieneAseguramiento>
</Expediente>";
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);

        // Act
        var result = await _parser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        // Best-effort extraction should populate LawMandatedFields
        result.Value.LawMandatedFields.ShouldNotBeNull();
        result.Value.LawMandatedFields.SourceAuthorityCode.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
        result.Value.LawMandatedFields.RequirementType.ShouldBe("ASEGURAMIENTO");
        result.Value.LawMandatedFields.RequirementTypeCode.ShouldBe(3);

        // Fields from bank systems should remain null
        result.Value.LawMandatedFields.InternalCaseId.ShouldBeNull();
        result.Value.LawMandatedFields.ProcessingStatus.ShouldBeNull();
        result.Value.LawMandatedFields.IsPrimaryTitular.ShouldBeNull();
        result.Value.LawMandatedFields.BranchCode.ShouldBeNull();
        result.Value.LawMandatedFields.AccountNumber.ShouldBeNull();
        result.Value.LawMandatedFields.InitialBlockedAmount.ShouldBeNull();
    }

    /// <summary>
    /// Tests that LawMandatedFields remains null when XML has no relevant authority/area data.
    /// </summary>
    [Fact]
    public async Task ParseAsync_XmlWithoutAuthorityData_LawMandatedFieldsIsNull()
    {
        // Arrange - minimal XML without authority or area information
        var xml = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <NumeroOficio>214-1-18714972/2025</NumeroOficio>
</Expediente>";
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);

        // Act
        var result = await _parser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        // LawMandatedFields should be null if no data can be extracted
        result.Value.LawMandatedFields.ShouldBeNull();
    }
}


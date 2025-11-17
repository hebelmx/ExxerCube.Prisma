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
    /// </summary>
    [Fact]
    public async Task ParseAsync_XmlWithPartes_ParsesPartes()
    {
        // Arrange
        var xml = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <SolicitudPartes>
        <Parte>
            <ParteId>1</ParteId>
            <Caracter>Contribuyente</Caracter>
            <PersonaTipo>Fisica</PersonaTipo>
            <Nombre>Juan</Nombre>
            <Paterno>Perez</Paterno>
            <Rfc>PERJ800101ABC</Rfc>
        </Parte>
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
}


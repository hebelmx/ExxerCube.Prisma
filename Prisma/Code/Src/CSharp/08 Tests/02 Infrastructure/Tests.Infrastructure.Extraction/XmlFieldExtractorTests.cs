using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="XmlFieldExtractor"/>.
/// Uses in-memory XML strings with the CNBV namespace (http://www.cnbv.gob.mx) that the
/// extractor requires, so no fixture files or external dependencies are needed.
/// </summary>
public class XmlFieldExtractorTests
{
    private const string CnbvNs = "http://www.cnbv.gob.mx";

    private readonly XmlFieldExtractor _extractor = new();

    // Minimal CNBV XML: Expediente + SolicitudEspecifica with block instruction
    private const string ValidBlockXml = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Expediente xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                    xmlns="{CnbvNs}">
          <Cnbv_NumeroExpediente>A/AS1-2505-088637-PHM</Cnbv_NumeroExpediente>
          <Cnbv_AreaClave>1</Cnbv_AreaClave>
          <Cnbv_AreaDescripcion>ASEGURAMIENTO</Cnbv_AreaDescripcion>
          <Cnbv_FechaPublicacion>2025-06-01</Cnbv_FechaPublicacion>
          <Cnbv_DiasPlazo>7</Cnbv_DiasPlazo>
          <AutoridadNombre>SUBDELEGACION 8 SAN ANGEL</AutoridadNombre>
          <TieneAseguramiento>true</TieneAseguramiento>
          <SolicitudPartes>
            <Rfc>PERJ800101ABC</Rfc>
          </SolicitudPartes>
          <SolicitudEspecifica>
            <InstruccionesCuentasPorConocer>Se solicita bloquear la cuenta 123456789012.</InstruccionesCuentasPorConocer>
            <PersonasSolicitud>
              <Rfc>PERJ800101ABC</Rfc>
              <Complementarios>ZUCM444444ABCDEF01 datos adicionales</Complementarios>
            </PersonasSolicitud>
          </SolicitudEspecifica>
        </Expediente>
        """;

    /// <summary>Valid XML extraction populates Expediente, subdivision, and measure hint.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_ValidXmlWithCnbvNamespace_ReturnsSuccessWithFields()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    /// <summary>Aseguramiento (TieneAseguramiento=true) should produce MeasureHint=Aseguramiento.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_TieneAseguramientoTrue_SetsMeasureHintAseguramiento()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
    }

    /// <summary>ASEGURAMIENTO area maps to Subdivision=Aseguramiento.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_AreaDescripcionAseguramiento_SetsSubdivisionAseguramiento()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["Subdivision"].ShouldBe("Aseguramiento");
    }

    /// <summary>SLA fields FechaPublicacion and DiasPlazo are extracted into AdditionalFields.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_ValidXml_ExtractsSlaFields()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["FechaPublicacion"].ShouldBe("2025-06-01");
        result.Value.AdditionalFields["DiasPlazo"].ShouldBe("7");
    }

    /// <summary>RFC found in SolicitudPartes is collected in RfcList.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_RfcInSolicitudPartes_PopulatesRfcList()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("RfcList").ShouldBeTrue();
        result.Value.AdditionalFields["RfcList"]!.ShouldContain("PERJ800101ABC");
    }

    /// <summary>CURP found in Complementarios is normalized and stored in AdditionalFields.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_CurpInComplementarios_ExtractsCurp()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("Curp").ShouldBeTrue();
        result.Value.AdditionalFields["Curp"]!.Length.ShouldBeLessThanOrEqualTo(18);
    }

    /// <summary>Account numbers in InstruccionesCuentasPorConocer are collected in CuentasRaw.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_AccountInInstrucciones_PopulatesCuentasRaw()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("CuentasRaw").ShouldBeTrue();
        result.Value.AdditionalFields["CuentasRaw"]!.ShouldContain("123456789012");
    }

    /// <summary>
    /// XML without AreaDescripcion falls back to Subdivision=Unknown rather than throwing.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_MissingAreaDescripcion_SubdivisionIsUnknown()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>X/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <TieneAseguramiento>true</TieneAseguramiento>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["Subdivision"].ShouldBe("Unknown");
    }

    /// <summary>
    /// Unblock instruction text (DEJAR SIN EFECTOS) infers MeasureHint=Desbloqueo
    /// when TieneAseguramiento is false.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_UnblockInstruction_SetsMeasureHintDesbloqueo()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/DE1-0000-000001-BBB</Cnbv_NumeroExpediente>
              <Cnbv_AreaDescripcion>ASEGURAMIENTO</Cnbv_AreaDescripcion>
              <TieneAseguramiento>false</TieneAseguramiento>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>Se solicita DEJAR SIN EFECTOS el aseguramiento previo.</InstruccionesCuentasPorConocer>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["MeasureHint"].ShouldBe("Desbloqueo");
    }

    /// <summary>Null source returns failure without throwing.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_NullSource_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldsAsync(null!, Array.Empty<FieldDefinition>());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Empty XmlContent with no FilePath returns failure without throwing.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_EmptyContent_ReturnsFailure()
    {
        var source = new XmlSource(string.Empty, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Malformed (unclosed tag) XML returns failure without throwing.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_MalformedXml_ReturnsFailureWithoutThrowing()
    {
        var source = new XmlSource("<Expediente><Cnbv_NumeroExpediente>ABC", isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Single-field extraction for a known field returns the correct FieldValue.</summary>
    [Fact]
    public async Task ExtractFieldAsync_KnownField_Expediente_ReturnsFieldValue()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldAsync(source, "expediente");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value.ShouldNotBeNull();
        result.Value!.FieldName.ShouldBe("expediente");
        result.Value.Value.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Origin.ShouldBe(Domain.Enums.FieldOrigin.Xml);
        result.Value.Confidence.ShouldBe(1.0f);
    }

    /// <summary>Single-field extraction for an AdditionalFields key returns the correct FieldValue.</summary>
    [Fact]
    public async Task ExtractFieldAsync_AdditionalFieldKey_ReturnsFieldValue()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldAsync(source, "DiasPlazo");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("7");
        result.Value.Origin.ShouldBe(Domain.Enums.FieldOrigin.Xml);
    }

    /// <summary>Single-field extraction for a missing field returns failure.</summary>
    [Fact]
    public async Task ExtractFieldAsync_UnknownField_ReturnsFailure()
    {
        var source = new XmlSource(ValidBlockXml, isContent: true);

        var result = await _extractor.ExtractFieldAsync(source, "NoExiste");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("NoExiste");
    }

    /// <summary>Null source on single-field extraction returns failure without throwing.</summary>
    [Fact]
    public async Task ExtractFieldAsync_NullSource_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(null!, "Expediente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }
}

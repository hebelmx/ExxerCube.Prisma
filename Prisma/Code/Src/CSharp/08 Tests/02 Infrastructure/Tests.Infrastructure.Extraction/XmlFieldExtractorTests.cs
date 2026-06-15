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

    // -----------------------------------------------------------------------
    // Real-corpus tests (D1) — read exact expected values from the samples
    // -----------------------------------------------------------------------

    /// <summary>
    /// The 222AAA sample should surface NumeroOficio, Domicilio, and Descripcion from
    /// the first PersonasSolicitud.  Nombre-only since Paterno/Materno are empty in this sample.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_222AaaSample_SurfacesNumeroOficioAndDomicilio()
    {
        var xml = File.ReadAllText(SamplePath("222AAA-44444444442025.xml"));
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;

        // D1 — NumeroOficio read from root Cnbv_NumeroOficio element
        fields.AdditionalFields.ContainsKey("NumeroOficio").ShouldBeTrue();
        fields.AdditionalFields["NumeroOficio"].ShouldBe("222/AAA/-4444444444/2025");

        // D1 — Domicilio from first PersonasSolicitud
        fields.AdditionalFields.ContainsKey("Domicilio").ShouldBeTrue();
        fields.AdditionalFields["Domicilio"].ShouldBe("Pza. de la Constitución S/N  CP 066V60 Col centro , CDMX");

        // D1 — Descripcion: Paterno/Materno are empty in this sample so only Nombre contributes
        fields.AdditionalFields.ContainsKey("Descripcion").ShouldBeTrue();
        fields.AdditionalFields["Descripcion"].ShouldBe("EAEROLÍNEAS PAYASO ORGULLO NACIONALIVE, S.A. DE C.V.");

        // Individual name part keys are also surfaced
        fields.AdditionalFields.ContainsKey("Nombre").ShouldBeTrue();
        fields.AdditionalFields["Nombre"].ShouldBe("EAEROLÍNEAS PAYASO ORGULLO NACIONALIVE, S.A. DE C.V.");
    }

    /// <summary>
    /// The 333BBB sample has all three name parts and a Domicilio.
    /// Descripcion should compose "PEREZ Y PEREZ JHON DOE".
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_333BbbSample_ComposesDescripcionFromAllThreeNameParts()
    {
        var xml = File.ReadAllText(SamplePath("333BBB-44444444442025.xml"));
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;

        fields.AdditionalFields["NumeroOficio"].ShouldBe("333/BBB/-4444444444/2025");
        fields.AdditionalFields["Domicilio"].ShouldBe("Parque Lira S/N 1 Sec Bosque de Chapultepec CP 11850");
        fields.AdditionalFields["Paterno"].ShouldBe("PEREZ");
        fields.AdditionalFields["Materno"].ShouldBe("Y PEREZ");
        fields.AdditionalFields["Nombre"].ShouldBe("JHON DOE");
        fields.AdditionalFields["Descripcion"].ShouldBe("PEREZ Y PEREZ JHON DOE");
    }

    /// <summary>
    /// The 555CCC sample has name parts in the first PersonasSolicitud; Domicilio is "NO SE CUENTA".
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_555CccSample_SurfacesNumeroOficioAndDescripcion()
    {
        var xml = File.ReadAllText(SamplePath("555CCC-66666662025.xml"));
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;

        fields.AdditionalFields["NumeroOficio"].ShouldBe("555/CCC/-6666666/2025");
        fields.AdditionalFields["Domicilio"].ShouldBe("NO SE CUENTA");
        fields.AdditionalFields["Descripcion"].ShouldBe("LUIS MCDONALD HUGO PACO");
    }

    /// <summary>
    /// The 333ccc sample — Domicilio element is empty so no Domicilio key; name parts present.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_333CccSample_EmptyDomicilioIsOmitted()
    {
        var xml = File.ReadAllText(SamplePath("333ccc-6666666662025.xml"));
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;

        fields.AdditionalFields["NumeroOficio"].ShouldBe("333/ccc/-666666666/2025");
        // Domicilio element is empty (<Domicilio />) so Value() returns null → key omitted
        fields.AdditionalFields.ContainsKey("Domicilio").ShouldBeFalse();
        fields.AdditionalFields["Descripcion"].ShouldBe("ZU CARNAL MARCELO");
    }

    // -----------------------------------------------------------------------
    // Inline unit tests for the new D1 fields
    // -----------------------------------------------------------------------

    /// <summary>NumeroOficio is surfaced in AdditionalFields under key "NumeroOficio".</summary>
    [Fact]
    public async Task ExtractFieldsAsync_NumeroOficioPresent_SurfacedInAdditionalFields()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroOficio>214-1-18714972/2025</Cnbv_NumeroOficio>
              <Cnbv_NumeroExpediente>A/AS1-2505-088637-PHM</Cnbv_NumeroExpediente>
              <TieneAseguramiento>false</TieneAseguramiento>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["NumeroOficio"].ShouldBe("214-1-18714972/2025");
    }

    /// <summary>
    /// Missing Cnbv_NumeroOficio element → key absent (no null entry injected).
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_NumeroOficioAbsent_KeyNotPresent()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/AS1-2505-088637-PHM</Cnbv_NumeroExpediente>
              <TieneAseguramiento>false</TieneAseguramiento>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("NumeroOficio").ShouldBeFalse();
    }

    /// <summary>
    /// Domicilio from the first PersonasSolicitud is surfaced in AdditionalFields.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_DomicilioPresent_SurfacedInAdditionalFields()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/AS1-2505-088637-PHM</Cnbv_NumeroExpediente>
              <TieneAseguramiento>false</TieneAseguramiento>
              <SolicitudEspecifica>
                <PersonasSolicitud>
                  <Paterno>GARCIA</Paterno>
                  <Materno>LOPEZ</Materno>
                  <Nombre>JUAN</Nombre>
                  <Domicilio>Av. Insurgentes Sur 123, CDMX</Domicilio>
                </PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["Domicilio"].ShouldBe("Av. Insurgentes Sur 123, CDMX");
    }

    /// <summary>
    /// Descripcion is composed as "Paterno Materno Nombre" (blank parts skipped).
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_AllNameParts_ComposesDescripcion()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/AS1-2505-088637-PHM</Cnbv_NumeroExpediente>
              <TieneAseguramiento>false</TieneAseguramiento>
              <SolicitudEspecifica>
                <PersonasSolicitud>
                  <Paterno>HERNANDEZ</Paterno>
                  <Materno>RUIZ</Materno>
                  <Nombre>MARIA</Nombre>
                </PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["Descripcion"].ShouldBe("HERNANDEZ RUIZ MARIA");
        result.Value.AdditionalFields["Paterno"].ShouldBe("HERNANDEZ");
        result.Value.AdditionalFields["Materno"].ShouldBe("RUIZ");
        result.Value.AdditionalFields["Nombre"].ShouldBe("MARIA");
    }

    /// <summary>Blank Paterno/Materno are skipped — only Nombre contributes to Descripcion.</summary>
    [Fact]
    public async Task ExtractFieldsAsync_BlankPaternoMaterno_DescripcionIsJustNombre()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/AS1-2505-088637-PHM</Cnbv_NumeroExpediente>
              <TieneAseguramiento>false</TieneAseguramiento>
              <SolicitudEspecifica>
                <PersonasSolicitud>
                  <Paterno />
                  <Materno />
                  <Nombre>EMPRESA SA DE CV</Nombre>
                </PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var source = new XmlSource(xml, isContent: true);

        var result = await _extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields["Descripcion"].ShouldBe("EMPRESA SA DE CV");
        result.Value.AdditionalFields.ContainsKey("Paterno").ShouldBeFalse();
        result.Value.AdditionalFields.ContainsKey("Materno").ShouldBeFalse();
    }

    /// <summary>
    /// Resolves the absolute path to a sample file in docs/legal/samples.
    /// Tries well-known locations in order:
    /// 1. Walk up from the test assembly output directory (works when running inside the repo tree).
    /// 2. Known absolute path as a fallback (CI/local dev on a fixed drive layout).
    /// </summary>
    private static string SamplePath(string fileName)
    {
        // Walk up the directory tree to find the repo root (contains docs/legal/samples)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "legal", "samples", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        // Known absolute path for this repository layout
        var known = Path.Combine(
            @"E:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma",
            "docs", "legal", "samples", fileName);
        if (File.Exists(known))
        {
            return known;
        }

        throw new FileNotFoundException(
            $"Sample file '{fileName}' not found. " +
            "Ensure docs/legal/samples exists in the repository tree. " +
            $"Last tried: {known}");
    }
}

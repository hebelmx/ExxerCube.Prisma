using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="XmlExpedienteParser"/>. Pins the full field-mapping surface
/// (every element-name lookup + parse default), the <see cref="XmlExpedienteParser"/>'s private helpers
/// (GetElementValue Cnbv_ fallback / xsi:nil / empty-to-null, ExtractLawMandatedFields hasData + per-field
/// ternaries, CaptureUnknownFields known-skip/unknown-capture, and the BuildExtractionMetadata counters)
/// via the public <see cref="XmlExpedienteParser.ParseAsync"/> + the attached <c>ExtractionMetadata</c>.
/// </summary>
public class XmlExpedienteParserMutationTests
{
    private readonly XmlExpedienteParser _parser =
        new(Substitute.For<ILogger<XmlExpedienteParser>>());

    private async Task<Result<Expediente>> Parse(string xml) =>
        await _parser.ParseAsync(System.Text.Encoding.UTF8.GetBytes(xml), TestContext.Current.CancellationToken);

    private async Task<Result<Expediente>> ParseBytes(byte[] bytes) =>
        await _parser.ParseAsync(bytes, TestContext.Current.CancellationToken);

    private static ExtractionMetadata MetaOf(Result<Expediente> result)
    {
        var meta = result.GetMetadata<Expediente, ExtractionMetadata>();
        meta.ShouldNotBeNull();
        return meta!;
    }

    private const string FullPlainXml = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <NumeroOficio>214-1-18714972/2025</NumeroOficio>
    <SolicitudSiara>SIARA-12345</SolicitudSiara>
    <Folio>123</Folio>
    <OficioYear>2025</OficioYear>
    <AreaClave>3</AreaClave>
    <AreaDescripcion>ASEGURAMIENTO</AreaDescripcion>
    <FechaPublicacion>2025-01-15</FechaPublicacion>
    <DiasPlazo>30</DiasPlazo>
    <AutoridadNombre>CNBV</AutoridadNombre>
    <AutoridadEspecificaNombre>SUBDELEGACION 8</AutoridadEspecificaNombre>
    <NombreSolicitante>Solicitante X</NombreSolicitante>
    <Referencia>REF-0</Referencia>
    <Referencia1>REF-1</Referencia1>
    <Referencia2>REF-2</Referencia2>
    <TieneAseguramiento>true</TieneAseguramiento>
    <SolicitudPartes>
        <ParteId>1</ParteId>
        <Caracter>Contribuyente</Caracter>
        <Persona>Fisica</Persona>
        <Nombre>Juan</Nombre>
        <Paterno>Perez</Paterno>
        <Materno>Lopez</Materno>
        <Rfc>PERJ800101ABC</Rfc>
        <Relacion>Titular</Relacion>
        <Domicilio>Calle 1</Domicilio>
        <Complementarios>Extra</Complementarios>
    </SolicitudPartes>
    <SolicitudEspecifica>
        <SolicitudEspecificaId>7</SolicitudEspecificaId>
        <InstruccionesCuentasPorConocer>Todas</InstruccionesCuentasPorConocer>
        <PersonasSolicitud>
            <PersonaId>2</PersonaId>
            <Caracter>Tercero</Caracter>
            <Persona>Moral</Persona>
            <Nombre>Empresa</Nombre>
            <Paterno>Pat</Paterno>
            <Materno>Mat</Materno>
            <Rfc>GOMA900202XYZ</Rfc>
            <Relacion>Socio</Relacion>
            <Domicilio>Calle 2</Domicilio>
            <Complementarios>Extra2</Complementarios>
        </PersonasSolicitud>
    </SolicitudEspecifica>
    <UnknownField>Surprise</UnknownField>
</Expediente>";

    // ---------------------------------------------------------------------------------------------
    // Full happy path — every field-mapping element-name literal + parse
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_FullXml_MapsEveryRootField()
    {
        var e = (await Parse(FullPlainXml)).Value.ShouldNotBeNull();

        e.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        e.NumeroOficio.ShouldBe("214-1-18714972/2025");
        e.SolicitudSiara.ShouldBe("SIARA-12345");
        e.Folio.ShouldBe(123);
        e.OficioYear.ShouldBe(2025);
        e.AreaClave.ShouldBe(3);
        e.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
        e.FechaPublicacion.ShouldBe(new DateTime(2025, 1, 15));
        e.DiasPlazo.ShouldBe(30);
        e.AutoridadNombre.ShouldBe("CNBV");
        e.AutoridadEspecificaNombre.ShouldBe("SUBDELEGACION 8");
        e.NombreSolicitante.ShouldBe("Solicitante X");
        e.Referencia.ShouldBe("REF-0");
        e.Referencia1.ShouldBe("REF-1");
        e.Referencia2.ShouldBe("REF-2");
        e.TieneAseguramiento.ShouldBeTrue();
    }

    [Fact]
    public async Task ParseAsync_FullXml_MapsSolicitudParteFields()
    {
        var e = (await Parse(FullPlainXml)).Value.ShouldNotBeNull();

        e.SolicitudPartes.Count.ShouldBe(1);
        var p = e.SolicitudPartes[0];
        p.ParteId.ShouldBe(1);
        p.Caracter.ShouldBe("Contribuyente");
        p.PersonaTipo.ShouldBe("Fisica"); // mapped from <Persona>
        p.Nombre.ShouldBe("Juan");
        p.Paterno.ShouldBe("Perez");
        p.Materno.ShouldBe("Lopez");
        p.Rfc.ShouldBe("PERJ800101ABC");
        p.Relacion.ShouldBe("Titular");
        p.Domicilio.ShouldBe("Calle 1");
        p.Complementarios.ShouldBe("Extra");
    }

    [Fact]
    public async Task ParseAsync_FullXml_MapsEspecificaAndNestedPersona()
    {
        var e = (await Parse(FullPlainXml)).Value.ShouldNotBeNull();

        e.SolicitudEspecificas.Count.ShouldBe(1);
        var esp = e.SolicitudEspecificas[0];
        esp.SolicitudEspecificaId.ShouldBe(7);
        esp.InstruccionesCuentasPorConocer.ShouldBe("Todas");
        esp.PersonasSolicitud.Count.ShouldBe(1);
        var per = esp.PersonasSolicitud[0];
        per.PersonaId.ShouldBe(2);
        per.Caracter.ShouldBe("Tercero");
        per.Persona.ShouldBe("Moral");
        per.Nombre.ShouldBe("Empresa");
        per.Paterno.ShouldBe("Pat");
        per.Materno.ShouldBe("Mat");
        per.Rfc.ShouldBe("GOMA900202XYZ");
        per.Relacion.ShouldBe("Socio");
        per.Domicilio.ShouldBe("Calle 2");
        per.Complementarios.ShouldBe("Extra2");
    }

    [Fact]
    public async Task ParseAsync_FullXml_MetadataCountsExact()
    {
        var result = await Parse(FullPlainXml);
        var m = MetaOf(result);

        // 15 root + 10 parte + 2 especifica + 10 persona = 37
        m.TotalFieldsExtracted.ShouldBe(37);
        // fecha (year>2000) + parte RFC valid + persona RFC valid
        m.RegexMatches.ShouldBe(3);
        // AreaDescripcion in catalog + AutoridadNombre present
        m.CatalogValidations.ShouldBe(2);
        m.PatternViolations.ShouldBe(0);
        m.Source.ShouldBe(SourceType.XML_HandFilled);
    }

    [Fact]
    public async Task ParseAsync_FullXml_CapturesOnlyUnknownField()
    {
        var e = (await Parse(FullPlainXml)).Value.ShouldNotBeNull();

        // Every known field is skipped; only <UnknownField> lands in AdditionalFields.
        e.AdditionalFields.Count.ShouldBe(1);
        e.AdditionalFields["UnknownField"].ShouldBe("Surprise");
    }

    [Fact]
    public async Task ParseAsync_FullXml_PopulatesLawMandatedFields()
    {
        var e = (await Parse(FullPlainXml)).Value.ShouldNotBeNull();

        e.LawMandatedFields.ShouldNotBeNull();
        e.LawMandatedFields.SourceAuthorityCode.ShouldBe("CNBV");
        e.LawMandatedFields.RequirementType.ShouldBe("ASEGURAMIENTO");
        e.LawMandatedFields.RequirementTypeCode.ShouldBe(3);
    }

    // ---------------------------------------------------------------------------------------------
    // Defaults when fields absent / unparseable
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_MinimalXml_AppliesDefaults()
    {
        var e = (await Parse("<Expediente><NumeroExpediente>X</NumeroExpediente></Expediente>")).Value.ShouldNotBeNull();

        e.NumeroOficio.ShouldBe(string.Empty);     // ?? string.Empty
        e.SolicitudSiara.ShouldBe(string.Empty);
        e.Folio.ShouldBe(0);                        // int.TryParse fail -> 0
        e.AreaClave.ShouldBe(0);
        e.DiasPlazo.ShouldBe(0);
        e.OficioYear.ShouldBe(DateTime.Now.Year);   // distinct default (NOT 0)
        e.FechaPublicacion.ShouldBe(DateTime.MinValue);
        e.TieneAseguramiento.ShouldBeFalse();
        e.AutoridadEspecificaNombre.ShouldBeNull(); // nullable, no ?? fallback
        e.NombreSolicitante.ShouldBeNull();
        e.Referencia.ShouldBe(string.Empty);
        e.LawMandatedFields.ShouldBeNull();         // hasData false
    }

    [Fact]
    public async Task ParseAsync_MinimalXml_MetadataCountsExact()
    {
        var m = MetaOf(await Parse("<Expediente><NumeroExpediente>X</NumeroExpediente></Expediente>"));

        // NumeroExpediente + OficioYear(default year>0) = 2
        m.TotalFieldsExtracted.ShouldBe(2);
        m.RegexMatches.ShouldBe(0);
        m.CatalogValidations.ShouldBe(0);
        // Only the fecha==MinValue violation
        m.PatternViolations.ShouldBe(1);
    }

    [Fact]
    public async Task ParseAsync_TieneAseguramientoFalseLiteral_ParsesFalse()
    {
        // bool.TryParse("false") => true, value false; `TryParse(...) && value` must be false.
        // Kills `&&` -> `||` (which would yield true).
        var e = (await Parse("<Expediente><TieneAseguramiento>false</TieneAseguramiento></Expediente>")).Value.ShouldNotBeNull();

        e.TieneAseguramiento.ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // GetElementValue branches: Cnbv_ fallback, xsi:nil, empty-to-null
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_CnbvPrefixedElement_ResolvedViaFallback()
    {
        var e = (await Parse("<Expediente><Cnbv_NumeroExpediente>FROM-CNBV</Cnbv_NumeroExpediente></Expediente>")).Value.ShouldNotBeNull();

        e.NumeroExpediente.ShouldBe("FROM-CNBV");
    }

    [Fact]
    public async Task ParseAsync_PlainTakesPrecedenceOverCnbvPrefix()
    {
        // Plain element is tried first (FirstOrDefault on plain name before Cnbv_ fallback).
        var xml = "<Expediente><NumeroExpediente>PLAIN</NumeroExpediente><Cnbv_NumeroExpediente>CNBV</Cnbv_NumeroExpediente></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.NumeroExpediente.ShouldBe("PLAIN");
    }

    [Fact]
    public async Task ParseAsync_NilAttribute_TreatedAsNull()
    {
        // xsi:nil="true" -> null even though the element has inner text.
        var xml = "<Expediente><AutoridadEspecificaNombre nil=\"true\">ignored</AutoridadEspecificaNombre></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.AutoridadEspecificaNombre.ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_EmptyElement_TreatedAsNull()
    {
        var xml = "<Expediente><AutoridadEspecificaNombre>   </AutoridadEspecificaNombre></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.AutoridadEspecificaNombre.ShouldBeNull();
    }

    // ---------------------------------------------------------------------------------------------
    // CaptureUnknownFields
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_UnknownFieldWithValue_Captured()
    {
        var xml = "<Expediente><NumeroExpediente>X</NumeroExpediente><BrandNewField>NewValue</BrandNewField></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.AdditionalFields.ShouldContainKeyAndValue("BrandNewField", "NewValue");
    }

    [Fact]
    public async Task ParseAsync_UnknownFieldEmpty_NotCaptured()
    {
        var xml = "<Expediente><NumeroExpediente>X</NumeroExpediente><BlankNewField>   </BlankNewField></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.AdditionalFields.ShouldNotContainKey("BlankNewField");
    }

    [Fact]
    public async Task ParseAsync_AllKnownFieldNamesAtRoot_NothingCaptured()
    {
        // Every known field name (plain AND Cnbv_-prefixed) present at root with a value must be
        // recognized and skipped. Blanking any knownFields entry would make that name "unknown" and
        // push it into AdditionalFields -> Count > 0. So Count == 0 kills every knownFields string.
        var known = new[]
        {
            "NumeroExpediente", "NumeroOficio", "SolicitudSiara", "Folio", "OficioYear", "AreaClave",
            "AreaDescripcion", "FechaPublicacion", "DiasPlazo", "AutoridadNombre", "AutoridadEspecificaNombre",
            "NombreSolicitante", "Referencia", "Referencia1", "Referencia2", "TieneAseguramiento",
            "SolicitudPartes", "SolicitudEspecifica", "PersonasSolicitud", "ParteId", "Caracter", "Persona",
            "PersonaTipo", "Paterno", "Materno", "Nombre", "Rfc", "Relacion", "Domicilio", "Complementarios",
            "SolicitudEspecificaId", "InstruccionesCuentasPorConocer", "PersonaId",
        };
        var sb = new System.Text.StringBuilder("<Expediente>");
        foreach (var name in known)
        {
            sb.Append('<').Append(name).Append('>').Append("v").Append("</").Append(name).Append('>');
            sb.Append("<Cnbv_").Append(name).Append('>').Append("v").Append("</Cnbv_").Append(name).Append('>');
        }
        sb.Append("</Expediente>");

        var e = (await Parse(sb.ToString())).Value.ShouldNotBeNull();

        e.AdditionalFields.Count.ShouldBe(0);
    }

    // ---------------------------------------------------------------------------------------------
    // BuildExtractionMetadata — catalog validation, fecha logic, RFC pattern violations
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ASEGURAMIENTO")]
    [InlineData("HACENDARIO")]
    [InlineData("PENAL")]
    [InlineData("CIVIL")]
    [InlineData("ADMINISTRATIVO")]
    public async Task ParseAsync_ValidAreaDescripcion_CountsCatalogValidation(string area)
    {
        var xml = $"<Expediente><NumeroExpediente>X</NumeroExpediente><AreaDescripcion>{area}</AreaDescripcion></Expediente>";
        var m = MetaOf(await Parse(xml));

        m.CatalogValidations.ShouldBe(1);   // area valid; AutoridadNombre absent
        m.PatternViolations.ShouldBe(1);    // only the fecha==MinValue violation
    }

    [Fact]
    public async Task ParseAsync_InvalidAreaDescripcion_CountsPatternViolation()
    {
        var xml = "<Expediente><NumeroExpediente>X</NumeroExpediente><AreaDescripcion>NOTACATALOG</AreaDescripcion></Expediente>";
        var m = MetaOf(await Parse(xml));

        m.CatalogValidations.ShouldBe(0);
        m.PatternViolations.ShouldBe(2);    // invalid area + fecha==MinValue
    }

    [Fact]
    public async Task ParseAsync_FechaYearExactly2000_NoRegexMatchNoViolation()
    {
        // 434 requires Year > 2000; 2000 is NOT > 2000 -> no regex match, and !=MinValue -> no violation.
        var xml = "<Expediente><NumeroExpediente>X</NumeroExpediente><FechaPublicacion>2000-06-01</FechaPublicacion></Expediente>";
        var m = MetaOf(await Parse(xml));

        m.RegexMatches.ShouldBe(0);
        m.PatternViolations.ShouldBe(0);
    }

    [Fact]
    public async Task ParseAsync_ZeroIdsAndInvalidPersonaRfc_MetadataExcludesZeroIds()
    {
        // OficioYear=0, ParteId/EspecificaId/PersonaId absent(=0): each `> 0` guard must NOT count
        // them (kills `> 0` -> `>= 0` on L402/446/476/482). Persona has an invalid RFC -> counted as
        // a field + a pattern violation (kills the persona RFC `patternViolations++`, L498).
        var xml = @"<Expediente>
            <NumeroExpediente>X</NumeroExpediente>
            <OficioYear>0</OficioYear>
            <SolicitudPartes><Nombre>P</Nombre></SolicitudPartes>
            <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>I</InstruccionesCuentasPorConocer>
                <PersonasSolicitud><Nombre>Q</Nombre><Rfc>BADRFC</Rfc></PersonasSolicitud>
            </SolicitudEspecifica>
        </Expediente>";
        var result = await Parse(xml);
        var m = MetaOf(result);

        // NumeroExpediente(1) + parte Nombre(1) + especifica Instrucciones(1) + persona Nombre(1) + persona Rfc(1) = 5
        m.TotalFieldsExtracted.ShouldBe(5);
        m.RegexMatches.ShouldBe(0);
        m.CatalogValidations.ShouldBe(0);
        // fecha==MinValue(1) + invalid persona RFC(1)
        m.PatternViolations.ShouldBe(2);

        var e = result.Value.ShouldNotBeNull();
        e.OficioYear.ShouldBe(0); // explicit "0" parses to 0, NOT the DateTime.Now.Year default
    }

    [Fact]
    public async Task ParseAsync_PartyWithInvalidRfc_CountsPatternViolation()
    {
        var xml = @"<Expediente>
            <NumeroExpediente>X</NumeroExpediente>
            <FechaPublicacion>2025-01-15</FechaPublicacion>
            <SolicitudPartes><Nombre>Juan</Nombre><Rfc>NOT-A-RFC</Rfc></SolicitudPartes>
        </Expediente>";
        var m = MetaOf(await Parse(xml));

        m.RegexMatches.ShouldBe(1);         // fecha only; RFC invalid
        m.PatternViolations.ShouldBe(1);    // invalid RFC
    }

    // ---------------------------------------------------------------------------------------------
    // ExtractLawMandatedFields hasData combinations
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_OnlyAreaClave_LawMandatedFieldsNotNullWithCodeOnly()
    {
        var xml = "<Expediente><NumeroExpediente>X</NumeroExpediente><AreaClave>9</AreaClave></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.LawMandatedFields.ShouldNotBeNull();
        e.LawMandatedFields.RequirementTypeCode.ShouldBe(9);
        e.LawMandatedFields.SourceAuthorityCode.ShouldBeNull(); // autoridad blank
        e.LawMandatedFields.RequirementType.ShouldBeNull();     // area desc blank
    }

    [Fact]
    public async Task ParseAsync_OnlyAutoridad_RequirementTypeCodeNull()
    {
        var xml = "<Expediente><NumeroExpediente>X</NumeroExpediente><AutoridadNombre>SAT</AutoridadNombre></Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.LawMandatedFields.ShouldNotBeNull();
        e.LawMandatedFields.SourceAuthorityCode.ShouldBe("SAT");
        e.LawMandatedFields.RequirementTypeCode.ShouldBeNull(); // areaClave 0 -> null (not 0)
    }

    // ---------------------------------------------------------------------------------------------
    // Multiple collection elements
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_MultiplePartes_AllParsed()
    {
        var xml = @"<Expediente>
            <SolicitudPartes><Nombre>Uno</Nombre></SolicitudPartes>
            <SolicitudPartes><Nombre>Dos</Nombre></SolicitudPartes>
        </Expediente>";
        var e = (await Parse(xml)).Value.ShouldNotBeNull();

        e.SolicitudPartes.Count.ShouldBe(2);
        e.SolicitudPartes[0].Nombre.ShouldBe("Uno");
        e.SolicitudPartes[1].Nombre.ShouldBe("Dos");
    }

    // ---------------------------------------------------------------------------------------------
    // BOM handling + structural failures
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_Utf8BomPrefixedXml_ParsesSuccessfully()
    {
        // detectEncodingFromByteOrderMarks:true must strip the EF BB BF BOM; otherwise XDocument.Load fails.
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes("<Expediente><NumeroExpediente>BOMOK</NumeroExpediente></Expediente>");
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(xmlBytes).ToArray();

        var result = await ParseBytes(withBom);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.NumeroExpediente.ShouldBe("BOMOK");
    }

    [Fact]
    public async Task ParseAsync_InvalidXml_ReturnsFailure()
    {
        var result = await Parse("<Invalid><Unclosed>");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }
}

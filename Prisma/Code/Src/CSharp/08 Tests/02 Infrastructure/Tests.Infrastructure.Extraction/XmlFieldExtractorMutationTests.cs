using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="XmlFieldExtractor"/>, complementing the existing
/// XmlFieldExtractorTests. Targets the previously-uncovered surface: the full measure-inference
/// chain (InferMeasure -> ParseActionKind -> ToSpanishMeasureName), authority values, the
/// present/absent collection guards (CuentasRaw / RfcList), the RFC sources + whitespace skip,
/// the subdivision accent normalization, ExtractFieldAsync field routing/failure, and
/// file-based loading.
/// </summary>
public class XmlFieldExtractorMutationTests
{
    private const string CnbvNs = "http://www.cnbv.gob.mx";

    private readonly XmlFieldExtractor _extractor = new();

    private static XmlSource Xml(
        string? instrucciones,
        bool aseguramiento = false,
        string area = "ASEGURAMIENTO",
        string? autoridad = null,
        string? autoridadEspecifica = null)
    {
        var instrEl = instrucciones is null
            ? string.Empty
            : $"<SolicitudEspecifica><InstruccionesCuentasPorConocer>{instrucciones}</InstruccionesCuentasPorConocer></SolicitudEspecifica>";
        var autEl = autoridad is null ? string.Empty : $"<AutoridadNombre>{autoridad}</AutoridadNombre>";
        var autEspEl = autoridadEspecifica is null ? string.Empty : $"<AutoridadEspecificaNombre>{autoridadEspecifica}</AutoridadEspecificaNombre>";

        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <Cnbv_AreaDescripcion>{area}</Cnbv_AreaDescripcion>
              <TieneAseguramiento>{(aseguramiento ? "true" : "false")}</TieneAseguramiento>
              {autEl}
              {autEspEl}
              {instrEl}
            </Expediente>
            """;
        return new XmlSource(xml, isContent: true);
    }

    private async Task<string?> Measure(string? instrucciones, bool aseguramiento = false)
    {
        var r = await _extractor.ExtractFieldsAsync(Xml(instrucciones, aseguramiento), Array.Empty<FieldDefinition>());
        r.IsSuccess.ShouldBeTrue(r.Error);
        return r.Value!.AdditionalFields["MeasureHint"];
    }

    // ---- Measure inference chain ----

    [Theory]
    // InferMeasure keyword branches (TieneAseguramiento=false)
    [InlineData("Se solicita DEJAR SIN EFECTOS el bloqueo previo", "Desbloqueo")]
    [InlineData("Se ELIMINA el registro de la lista", "Desbloqueo")]
    [InlineData("Solicitud para REANUDAR operaciones", "Desbloqueo")]
    [InlineData("Remitir COPIA CERTIFICADA del oficio", "Documentacion")]
    [InlineData("Entregar el DOCUMENTO oficial completo", "Documentacion")]
    [InlineData("Realizar TRANSFERENCIA de los fondos", "Transferencia")]
    // ParseActionKind fallback (no InferMeasure keyword matched)
    [InlineData("Se ordena el bloqueo inmediato", "Aseguramiento")] // BLOQ
    [InlineData("Proceder a LIBERAR las cuentas", "Desbloqueo")]     // LIBER (avoids BLOQ)
    [InlineData("Autorizar el TRASPASO de recursos", "Transferencia")] // TRASP (avoids TRANSFER)
    [InlineData("Proporcionar INFORMACION de saldos", "Informacion")] // INFO
    [InlineData("Adjuntar el DOCTO solicitado", "Documentacion")]     // DOC (avoids DOCUMENT)
    [InlineData("Se debe IGNORAR la presente", "Ignorar")]            // IGNOR
    [InlineData("Solicitud generica de revision", "Otro")]            // no keyword -> Other
    public async Task MeasureHint_FromInstructions_MapsToExactMeasure(string instr, string expected)
    {
        (await Measure(instr)).ShouldBe(expected);
    }

    [Fact]
    public async Task MeasureHint_AseguramientoFlagWins_EvenOverUnblockText()
    {
        // TieneAseguramiento=true short-circuits InferMeasure -> Aseguramiento regardless of text.
        (await Measure("DEJAR SIN EFECTOS", aseguramiento: true)).ShouldBe("Aseguramiento");
    }

    [Fact]
    public async Task MeasureHint_NoInstructionsNoAseguramiento_DefaultsToInformacion()
    {
        // instrucciones null + aseguramiento false -> ParseActionKind(null)=Unknown -> Informacion default.
        (await Measure(null)).ShouldBe("Informacion");
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task ExtractFieldsAsync_TieneAseguramiento_StoredUnderItsKey(bool flag, string expected)
    {
        // pins the "TieneAseguramiento" dictionary key + the raw flag value.
        var r = await _extractor.ExtractFieldsAsync(Xml("INFORMACION", aseguramiento: flag), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["TieneAseguramiento"].ShouldBe(expected);
    }

    // ---- Authority values ----

    [Fact]
    public async Task ExtractFieldsAsync_AuthorityNames_AreExtractedExactly()
    {
        var r = await _extractor.ExtractFieldsAsync(
            Xml("INFORMACION", autoridad: "FISCALIA GENERAL", autoridadEspecifica: "UNIDAD ESPECIALIZADA"),
            Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["AutoridadNombre"].ShouldBe("FISCALIA GENERAL");
        r.Value.AdditionalFields["AutoridadEspecificaNombre"].ShouldBe("UNIDAD ESPECIALIZADA");
    }

    [Fact]
    public async Task ExtractFieldsAsync_AreaClaveAndDescripcion_StoredUnderTheirKeys()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <Cnbv_AreaClave>5</Cnbv_AreaClave>
              <Cnbv_AreaDescripcion>TRANSFERENCIAS</Cnbv_AreaDescripcion>
              <TieneAseguramiento>false</TieneAseguramiento>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["AreaClave"].ShouldBe("5");
        r.Value.AdditionalFields["AreaDescripcion"].ShouldBe("TRANSFERENCIAS");
        r.Value.AdditionalFields["Subdivision"].ShouldBe("Transferencias");
    }

    // ---- CuentasRaw present/absent ----

    [Fact]
    public async Task ExtractFieldsAsync_NoAccountNumbers_OmitsCuentasRaw()
    {
        // instrucciones with no 6+ digit run -> accounts.Length == 0 -> key NOT added (kills > -> >=).
        var r = await _extractor.ExtractFieldsAsync(Xml("Solicitud de INFORMACION sin numeros"), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields.ContainsKey("CuentasRaw").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_MultipleAccounts_AreDistinctAndJoined()
    {
        var r = await _extractor.ExtractFieldsAsync(
            Xml("Conocer cuentas 123456 y 7890123 y 123456"), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        var cuentas = r.Value!.AdditionalFields["CuentasRaw"];
        cuentas.ShouldBe("123456,7890123"); // distinct, order-preserved, comma-joined
    }

    // ---- RFC sources ----

    [Fact]
    public async Task ExtractFieldsAsync_NoRfc_OmitsRfcList()
    {
        var r = await _extractor.ExtractFieldsAsync(Xml("INFORMACION"), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields.ContainsKey("RfcList").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_RfcOnlyInPersonasSolicitud_IsCollected()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>INFORMACION</InstruccionesCuentasPorConocer>
                <PersonasSolicitud><Rfc>XAXX010101000</Rfc></PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["RfcList"].ShouldBe("XAXX010101000");
    }

    [Fact]
    public async Task ExtractFieldsAsync_RfcOnlyInSolicitudPartes_IsCollected()
    {
        // RFC present ONLY in SolicitudPartes (not echoed in PersonasSolicitud) so the
        // SolicitudPartes 'rfcs.Add' statement is the sole source -> removing it drops RfcList.
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <SolicitudPartes><Rfc>AAA010101AA1</Rfc></SolicitudPartes>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>INFORMACION</InstruccionesCuentasPorConocer>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["RfcList"].ShouldBe("AAA010101AA1");
    }

    [Fact]
    public async Task ExtractFieldsAsync_WhitespaceRfc_IsSkipped()
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <SolicitudPartes><Rfc>   </Rfc></SolicitudPartes>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>INFORMACION</InstruccionesCuentasPorConocer>
                <PersonasSolicitud><Rfc>GODE561231GR8</Rfc></PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        // whitespace RFC skipped; only the real one collected
        r.Value!.AdditionalFields["RfcList"].ShouldBe("GODE561231GR8");
    }

    [Fact]
    public async Task ExtractFieldsAsync_OnlyWhitespaceRfcInSolicitudPartes_OmitsRfcList()
    {
        // SolicitudPartes RFC is whitespace-only and there is no other RFC -> rfcs stays empty
        // -> RfcList key absent. A '!IsNullOrWhiteSpace' -> '!= null' mutant would add "" and
        // emit an (empty) RfcList.
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <SolicitudPartes><Rfc>   </Rfc></SolicitudPartes>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>INFORMACION</InstruccionesCuentasPorConocer>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields.ContainsKey("RfcList").ShouldBeFalse();
    }

    // ---- Subdivision accent/space normalization ----

    [Theory]
    [InlineData("OPERACIONES ILÍCITAS", "OperacionesIlicitas")] // space removal + í
    [InlineData("TRÁMITE", "Tramite")]                          // á
    [InlineData("TELÉFONO", "Telefono")]                        // é
    [InlineData("GESTIÓN", "Gestion")]                          // ó
    [InlineData("NÚMERO", "Numero")]                            // ú
    public async Task ExtractFieldsAsync_AccentedArea_NormalizesSubdivision(string area, string expected)
    {
        // title-case -> remove spaces -> strip each accent (one InlineData per accent letter).
        var r = await _extractor.ExtractFieldsAsync(Xml("INFORMACION", area: area), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["Subdivision"].ShouldBe(expected);
    }

    // ---- CURP loose vs strict ----

    [Fact]
    public async Task ExtractFieldsAsync_LooseCurpWithoutVerifierDigits_IsCapturedAndUppercased()
    {
        // 16-char curp payload (no trailing 2 digits) in lowercase exercises the loose regex + uppercasing.
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>INFORMACION</InstruccionesCuentasPorConocer>
                <PersonasSolicitud><Complementarios>curp gole561231hdfmrl asociado</Complementarios></PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["Curp"].ShouldBe("GOLE561231HDFMRL");
    }

    [Fact]
    public async Task ExtractFieldsAsync_StrictValidCurp_IsCapturedViaStrictRegex()
    {
        // A structurally-valid 18-char CURP matches the StrictCurpRegex branch (exercised
        // before the loose fallback). "GOLE561231HDFMRL08": vowel O, month 12, day 31, sex H.
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Expediente xmlns="{CnbvNs}">
              <Cnbv_NumeroExpediente>A/XX1-0000-000000-XXX</Cnbv_NumeroExpediente>
              <SolicitudEspecifica>
                <InstruccionesCuentasPorConocer>INFORMACION</InstruccionesCuentasPorConocer>
                <PersonasSolicitud><Complementarios>titular GOLE561231HDFMRL08 confirmado</Complementarios></PersonasSolicitud>
              </SolicitudEspecifica>
            </Expediente>
            """;
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(xml, isContent: true), Array.Empty<FieldDefinition>());

        r.IsSuccess.ShouldBeTrue(r.Error);
        r.Value!.AdditionalFields["Curp"].ShouldBe("GOLE561231HDFMRL08");
    }

    // ---- ExtractFieldAsync routing ----

    [Fact]
    public async Task ExtractFieldAsync_AccionAliases_ReturnInstructions()
    {
        var source = Xml("INFORMACION de cuentas");

        (await _extractor.ExtractFieldAsync(source, "accionsolicitada")).Value!.Value.ShouldBe("INFORMACION de cuentas");
        (await _extractor.ExtractFieldAsync(source, "accion_solicitada")).Value!.Value.ShouldBe("INFORMACION de cuentas");
    }

    [Fact]
    public async Task ExtractFieldAsync_CausaWhenAbsent_ReturnsFailure()
    {
        // Causa is never populated by the XML extractor -> null -> "not found" failure.
        var result = await _extractor.ExtractFieldAsync(Xml("INFORMACION"), "causa");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("causa");
    }

    [Fact]
    public async Task ExtractFieldAsync_FieldValueMetadata_IsXmlOriginConfidenceOne()
    {
        var result = await _extractor.ExtractFieldAsync(Xml("INFORMACION", autoridad: "FISCALIA"), "AutoridadNombre");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("FISCALIA");
        result.Value.SourceType.ShouldBe("XML");
        result.Value.Confidence.ShouldBe(1.0f);
    }

    [Fact]
    public async Task ExtractFieldAsync_OnMalformedXml_PropagatesFailure()
    {
        var result = await _extractor.ExtractFieldAsync(new XmlSource("<Expediente><x", isContent: true), "expediente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    // ---- File-based loading ----

    [Fact]
    public async Task ExtractFieldsAsync_FromFilePath_LoadsAndExtracts()
    {
        var path = Path.GetTempFileName();
        try
        {
            var xml = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Expediente xmlns="{CnbvNs}">
                  <Cnbv_NumeroExpediente>F/IL1-0000-000099-ZZZ</Cnbv_NumeroExpediente>
                  <Cnbv_AreaDescripcion>ASEGURAMIENTO</Cnbv_AreaDescripcion>
                  <TieneAseguramiento>true</TieneAseguramiento>
                </Expediente>
                """;
            File.WriteAllText(path, xml);

            var r = await _extractor.ExtractFieldsAsync(new XmlSource(path), Array.Empty<FieldDefinition>());

            r.IsSuccess.ShouldBeTrue(r.Error);
            r.Value!.Expediente.ShouldBe("F/IL1-0000-000099-ZZZ");
            r.Value.AdditionalFields["MeasureHint"].ShouldBe("Aseguramiento");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractFieldsAsync_NonexistentFilePathAndNoContent_ReturnsFailure()
    {
        var r = await _extractor.ExtractFieldsAsync(new XmlSource(@"Z:\does\not\exist.xml"), Array.Empty<FieldDefinition>());

        r.IsFailure.ShouldBeTrue();
        r.Error.ShouldNotBeNullOrEmpty();
    }
}

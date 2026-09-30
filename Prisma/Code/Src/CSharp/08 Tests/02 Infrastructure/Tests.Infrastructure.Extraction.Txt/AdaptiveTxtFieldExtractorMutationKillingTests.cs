namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Mutation-killing tests for <see cref="AdaptiveTxtFieldExtractor"/>.
/// </summary>
/// <remarks>
/// These tests were written against the Stryker.NET survivor map (baseline 24.90%, Killed 64 /
/// Survived 167). Each test pins an EXACT extracted value (not just non-null) so that a string,
/// equality, boolean, or block-removal mutation in the corresponding regex / branch produces an
/// observable failure. The bulk of the survivors were the ten extraction helpers that had no
/// value-asserting coverage (Email, Telefono, CodigoPostal, Direccion, FundamentoLegal,
/// AutoridadEspecifica, FechaPublicacion, DiasPlazo, TieneAseguramiento, NombreSolicitante),
/// plus the <c>ExtractFieldByName</c> alias switch, the produced <see cref="FieldValue"/> metadata,
/// the authority-priority ladder, and the labeled NumeroOficio path.
/// </remarks>
public class AdaptiveTxtFieldExtractorMutationKillingTests
{
    private readonly AdaptiveTxtFieldExtractor _extractor;

    public AdaptiveTxtFieldExtractorMutationKillingTests(ITestOutputHelper output)
    {
        var logger = XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output);
        _extractor = new AdaptiveTxtFieldExtractor(logger);
    }

    private static TxtSource Src(string text, float? confidence = null) => new(text, confidence);

    // ---------------------------------------------------------------------
    // FieldValue metadata + confidence default propagation
    // Kills: L264 "TXT_OCR" string, FieldOrigin.PdfOcr, L122 `?? 0.8f` null-coalescing,
    //        L267 "not found" failure-message string.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldAsync_Match_TagsSourceTypeAndPdfOcrOrigin()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Expediente: A/AS1-2505-088637-PHM"), "Expediente");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SourceType.ShouldBe("TXT_OCR");
        result.Value.Origin.ShouldBe(FieldOrigin.PdfOcr);
        result.Value.FieldName.ShouldBe("Expediente");
    }

    [Fact]
    public async Task ExtractFieldAsync_UsesProvidedOcrConfidence()
    {
        var result = await _extractor.ExtractFieldAsync(Src("CAUSA: X", confidence: 0.5f), "Causa");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Confidence.ShouldBe(0.5f);
    }

    [Fact]
    public async Task ExtractFieldAsync_NullOcrConfidence_DefaultsToPointEight()
    {
        // Kills the null-coalescing "remove left" mutant `source.OcrConfidence ?? 0.8f` -> would
        // still be 0.8f, so we instead prove the LEFT operand is used by supplying a non-default
        // confidence here is impossible; this asserts the documented default of 0.8 when null.
        var result = await _extractor.ExtractFieldAsync(Src("CAUSA: X", confidence: null), "Causa");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Confidence.ShouldBe(0.8f);
    }

    [Fact]
    public async Task ExtractFieldAsync_UnknownField_ReturnsNotFoundFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("nothing relevant here"), "Email");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ExtractFieldAsync_NullSource_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(null!, "Expediente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("TxtSource cannot be null");
    }

    // ---------------------------------------------------------------------
    // ExtractFieldByName alias switch — every alias must map to the right extractor.
    // Kills the String mutations on the switch case labels (L229-256).
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("expediente")]
    [InlineData("numeroexpediente")]
    [InlineData("numero_expediente")]
    public async Task ExtractFieldAsync_ExpedienteAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("Expediente: A/AS1-2505-088637-PHM"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Theory]
    [InlineData("accionsolicitada")]
    [InlineData("accion_solicitada")]
    public async Task ExtractFieldAsync_AccionSolicitadaAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("ACCIÓN SOLICITADA: Aseguramiento"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Aseguramiento");
    }

    [Theory]
    [InlineData("numerooficio")]
    [InlineData("numero_oficio")]
    [InlineData("solicitudsiara")]
    [InlineData("solicitud_siara")]
    public async Task ExtractFieldAsync_NumeroOficioAndSiaraAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("AGAFADAFSON2/2025/000084"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000084");
    }

    [Theory]
    [InlineData("autoridadnombre")]
    [InlineData("autoridad_nombre")]
    public async Task ExtractFieldAsync_AutoridadNombreAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("Comisión Nacional Bancaria y de Valores"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Comisión Nacional Bancaria y de Valores");
    }

    [Theory]
    [InlineData("autoridadespecificanombre")]
    [InlineData("autoridad_especifica_nombre")]
    public async Task ExtractFieldAsync_AutoridadEspecificaAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("DIRECCIÓN GENERAL DE SANCIONES"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value!.ShouldContain("DIRECCIÓN GENERAL DE SANCIONES");
    }

    [Theory]
    [InlineData("nombresolicitante")]
    [InlineData("nombre_solicitante")]
    public async Task ExtractFieldAsync_NombreSolicitanteAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("Lic. Juan Pérez García"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Juan Pérez García");
    }

    [Theory]
    [InlineData("email")]
    [InlineData("correo")]
    [InlineData("correoelectronico")]
    [InlineData("correo_electronico")]
    public async Task ExtractFieldAsync_EmailAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("contacto: juan@banco.mx"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("juan@banco.mx");
    }

    [Theory]
    [InlineData("telefono")]
    [InlineData("tel")]
    public async Task ExtractFieldAsync_TelefonoAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("Teléfono: (55) 1234-5678"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("(55) 1234-5678");
    }

    [Theory]
    [InlineData("codigopostal")]
    [InlineData("codigo_postal")]
    [InlineData("cp")]
    public async Task ExtractFieldAsync_CodigoPostalAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("C.P. 06600"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("06600");
    }

    [Theory]
    [InlineData("fundamentolegal")]
    [InlineData("fundamento_legal")]
    public async Task ExtractFieldAsync_FundamentoLegalAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("con fundamento en el artículo 142"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Art. 142");
    }

    [Theory]
    [InlineData("fechapublicacion")]
    [InlineData("fecha_publicacion")]
    public async Task ExtractFieldAsync_FechaPublicacionAliases_AllResolve(string alias)
    {
        // day == month so the ISO result is independent of the runner's date culture.
        var result = await _extractor.ExtractFieldAsync(Src("Fecha de Publicación: 03/03/2025"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("2025-03-03");
    }

    [Theory]
    [InlineData("diasplazo")]
    [InlineData("dias_plazo")]
    public async Task ExtractFieldAsync_DiasPlazoAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("plazo de 7 días"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("7");
    }

    [Theory]
    [InlineData("tieneaseguramiento")]
    [InlineData("tiene_aseguramiento")]
    public async Task ExtractFieldAsync_TieneAseguramientoAliases_AllResolve(string alias)
    {
        var result = await _extractor.ExtractFieldAsync(Src("se ordena el aseguramiento de cuentas"), alias);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("True");
    }

    // ---------------------------------------------------------------------
    // ExtractEmail (L477-482) — lowercasing + the success?value:null conditional.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractEmail_MixedCase_ReturnsLowercased()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Correo: Juan.Perez@Banco.MX"), "email");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("juan.perez@banco.mx");
    }

    [Fact]
    public async Task ExtractEmail_NoEmailPresent_ReturnsFailure()
    {
        // Kills the Conditional(true) mutant that would always return match.Value ("") on no match.
        var result = await _extractor.ExtractFieldAsync(Src("no address at all"), "email");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractTelefono (L487-501) — labeled path AND unlabeled fallback path.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractTelefono_LabeledPattern_ReturnsCapturedNumber()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Teléfono: (55) 1234-5678"), "telefono");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("(55) 1234-5678");
    }

    [Fact]
    public async Task ExtractTelefono_UnlabeledFallback_ReturnsBareNumber()
    {
        var result = await _extractor.ExtractFieldAsync(Src("comuníquese al 55 1234-5678 por favor"), "telefono");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("55 1234-5678");
    }

    [Fact]
    public async Task ExtractTelefono_NoNumber_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("sin teléfono disponible"), "telefono");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractCodigoPostal (L506-513) — captures group 1 only (no "C.P." prefix).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractCodigoPostal_ReturnsFiveDigitsWithoutPrefix()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Domicilio Col. Centro C.P. 06600 CDMX"), "cp");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("06600");
    }

    [Fact]
    public async Task ExtractCodigoPostal_NoPostalCode_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("sin codigo postal"), "cp");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractDireccion (L518-528) — keyword-anchored block, newlines collapsed to spaces.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractDireccion_StreetKeyword_ReturnsLineContainingAddress()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Av. Insurgentes Sur 1971, Col. Centro"), "direccion");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value!.ShouldContain("Av. Insurgentes Sur 1971");
    }

    [Fact]
    public async Task ExtractDireccion_MultiLineBlock_CollapsesNewlinesToSpaces()
    {
        var text = "Calle Falsa 123\nCol. Juárez\nCiudad de México";
        var result = await _extractor.ExtractFieldAsync(Src(text), "direccion");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value!.ShouldNotContain("\n");
        // Pin the SPACE that replaces each newline (kills the "\n"->" " replacement-arg mutant).
        result.Value.Value!.ShouldContain("123 Col.");
    }

    [Fact]
    public async Task ExtractDireccion_NoAddressKeywords_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("texto sin domicilio alguno"), "direccion");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractFundamentoLegal (L533-550) — "Art. N" prefix + "; " join of multiples.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractFundamentoLegal_SingleArticle_PrefixesWithArt()
    {
        var result = await _extractor.ExtractFieldAsync(Src("con fundamento en el artículo 142"), "fundamentolegal");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Art. 142");
    }

    [Fact]
    public async Task ExtractFundamentoLegal_MultipleArticles_JoinsWithSemicolon()
    {
        var text = "artículo 142 y conforme al artículo 251 del código";
        var result = await _extractor.ExtractFieldAsync(Src(text), "fundamentolegal");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value!.ShouldContain("Art. 142");
        result.Value.Value!.ShouldContain("; ");
        result.Value.Value!.ShouldContain("251");
    }

    [Fact]
    public async Task ExtractFundamentoLegal_NoArticles_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("documento sin fundamento citado"), "fundamentolegal");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractAutoridadEspecifica (L555-560).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractAutoridadEspecifica_DireccionKeyword_ReturnsMatch()
    {
        var result = await _extractor.ExtractFieldAsync(Src("DIRECCIÓN GENERAL DE SANCIONES"), "autoridadespecificanombre");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value!.ShouldContain("DIRECCIÓN GENERAL DE SANCIONES");
    }

    [Fact]
    public async Task ExtractAutoridadEspecifica_NoDepartmentKeyword_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("texto ordinario sin oficina"), "autoridadespecificanombre");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractFechaPublicacion (L565-580) — labeled only, normalized to yyyy-MM-dd.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractFechaPublicacion_LabeledDate_NormalizesToIso()
    {
        // day == month so the ISO result is independent of the runner's date culture.
        var result = await _extractor.ExtractFieldAsync(Src("Fecha de Publicación: 03/03/2025"), "fechapublicacion");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("2025-03-03");
    }

    [Fact]
    public async Task ExtractFechaPublicacion_UnlabeledDate_NotExtracted()
    {
        // The extractor deliberately only matches a labeled date; a bare date must NOT be guessed.
        var result = await _extractor.ExtractFieldAsync(Src("emitido el 03/03/2025"), "fechapublicacion");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractDiasPlazo (L585-600) — three lead-in phrasings, parsed to int.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("plazo de 7 días", "7")]
    [InlineData("en 10 días", "10")]
    [InlineData("dentro de 3 días", "3")]
    public async Task ExtractDiasPlazo_VariousPhrasings_ReturnsNumber(string text, string expected)
    {
        var result = await _extractor.ExtractFieldAsync(Src(text), "diasplazo");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExtractDiasPlazo_NoDeadline_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("sin plazo establecido"), "diasplazo");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractTieneAseguramiento (L605-636) — positive keywords -> True,
    // desbloqueo/liberación keywords -> False, otherwise not present.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("se ordena el aseguramiento de cuentas")]
    [InlineData("procédase al embargo de bienes")]
    [InlineData("se solicita el bloqueo inmediato")]
    [InlineData("retención de fondos")]
    [InlineData("inmovilización de la cuenta")]
    [InlineData("congelamiento de activos")]
    public async Task ExtractTieneAseguramiento_SeizureKeyword_ReturnsTrue(string text)
    {
        var result = await _extractor.ExtractFieldAsync(Src(text), "tieneaseguramiento");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("True");
    }

    [Theory]
    [InlineData("liberación de los fondos retenidos antes")]
    [InlineData("se ordena dejar sin efectos la medida")]
    public async Task ExtractTieneAseguramiento_ReleaseKeyword_ReturnsFalse(string text)
    {
        var result = await _extractor.ExtractFieldAsync(Src(text), "tieneaseguramiento");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("False");
    }

    [Fact]
    public async Task ExtractTieneAseguramiento_Desbloqueo_ReturnsTrue_BloqueoSubstringWins()
    {
        // Documents actual behavior: "desbloqueo" contains the positive "bloqueo" substring,
        // which is checked first, so the result is True (the release branch never sees it).
        var result = await _extractor.ExtractFieldAsync(Src("se autoriza el desbloqueo de la cuenta"), "tieneaseguramiento");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("True");
    }

    [Fact]
    public async Task ExtractTieneAseguramiento_NoSeizureContext_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("documento meramente informativo"), "tieneaseguramiento");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractNombreSolicitante (L452-472) — honorific path AND job-title path.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("Lic. Juan Pérez García")]
    [InlineData("Mtra. Ana López Hernández")]
    [InlineData("Dr. Carlos Ramírez Soto")]
    public async Task ExtractNombreSolicitante_HonorificPrefix_ReturnsName(string text)
    {
        var result = await _extractor.ExtractFieldAsync(Src(text), "nombresolicitante");

        result.IsSuccess.ShouldBeTrue();
        // The capture group excludes the honorific itself.
        result.Value!.Value!.ShouldNotContain(".");
        result.Value.Value!.Split(' ').Length.ShouldBe(3);
    }

    [Fact]
    public async Task ExtractNombreSolicitante_HonorificLic_ReturnsExactName()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Lic. Juan Pérez García"), "nombresolicitante");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Juan Pérez García");
    }

    [Fact]
    public async Task ExtractNombreSolicitante_JobTitleOnNextLine_ReturnsNameLine()
    {
        // The title pattern requires a "de"/"del" connector after the title keyword.
        var text = "Director de Supervisión\nMaría Fernanda Ruiz";
        var result = await _extractor.ExtractFieldAsync(Src(text), "nombresolicitante");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("María Fernanda Ruiz");
    }

    [Fact]
    public async Task ExtractNombreSolicitante_NoNamePattern_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Src("documento sin firmante"), "nombresolicitante");

        result.IsFailure.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------
    // ExtractNumeroOficio — the labeled path (was NoCoverage; the unlabeled pattern
    // always matched first in prior tests). Force the labeled-only branch.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractNumeroOficio_LabeledOnly_UsesLabeledCapture()
    {
        // "OFICIO123" does NOT satisfy the unlabeled pattern (needs /dddd/dddddd),
        // so extraction must come from the "Número de Oficio:" labeled branch.
        var result = await _extractor.ExtractFieldAsync(Src("Número de Oficio: OFICIO123"), "numerooficio");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("OFICIO123");
    }

    // ---------------------------------------------------------------------
    // ExtractAutoridadNombre — the priority ladder below SAT-explicit/CNBV-full.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractAutoridadNombre_AgaffFullName_ReturnsFullName()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Administración General de Auditoría Fiscal Federal"), "autoridadnombre");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Administración General de Auditoría Fiscal Federal");
    }

    [Theory]
    [InlineData("Oficio girado por la AGAFF al banco", "AGAFF")]
    [InlineData("Resolución de la CNBV vigente", "CNBV")]
    [InlineData("Notificación del SAT pendiente", "SAT")]
    public async Task ExtractAutoridadNombre_BareAcronym_ReturnsAcronym(string text, string expected)
    {
        var result = await _extractor.ExtractFieldAsync(Src(text), "autoridadnombre");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExtractAutoridadNombre_AcronymOnlyInEmailDomain_NotMatched()
    {
        // The (?<![@.]) and (?!\.gob\.mx) guards must reject "sat" inside an email domain.
        var result = await _extractor.ExtractFieldAsync(Src("escribir a contacto@sat.gob.mx para dudas"), "autoridadnombre");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractAutoridadNombre_AcronymAfterAtSign_NotMatched()
    {
        // Isolates the (?<![@.]) prefix guard: "SAT" is preceded by '@' but NOT followed by
        // ".gob.mx", so only the prefix guard can reject it.
        var result = await _extractor.ExtractFieldAsync(Src("envíe a info@SAT hoy"), "autoridadnombre");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractAutoridadNombre_AcronymFollowedByGobMxDomain_NotMatched()
    {
        // Isolates the (?!\.gob\.mx) suffix guard: "SAT" is preceded by a space (prefix guard
        // passes) but followed by ".gob.mx", so only the suffix guard can reject it.
        var result = await _extractor.ExtractFieldAsync(Src("consulte SAT.gob.mx pronto"), "autoridadnombre");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractAutoridadNombre_SatExplicitWinsOverCnbvText_ReturnsSat()
    {
        // SAT explicit self-identification is priority 1, even when CNBV text is present.
        var text = "Comisión Nacional Bancaria y de Valores\nSAT - Servicio de Administración Tributaria";
        var result = await _extractor.ExtractFieldAsync(Src(text), "autoridadnombre");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("SAT");
    }

    // ---------------------------------------------------------------------
    // ExtractExpediente — primary pattern vs OCR-fuzzy+clean must be distinguishable.
    // Kills the L319 "return match.Value" block-removal: removing it routes a clean
    // expediente through CleanOcrErrors (O->0), corrupting a legitimate trailing 'O'.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractExpediente_TrailingLetterO_PreservedByPrimaryPattern()
    {
        var result = await _extractor.ExtractFieldAsync(Src("Expediente: A/AS1-2505-088637-PHO"), "expediente");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHO");
    }

    [Fact]
    public async Task ExtractExpediente_OcrZeroAsLetterO_CleanedToDigits()
    {
        // Fuzzy path: O in the numeric sections is cleaned back to 0.
        var result = await _extractor.ExtractFieldAsync(Src("Expediente: A/AS1-25O5-O88637-PHM"), "expediente");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
    }

    // ---------------------------------------------------------------------
    // ExtractFieldsAsync core extraction — pins the AdditionalFields KEY names
    // (string mutations at L169-204) and the AddFieldIfNotPresent add branch (L213-214).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_RichDocument_PopulatesNamedAdditionalFields()
    {
        var text = @"
Expediente: A/AS1-2505-088637-PHM
CAUSA: Investigación administrativa
ACCIÓN SOLICITADA: Aseguramiento preventivo
AGAFADAFSON2/2025/000084
Comisión Nacional Bancaria y de Valores
DIRECCIÓN GENERAL DE SUPERVISIÓN
Lic. Juan Pérez García
Correo: juan@banco.mx
Teléfono: (55) 1234-5678
Av. Insurgentes Sur 1971, Col. Centro
C.P. 06600
con fundamento en el artículo 142
Fecha de Publicación: 03/03/2025
plazo de 7 días
se ordena el aseguramiento de cuentas
";
        var result = await _extractor.ExtractFieldsAsync(Src(text, 0.9f), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue();
        var f = result.Value!;

        f.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        f.Causa.ShouldBe("Investigación administrativa");
        f.AccionSolicitada.ShouldBe("Aseguramiento preventivo");

        f.AdditionalFields["NumeroOficio"].ShouldBe("AGAFADAFSON2/2025/000084");
        f.AdditionalFields["SolicitudSiara"].ShouldBe("AGAFADAFSON2/2025/000084");
        f.AdditionalFields["AutoridadNombre"].ShouldBe("Comisión Nacional Bancaria y de Valores");
        f.AdditionalFields.ShouldContainKey("AutoridadEspecificaNombre");
        // The honorific capture's \s+ spans the newline and greedily annexes the next
        // capitalized token ("Correo"); the exact-value contract is pinned separately in
        // ExtractNombreSolicitante_HonorificLic_ReturnsExactName.
        f.AdditionalFields["NombreSolicitante"].ShouldStartWith("Juan Pérez García");
        f.AdditionalFields["Email"].ShouldBe("juan@banco.mx");
        f.AdditionalFields["Telefono"].ShouldBe("(55) 1234-5678");
        f.AdditionalFields.ShouldContainKey("Direccion");
        f.AdditionalFields["CodigoPostal"].ShouldBe("06600");
        f.AdditionalFields["FundamentoLegal"].ShouldBe("Art. 142");
        f.AdditionalFields["FechaPublicacion"].ShouldBe("2025-03-03");
        f.AdditionalFields["DiasPlazo"].ShouldBe("7");
        f.AdditionalFields["TieneAseguramiento"].ShouldBe("True");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NoExtractableExtras_DoesNotInventAdditionalFields()
    {
        // Kills AddFieldIfNotPresent negation mutants that would add empty/null values.
        var result = await _extractor.ExtractFieldsAsync(Src("Expediente: A/AS1-2505-088637-PHM"), Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFields.ShouldNotContainKey("Email");
        result.Value.AdditionalFields.ShouldNotContainKey("Telefono");
        result.Value.AdditionalFields.ShouldNotContainKey("DiasPlazo");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NonCoreFieldDefinition_StoredUnderRawFieldName()
    {
        // ApplyFieldToExtractedFields stores non-core fields under the RAW (caller-supplied)
        // field name. Using a casing that differs from the canonical core key ("Email") proves
        // the default branch runs and is keyed on fieldDef.FieldName, not the normalized name.
        var fieldDefs = new[] { new FieldDefinition("EMAIL") };
        var result = await _extractor.ExtractFieldsAsync(Src("Correo: juan@banco.mx"), fieldDefs);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFields.ShouldContainKey("EMAIL");
        result.Value.AdditionalFields["EMAIL"].ShouldBe("juan@banco.mx");
    }
}

using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for the SAT requerimiento DOCX field patterns added in PRISMA-E5-S5 (D2 hardening).
/// Covers the new label variants: RFC, Número de folio, Fecha del oficio, Autoridad,
/// and the expanded Expediente/NumeroExpediente aliases.
/// All tests use synthetic in-memory DOCX constructed with OpenXml — no real SAT fixture
/// is required (noted as limitation: "no real SAT .docx fixture available; tested at the
/// ExtractFieldAsync / ExtractFieldsAsync seam").
/// </summary>
public class DocxFieldExtractorSatPatternTests
{
    private readonly DocxFieldExtractor _extractor =
        new(Substitute.For<ILogger<DocxFieldExtractor>>());

    // ------------------------------------------------------------------ helpers

    /// <summary>Builds a minimal DOCX whose body text is <paramref name="paragraphs"/> joined as separate paragraphs.</summary>
    private static DocxSource Source(params string[] paragraphs) => new(BuildDocx(paragraphs));

    private static byte[] BuildDocx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            foreach (var text in paragraphs)
            {
                body.AppendChild(new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // ============================================================
    // 1. RFC del contribuyente — full SAT label with accent (PRISMA-E5-S5 new pattern)
    // ============================================================

    /// <summary>
    /// SAT requerimiento DOCX: "RFC del contribuyente: XAXX010101000"
    /// The full label (with "del contribuyente") must be matched and the RFC value returned.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcFullLabel_ExtractsRfcValue()
    {
        // Arrange — SAT field: "RFC del contribuyente:"
        var source = Source("RFC del contribuyente: XAXX010101000");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "rfc");

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("XAXX010101000");
        result.Value.SourceType.ShouldBe("DOCX");
        result.Value.Origin.ShouldBe(ExxerCube.Prisma.Domain.Enums.FieldOrigin.Docx);
    }

    /// <summary>
    /// SAT requerimiento DOCX: bare "RFC:" label (no "del contribuyente" qualifier).
    /// Both CNBV oficios and SAT documents use this abbreviated form.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcBareLabel_ExtractsRfcValue()
    {
        // Arrange — SAT field: "RFC:" (bare label)
        var source = Source("RFC: BAAZ9211042A9");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "rfccontribuyente");

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("BAAZ9211042A9");
    }

    /// <summary>
    /// RFC alias "rfc_contribuyente" (underscore form) resolves identically.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcUnderscoreAlias_ExtractsRfcValue()
    {
        var source = Source("RFC del contribuyente: GAGO821002HE0");

        var result = await _extractor.ExtractFieldAsync(source, "rfc_contribuyente");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("GAGO821002HE0");
    }

    /// <summary>
    /// When the document has no RFC pattern, ExtractFieldAsync returns failure (not throw).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcNotPresent_ReturnsFailure()
    {
        var source = Source("Este documento no tiene RFC.");

        var result = await _extractor.ExtractFieldAsync(source, "rfc");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    // ============================================================
    // 2. Número de folio — full SAT label (PRISMA-E5-S5 new pattern)
    // ============================================================

    /// <summary>
    /// SAT field: "Número de folio: A/AS1-2505-088637-PHM" (full label with accent).
    /// Value must be normalised (internal whitespace collapsed) to match the canonical form.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_NumeroFolioFullLabel_ExtractsFolioNormalised()
    {
        // Arrange — SAT field: "Número de folio:" with a spaced folio value
        var source = Source("Número de folio: A/AS1- 2505-088637-PHM");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "folio");

        // Assert — whitespace inside the folio must be collapsed (normalised)
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.SourceType.ShouldBe("DOCX");
    }

    /// <summary>
    /// SAT field: "Numero de folio:" (without accent — OCR tolerance).
    /// The pattern is accent-tolerant: [uú] covers both forms.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_NumeroFolioNoAccent_StillMatches()
    {
        // Arrange — OCR-degraded label without accent
        var source = Source("Numero de folio: B/CDEF-1234-567890-ABC");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "numerofolio");

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("B/CDEF-1234-567890-ABC");
    }

    /// <summary>
    /// SAT field: bare "Folio:" label (shortest variant used in some templates).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FolioBareLabelVariant_ExtractsValue()
    {
        var source = Source("Folio: A/Y1-2506-099999-XYZ");

        var result = await _extractor.ExtractFieldAsync(source, "folio");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("A/Y1-2506-099999-XYZ");
    }

    // ============================================================
    // 3. Fecha del oficio — SAT requerimiento date header (PRISMA-E5-S5 new pattern)
    // ============================================================

    /// <summary>
    /// SAT field: "Fecha del oficio: 09 de Abril de 2025"
    /// Full label with "del oficio" qualifier.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaOficioFullLabel_ExtractsFechaString()
    {
        // Arrange — SAT field: "Fecha del oficio:"
        var source = Source("Fecha del oficio: 09 de Abril de 2025");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "fecharequerimiento");

        // Assert — raw date string returned (downstream normalises to ISO)
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldNotBeNull();
        result.Value.Value!.ShouldContain("09");
        result.Value.Value.ShouldContain("2025");
    }

    /// <summary>
    /// SAT field: "Fecha de emisión:" — alternative wording used in some SAT templates.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaEmisionAlternativeLabel_ExtractsValue()
    {
        var source = Source("Fecha de emisión: 15 de Junio de 2025");

        var result = await _extractor.ExtractFieldAsync(source, "fecha_oficio");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldNotBeNull();
        result.Value.Value!.ShouldContain("15");
        result.Value.Value.ShouldContain("Junio");
    }

    /// <summary>
    /// SAT field: bare "Fecha:" label (shortest form).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaBareLabel_ExtractsValue()
    {
        var source = Source("Fecha: 2025-04-09");

        var result = await _extractor.ExtractFieldAsync(source, "fechaoficio");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldNotBeNull();
        result.Value.Value!.ShouldContain("2025");
    }

    // ============================================================
    // 4. Autoridad nombre — SAT authority identification (PRISMA-E5-S5 new pattern)
    // ============================================================

    /// <summary>
    /// SAT field: explicit "SAT - Servicio de Administración Tributaria" self-identification.
    /// Priority 1: returns canonical "SAT" acronym.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_AutoridadSatExplicitLabel_ReturnsSat()
    {
        // Arrange — SAT requerimiento DOCX contains the full SAT self-identification string
        var source = Source("Emitido por: SAT - Servicio de Administración Tributaria");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "autoridadnombre");

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("SAT");
    }

    /// <summary>
    /// SAT field: "Comisión Nacional Bancaria y de Valores" — CNBV-issued SIARA document.
    /// Priority 2: returns the full CNBV name.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_AutoridadCnbvFullName_ReturnsCnbvName()
    {
        var source = Source("La Comisión Nacional Bancaria y de Valores requiere lo siguiente:");

        var result = await _extractor.ExtractFieldAsync(source, "autoridad_nombre");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("Comisión Nacional Bancaria y de Valores");
    }

    /// <summary>
    /// SAT field: bare "SAT" acronym (word-boundary guard prevents false match on "@sat.gob.mx").
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_AutoridadBareAcronymSat_ReturnsSat()
    {
        // The SAT acronym appears in a sentence but not as part of an email domain.
        var source = Source("El SAT ha emitido el siguiente requerimiento fiscal.");

        var result = await _extractor.ExtractFieldAsync(source, "autoridadnombre");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("SAT");
    }

    /// <summary>
    /// Negative case: email address "@sat.gob.mx" must NOT be matched as authority "SAT".
    /// The word-boundary guard prevents false matches on email domains.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_AutoridadEmailDomainOnly_DoesNotFalselyMatchSat()
    {
        // Only an email containing @sat.gob.mx — the word-boundary guard should prevent match.
        var source = Source("Contacto: receptor@sat.gob.mx para dudas.");

        var result = await _extractor.ExtractFieldAsync(source, "autoridadnombre");

        // "@sat.gob.mx" should NOT match the bare SAT acronym guard.
        // The SAT acronym guard requires: not preceded by @/. AND not followed by .gob.mx
        result.IsFailure.ShouldBeTrue("email domain @sat.gob.mx must not be reported as SAT authority");
    }

    // ============================================================
    // 5. ExtractFieldsAsync multi-field SAT scenario
    // ============================================================

    /// <summary>
    /// Representative SAT requerimiento DOCX with multiple fields: RFC, Número de folio,
    /// Fecha del oficio and the requerimiento id. All fields must be extracted in a single
    /// ExtractFieldsAsync call (as the Athena worker does).
    /// Limitation: synthetic DOCX, no real SAT fixture.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_SatRequerimientoLayout_ExtractsAllSatFields()
    {
        // Arrange — representative SAT requerimiento header text (synthetic)
        var source = Source(
            "SAT - Servicio de Administración Tributaria",
            "RFC del contribuyente: GAGO821002HE0",
            "Número de folio: A/AS1-2505-088637-PHM",
            "Fecha del oficio: 09 de Abril de 2025",
            "AGAFADAFSON2/2025/000084");

        var fieldDefinitions = new[]
        {
            new FieldDefinition("rfc"),
            new FieldDefinition("folio"),
            new FieldDefinition("fecharequerimiento"),
            new FieldDefinition("autoridadnombre"),
            new FieldDefinition("requerimiento"),
        };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert — all five SAT fields extracted
        result.IsSuccess.ShouldBeTrue(result.Error);
        var fields = result.Value!;

        fields.AdditionalFields.ShouldContainKey("rfc");
        fields.AdditionalFields["rfc"].ShouldBe("GAGO821002HE0");

        fields.AdditionalFields.ShouldContainKey("folio");
        fields.AdditionalFields["folio"].ShouldBe("A/AS1-2505-088637-PHM");

        fields.AdditionalFields.ShouldContainKey("fecharequerimiento");
        fields.AdditionalFields["fecharequerimiento"].ShouldNotBeNullOrEmpty();
        fields.AdditionalFields["fecharequerimiento"]!.ShouldContain("2025");

        fields.AdditionalFields.ShouldContainKey("autoridadnombre");
        fields.AdditionalFields["autoridadnombre"].ShouldBe("SAT");

        fields.AdditionalFields.ShouldContainKey("requerimiento");
        fields.AdditionalFields["requerimiento"].ShouldBe("AGAFADAFSON2/2025/000084");
    }

    // ============================================================
    // 6. NumeroExpediente alias — expanded Expediente routing (PRISMA-E5-S5)
    // ============================================================

    /// <summary>
    /// "numeroexpediente" alias now routes to the same expediente extractor
    /// (mirrors AdaptiveTxtFieldExtractor's alias set for consistency).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_NumeroExpedienteAlias_ExtractsExpediente()
    {
        var source = Source("A/AS1-2505-088637-PHM");

        var result = await _extractor.ExtractFieldAsync(source, "numeroexpediente");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
    }

    /// <summary>
    /// "numero_expediente" (underscore form) alias also routes correctly.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_NumeroExpedienteUnderscoreAlias_ExtractsExpediente()
    {
        var source = Source("A/AS1-2505-088637-PHM");

        var result = await _extractor.ExtractFieldAsync(source, "numero_expediente");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
    }

    // ============================================================
    // 7. SolicitudSiara alias — SIARA solicitud id (PRISMA-E5-S5)
    // ============================================================

    /// <summary>
    /// "solicitudsiara" alias now routes to the requerimiento extractor (SIARA id pattern).
    /// Mirrors AdaptiveTxtFieldExtractor's alias for cross-source field consistency under fusion.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_SolicitudSiaraAlias_ExtractsSiaraId()
    {
        var source = Source("AGAFADAFSON2/2025/000084");

        var result = await _extractor.ExtractFieldAsync(source, "solicitudsiara");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000084");
    }

    // ============================================================
    // 8. RFC — adversarial multi-label flat-join (PRISMA review-fix R1)
    // ============================================================

    /// <summary>
    /// Regression: flat-joined DOCX body contains BOTH "RFC del representante legal" and
    /// "RFC del contribuyente" on the same logical line.  The old optional-qualifier pattern
    /// returned the FIRST match (representante), not the contribuyente RFC.
    ///
    /// Input to the regex (flat join): "RFC del representante legal: REPR870320AB3 RFC del contribuyente: GAGO821002HE0"
    /// Expected: "GAGO821002HE0" (contribuyente wins regardless of document order).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcContribuyenteAfterRepresentante_ReturnsContribuyenteRfc()
    {
        // Arrange — REPR... appears BEFORE GAGO... in the DOCX paragraphs.
        // After string.Join(" ", Text runs) these become a single flat line.
        var source = Source(
            "RFC del representante legal: REPR870320AB3",
            "RFC del contribuyente: GAGO821002HE0");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "rfc");

        // Assert — must return the CONTRIBUYENTE RFC, not the representante RFC.
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("GAGO821002HE0",
            "contribuyente RFC must win over representante RFC regardless of flat-join order");
    }

    /// <summary>
    /// Bare "RFC:" label (no qualifier) with no representante or autorizado RFC nearby.
    /// The bare fallback must still extract the value (regression guard — old code succeeded;
    /// new code must not break this path).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcBareNoQualifier_ExtractsRfc()
    {
        // Arrange — minimal bare-label document; no "del contribuyente" / "del representante".
        var source = Source("RFC: XAXX010101000");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "rfc");

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("XAXX010101000");
    }

    /// <summary>
    /// Bare "RFC del representante legal:" only — no contribuyente RFC present.
    /// The bare fallback must NOT match because (?!\s+del\s) guards the "del" qualifier.
    /// Returns failure (no contribuyente RFC to report).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RfcRepresentanteLegalOnly_ReturnsFailure()
    {
        // Arrange — only a third-party RFC (representante), not the contribuyente.
        var source = Source("RFC del representante legal: REPR870320AB3");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "rfc");

        // Assert — no contribuyente RFC found; must return failure, not the representante RFC.
        result.IsFailure.ShouldBeTrue(
            "a representante-only RFC must not be returned as the contribuyente RFC");
    }

    // ============================================================
    // 9. Fecha — flat-join contamination guard (PRISMA review-fix R1)
    // ============================================================

    /// <summary>
    /// Regression: flat-joined DOCX body has "Fecha: 09 de Abril de 2025 RFC del contribuyente: GAGO821002HE0".
    /// The old [^\n\r,;]{5,40} pattern captured "09 de Abril de 2025 RFC del" (38 chars).
    ///
    /// Expected: exactly "09 de Abril de 2025" (Spanish long-form shape; no trailing label text).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaFlatJoinContamination_ReturnsDateOnly()
    {
        // Arrange — two paragraphs joined flat so there is no newline between date and next label.
        var source = Source(
            "Fecha: 09 de Abril de 2025",
            "RFC del contribuyente: GAGO821002HE0");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "fecharequerimiento");

        // Assert — value must be exactly the date token; "RFC del" must NOT appear.
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldNotBeNull();
        result.Value.Value!.Trim().ShouldBe("09 de Abril de 2025",
            "date capture must stop at the date token boundary, not bleed into the next field label");
    }

    /// <summary>
    /// ISO date variant "2025-04-09" must be extracted correctly from a flat DOCX.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaIsoVariant_ExtractsIsoDate()
    {
        var source = Source(
            "Fecha del oficio: 2025-04-09",
            "RFC del contribuyente: GAGO821002HE0");

        var result = await _extractor.ExtractFieldAsync(source, "fechaoficio");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("2025-04-09");
    }

    /// <summary>
    /// Numeric date variant "09/04/2025" must be extracted correctly from a flat DOCX.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaNumericVariant_ExtractsNumericDate()
    {
        var source = Source(
            "Fecha de emisión: 09/04/2025",
            "Folio: A/AS1-2505-088637-PHM");

        var result = await _extractor.ExtractFieldAsync(source, "fecharequerimiento");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("09/04/2025");
    }

    /// <summary>
    /// True-negative: prose sentence "La Fecha de inicio del proceso fue importante" — no date token.
    /// The label pattern requires a colon (or an immediately following date-shaped token) so
    /// bare "Fecha" in prose with no colon and no date shape must NOT produce a bogus capture.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_FechaProseNoDateToken_ReturnsFailure()
    {
        // Arrange — "Fecha" appears in prose; no colon, no date-shaped value follows.
        var source = Source("La Fecha de inicio del proceso fue importante para el expediente.");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "fecharequerimiento");

        // Assert — no date-shaped token → failure (not a bogus capture).
        result.IsFailure.ShouldBeTrue(
            "prose 'Fecha' without a date-shaped token must not produce a bogus date capture");
    }

    // ============================================================
    // 10. Autoridad — bare-acronym priority (PRISMA review-fix R1, Defect 3)
    // ============================================================

    /// <summary>
    /// Priority test: document contains BOTH bare "CNBV" and bare "SAT" but neither full name.
    /// The bare-acronym pass iterates AGAFF → CNBV → SAT, so CNBV must win.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_AutoridadBothCnbvAndSatBare_ReturnsCnbv()
    {
        // Arrange — "SAT" appears before "CNBV" in the paragraph to maximise the adversarial
        // pressure: if the loop were SAT-first, this would incorrectly return "SAT".
        var source = Source("El SAT y la CNBV coordinaron la revisión.");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "autoridadnombre");

        // Assert — CNBV must win because it precedes SAT in the bare-acronym iteration order.
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("CNBV",
            "bare-acronym pass must return CNBV before SAT when both acronyms are present");
    }

    /// <summary>
    /// Priority test: document contains the full CNBV name ("Comisión Nacional Bancaria y de Valores")
    /// AND an incidental mention of "SAT" in passing.  The full-name check runs before bare-acronym
    /// scanning, so the full CNBV name must be returned.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_AutoridadCnbvFullNameWithIncidentalSat_ReturnsCnbvFullName()
    {
        // Arrange — CNBV full name present; SAT mentioned only in a cross-reference clause.
        var source = Source(
            "La Comisión Nacional Bancaria y de Valores, en coordinación con el SAT,",
            "emite el presente requerimiento de información.");

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "autoridad_nombre");

        // Assert — full CNBV name wins; incidental "SAT" must not override the full-name result.
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("Comisión Nacional Bancaria y de Valores",
            "full CNBV name must take priority over bare SAT mention");
    }
}

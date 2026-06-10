using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="DocxMetadataExtractor"/>. Drives the OpenXml-deterministic
/// extraction end to end (regex field parsing + <c>BuildExtractionMetadata</c> counters) with exact-value
/// assertions. Each helper's true/false/value branches are isolated; date fixtures use culture-unambiguous
/// values (ISO <c>yyyy-MM-dd</c> and DD/DD which read identically in every locale).
/// </summary>
public class DocxMetadataExtractorMutationTests
{
    private readonly DocxMetadataExtractor _extractor =
        new(Substitute.For<ILogger<DocxMetadataExtractor>>());

    private Task<Result<ExtractedMetadata>> Extract(params string[] paragraphs) =>
        _extractor.ExtractFromDocxAsync(BuildDocx(paragraphs), TestContext.Current.CancellationToken);

    // ---------------------------------------------------------------- full happy path + metadata counters

    [Fact]
    public async Task ExtractFromDocxAsync_ExpedienteAreaRfc_PinsFieldsAndQualityMetadata()
    {
        // 3 single-word paragraphs => extracted text "EXP ASEGURAMIENTO RFC" => wordCount 3.
        var result = await Extract("A/AS1-2505-088637-PHM", "ASEGURAMIENTO", "PERJ800101ABC");

        result.IsSuccess.ShouldBeTrue();
        var m = result.Value!;

        m.Expediente.ShouldNotBeNull();
        m.Expediente!.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        m.Expediente.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
        m.RfcValues.ShouldBe(new[] { "PERJ800101ABC" });
        m.Names.ShouldBeNull();
        m.Dates.ShouldBeNull();
        m.LegalReferences.ShouldBeNull();

        var q = m.QualityMetadata.ShouldNotBeNull();
        q.Source.ShouldBe(SourceType.DOCX_OCR_Authority);
        q.TotalFieldsExtracted.ShouldBe(3); // expediente + area + rfc
        q.RegexMatches.ShouldBe(2);          // expediente + rfc-pattern match
        q.CatalogValidations.ShouldBe(1);    // ASEGURAMIENTO is a known catalog area
        q.PatternViolations.ShouldBe(0);
        q.TotalWords.ShouldBe(3);
        q.LowConfidenceWords.ShouldBe(0);    // (int)(3 * 0.15) == 0  (kills the '*' -> '/' arithmetic mutant)
        q.MeanConfidence.ShouldBe(0.70);
        q.MinConfidence.ShouldBe(0.55);
        q.QualityIndex.ShouldBe(0.70);
    }

    [Fact]
    public async Task ExtractFromDocxAsync_CatalogAreaAndTwoRfcs_AggregatesCounters()
    {
        // Two distinct valid RFCs => totalFields = expediente + area + 2 RFCs = 4; regexMatches =
        // expediente + 2 RFC-pattern matches = 3; JUDICIAL catalogued => catalogValidations 1.
        // NOTE: patternViolations is ALWAYS 0 here by construction — see the dead-branch comment below.
        var result = await Extract("A/AS1-2505-088637-PHM", "JUDICIAL", "PERJ800101ABC", "GOMA900202XYZ");

        var q = result.Value!.QualityMetadata.ShouldNotBeNull();
        q.TotalFieldsExtracted.ShouldBe(4);
        q.RegexMatches.ShouldBe(3);
        q.CatalogValidations.ShouldBe(1);
        // patternViolations++ is UNREACHABLE: every value captured by the loose RFC scan is, by construction,
        // a full match of the same pattern, so the anchored ^...$ re-test always passes; and
        // ExtractAreaDescripcion only ever emits a catalogued area or "" (skipped by the IsNullOrWhiteSpace
        // guard). Pinned as an equivalent/dead-branch floor.
        q.PatternViolations.ShouldBe(0);
    }

    // ---------------------------------------------------------------- ExtractExpediente / no-match

    [Fact]
    public async Task ExtractFromDocxAsync_NoExpedientePattern_ExpedienteNullAndNoQualityMetadata()
    {
        var result = await Extract("ningun", "numero", "aqui");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBeNull();
        result.Value.QualityMetadata.ShouldBeNull(); // only built when expediente != null
    }

    // ---------------------------------------------------------------- ExtractAreaDescripcion

    [Theory]
    [InlineData("HACENDARIO", "HACENDARIO")]
    [InlineData("JUDICIAL", "JUDICIAL")]
    public async Task ExtractFromDocxAsync_KnownArea_SetAsAreaDescripcion(string areaWord, string expected)
    {
        var result = await Extract("A/AS1-2505-088637-PHM", areaWord);

        result.Value!.Expediente!.AreaDescripcion.ShouldBe(expected);
    }

    [Fact]
    public async Task ExtractFromDocxAsync_NoAreaWord_AreaDescripcionEmpty()
    {
        var result = await Extract("A/AS1-2505-088637-PHM", "sin", "area");

        result.Value!.Expediente!.AreaDescripcion.ShouldBe(string.Empty);
    }

    // ---------------------------------------------------------------- ExtractAutoridadNombre / LawMandatedFields

    [Theory]
    [InlineData("SUBDELEGACION 5 ZONA NORTE")]
    [InlineData("ADMINISTRACION LOCAL CENTRO")]
    [InlineData("UNIDAD ESPECIALIZADA")]
    public async Task ExtractFromDocxAsync_AuthorityPattern_PopulatesSourceAuthorityCode(string authority)
    {
        var result = await Extract("A/AS1-2505-088637-PHM", authority);

        var law = result.Value!.Expediente!.LawMandatedFields.ShouldNotBeNull();
        law.SourceAuthorityCode.ShouldBe(authority);
    }

    [Fact]
    public async Task ExtractFromDocxAsync_AreaButNoAuthority_LawMandatedFieldsHasRequirementTypeOnly()
    {
        var result = await Extract("A/AS1-2505-088637-PHM", "ASEGURAMIENTO");

        var law = result.Value!.Expediente!.LawMandatedFields.ShouldNotBeNull();
        law.SourceAuthorityCode.ShouldBeNull();      // no authority pattern
        law.RequirementType.ShouldBe("ASEGURAMIENTO"); // area present
    }

    [Fact]
    public async Task ExtractFromDocxAsync_NoAuthorityNoArea_LawMandatedFieldsNull()
    {
        // expediente matches but neither authority nor area present -> hasData false -> null.
        var result = await Extract("A/AS1-2505-088637-PHM", "texto", "irrelevante");

        result.Value!.Expediente!.LawMandatedFields.ShouldBeNull();
    }

    // ---------------------------------------------------------------- ExtractRfcValues

    [Fact]
    public async Task ExtractFromDocxAsync_DuplicateRfc_DistinctSingleValue()
    {
        var result = await Extract("A/AS1-2505-088637-PHM", "PERJ800101ABC", "PERJ800101ABC");

        result.Value!.RfcValues.ShouldBe(new[] { "PERJ800101ABC" }); // Distinct collapses the duplicate
    }

    // ---------------------------------------------------------------- ExtractNames

    [Fact]
    public async Task ExtractFromDocxAsync_CapitalizedNameSequence_ExtractedIntoNames()
    {
        // Single name, no other Capital+lowercase tokens => exact value; kills Take(10)->Skip(10).
        var result = await Extract("A/AS1-2505-088637-PHM", "Juan", "Perez");

        result.Value!.Names.ShouldBe(new[] { "Juan Perez" });
    }

    // ---------------------------------------------------------------- ExtractDates

    [Fact]
    public async Task ExtractFromDocxAsync_IsoDate_ParsedIntoDates()
    {
        var result = await Extract("A/AS1-2505-088637-PHM", "2025-01-15");

        result.Value!.Dates.ShouldBe(new[] { new DateTime(2025, 1, 15) });
    }

    [Fact]
    public async Task ExtractFromDocxAsync_SlashDate_ParsedIntoDates()
    {
        // 12/12/2025 reads as 12-Dec-2025 in every culture (kills the DD/MM/YYYY pattern removal).
        var result = await Extract("A/AS1-2505-088637-PHM", "12/12/2025");

        result.Value!.Dates.ShouldBe(new[] { new DateTime(2025, 12, 12) });
    }

    [Fact]
    public async Task ExtractFromDocxAsync_DashDate_ParsedIntoDates()
    {
        // 12-12-2025 only matches the DD-MM-YYYY pattern and reads identically in all cultures.
        var result = await Extract("A/AS1-2505-088637-PHM", "12-12-2025");

        result.Value!.Dates.ShouldBe(new[] { new DateTime(2025, 12, 12) });
    }

    // ---------------------------------------------------------------- ExtractLegalReferences

    [Fact]
    public async Task ExtractFromDocxAsync_LeyReference_ExtractedIntoLegalReferences()
    {
        var result = await Extract("A/AS1-2505-088637-PHM", "LEY ABC123");

        result.Value!.LegalReferences.ShouldNotBeNull();
        result.Value.LegalReferences!.ShouldContain("LEY ABC123");
    }

    // ---------------------------------------------------------------- unsupported formats / errors

    [Fact]
    public async Task ExtractFromDocxAsync_InvalidBytes_ReturnsFailure()
    {
        var result = await _extractor.ExtractFromDocxAsync(new byte[] { 1, 2, 3, 4 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExtractFromDocxAsync_NoBodyDocx_ReturnsFailureMentioningBody()
    {
        var result = await _extractor.ExtractFromDocxAsync(BuildDocxNoBody(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("no body");
    }

    [Fact]
    public async Task ExtractFromDocxAsync_NoMainDocumentPart_ReturnsFailureMentioningMainPart()
    {
        var result = await _extractor.ExtractFromDocxAsync(BuildDocxNoMainPart(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("no main document part");
    }

    [Fact]
    public async Task ExtractFromXmlAsync_ReturnsFailureMentioningXmlExtractor()
    {
        var result = await _extractor.ExtractFromXmlAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("XmlMetadataExtractor");
    }

    [Fact]
    public async Task ExtractFromPdfAsync_ReturnsFailureMentioningPdfExtractor()
    {
        var result = await _extractor.ExtractFromPdfAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("PdfMetadataExtractor");
    }

    // ---------------------------------------------------------------- ExtractTextAsync

    [Fact]
    public async Task ExtractTextAsync_ValidDocx_ReturnsJoinedText()
    {
        var bytes = BuildDocx("Hola", "Mundo");

        var result = await _extractor.ExtractTextAsync(bytes, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hola Mundo");
    }

    [Fact]
    public async Task ExtractTextAsync_PreCancelled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _extractor.ExtractTextAsync(BuildDocx("x"), cts.Token);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_NoBodyDocx_ReturnsFailureMentioningBody()
    {
        var result = await _extractor.ExtractTextAsync(BuildDocxNoBody(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("no body");
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidBytes_ReturnsFailureFromCatch()
    {
        // WordprocessingDocument.Open on garbage throws -> generic catch wraps it.
        var result = await _extractor.ExtractTextAsync(new byte[] { 1, 2, 3, 4 }, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Error extracting DOCX text");
    }

    [Fact]
    public async Task ExtractTextAsync_NoMainDocumentPart_ReturnsFailureMentioningMainPart()
    {
        var result = await _extractor.ExtractTextAsync(BuildDocxNoMainPart(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("no main document part");
    }

    // ================================================================ builder

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
                var paragraph = body.AppendChild(new Paragraph());
                paragraph.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] BuildDocxNoBody()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(); // intentionally no Body appended
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] BuildDocxNoMainPart()
    {
        using var stream = new MemoryStream();
        using (WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            // Valid OPC package with no MainDocumentPart added.
        }

        return stream.ToArray();
    }
}

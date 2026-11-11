using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="DocxFieldExtractor"/>. Pins the field-name <c>switch</c> routing
/// (in both <c>ExtractFieldByName</c> and <c>ApplyFieldToExtractedFields</c>), the case-insensitive lookup,
/// the regex captures, the <see cref="FieldValue"/> shape, and the content/path/neither source branches.
/// Each regex field is isolated as the trailing text so the <c>[^\n\r]+</c> capture is exact.
/// </summary>
public class DocxFieldExtractorMutationTests
{
    private readonly DocxFieldExtractor _extractor =
        new(Substitute.For<ILogger<DocxFieldExtractor>>());

    private static DocxSource Source(params string[] paragraphs) => new(BuildDocx(paragraphs));

    // ---------------------------------------------------------------- ExtractFieldsAsync routing

    [Fact]
    public async Task ExtractFieldsAsync_ExpedienteDefinition_SetsExpedienteOnly()
    {
        var result = await _extractor.ExtractFieldsAsync(
            Source("A/AS1-2505-088637-PHM"),
            new[] { new FieldDefinition("Expediente") });

        result.IsSuccess.ShouldBeTrue();
        var fields = result.Value!;
        fields.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        fields.Causa.ShouldBeNull();
        fields.AccionSolicitada.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractFieldsAsync_CausaDefinition_SetsCausaOnly()
    {
        // Causa is the only/last content so [^\n\r]+ captures exactly its value.
        var result = await _extractor.ExtractFieldsAsync(
            Source("CAUSA: Bloqueo total de cuentas"),
            new[] { new FieldDefinition("Causa") });

        var fields = result.Value!;
        fields.Causa.ShouldBe("Bloqueo total de cuentas");
        fields.Expediente.ShouldBeNull();
        fields.AccionSolicitada.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractFieldsAsync_AccionSolicitadaDefinition_SetsAccionOnly()
    {
        var result = await _extractor.ExtractFieldsAsync(
            Source("ACCIÓN SOLICITADA: Embargar los bienes"),
            new[] { new FieldDefinition("AccionSolicitada") });

        var fields = result.Value!;
        fields.AccionSolicitada.ShouldBe("Embargar los bienes");
        fields.Expediente.ShouldBeNull();
        fields.Causa.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractFieldsAsync_AccionUnderscoreKey_AlsoSetsAccion()
    {
        // Kills the "accion_solicitada" arm specifically (separate from "accionsolicitada").
        var result = await _extractor.ExtractFieldsAsync(
            Source("ACCIÓN SOLICITADA: Congelar fondos"),
            new[] { new FieldDefinition("accion_solicitada") });

        result.Value!.AccionSolicitada.ShouldBe("Congelar fondos");
    }

    [Fact]
    public async Task ExtractFieldsAsync_UnknownFieldDefinition_LeavesFieldsEmpty()
    {
        var result = await _extractor.ExtractFieldsAsync(
            Source("A/AS1-2505-088637-PHM"),
            new[] { new FieldDefinition("DoesNotExist") });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldBeNull();
    }

    // ---------------------------------------------------------------- source resolution branches

    [Fact]
    public async Task ExtractFieldsAsync_FromFilePath_ReadsFileContent()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"docxfx_{Guid.NewGuid():N}.docx");
        await File.WriteAllBytesAsync(tempFile, BuildDocx("A/AS1-2505-088637-PHM"), TestContext.Current.CancellationToken);
        try
        {
            var result = await _extractor.ExtractFieldsAsync(
                new DocxSource(tempFile),
                new[] { new FieldDefinition("Expediente") });

            result.IsSuccess.ShouldBeTrue();
            result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExtractFieldsAsync_NoContentNoPath_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldsAsync(
            new DocxSource(),
            new[] { new FieldDefinition("Expediente") });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileContent or valid FilePath");
    }

    [Fact]
    public async Task ExtractFieldsAsync_InvalidDocxBytes_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldsAsync(
            new DocxSource(new byte[] { 1, 2, 3, 4 }),
            new[] { new FieldDefinition("Expediente") });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Failed to extract text from DOCX");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NoBodyDocx_ReturnsFailureMentioningBody()
    {
        var result = await _extractor.ExtractFieldsAsync(
            new DocxSource(BuildDocxNoBody()),
            new[] { new FieldDefinition("Expediente") });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("no body");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NoMainDocumentPart_ReturnsFailureMentioningMainPart()
    {
        var result = await _extractor.ExtractFieldsAsync(
            new DocxSource(BuildDocxNoMainPart()),
            new[] { new FieldDefinition("Expediente") });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("no main document part");
    }

    [Fact]
    public async Task ExtractFieldsAsync_NullFieldDefinitions_ReturnsFailureFromCatch()
    {
        // Valid content, but the foreach over a null fieldDefinitions throws -> exercises the catch block.
        var result = await _extractor.ExtractFieldsAsync(Source("A/AS1-2505-088637-PHM"), null!);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Error extracting DOCX fields");
    }

    // ---------------------------------------------------------------- ExtractFieldAsync (single)

    [Fact]
    public async Task ExtractFieldAsync_Expediente_ReturnsFullFieldValue()
    {
        var result = await _extractor.ExtractFieldAsync(Source("A/AS1-2505-088637-PHM"), "Expediente");

        result.IsSuccess.ShouldBeTrue();
        var fv = result.Value!;
        fv.FieldName.ShouldBe("Expediente");
        fv.Value.ShouldBe("A/AS1-2505-088637-PHM");
        fv.Confidence.ShouldBe(1.0f);
        fv.SourceType.ShouldBe("DOCX");
        fv.Origin.ShouldBe(FieldOrigin.Docx);
    }

    [Fact]
    public async Task ExtractFieldAsync_MixedCaseFieldName_StillResolves()
    {
        // Proves fieldName.ToLowerInvariant() in ExtractFieldByName.
        var result = await _extractor.ExtractFieldAsync(Source("A/AS1-2505-088637-PHM"), "ExPeDiEnTe");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldAsync_FieldNotPresentInText_ReturnsFailure()
    {
        // Known field name but the text has no expediente pattern -> ExtractExpediente returns null.
        var result = await _extractor.ExtractFieldAsync(Source("sin numero de expediente"), "Expediente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ExtractFieldAsync_UnknownFieldName_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(Source("A/AS1-2505-088637-PHM"), "Nope");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ExtractFieldAsync_Causa_LowercaseMarkerStillMatches()
    {
        // IgnoreCase regex: lowercase "causa:" still captures.
        var result = await _extractor.ExtractFieldAsync(Source("causa: revision fiscal"), "Causa");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("revision fiscal");
    }

    [Fact]
    public async Task ExtractFieldAsync_FromFilePath_ReadsFileContent()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"docxfx_{Guid.NewGuid():N}.docx");
        await File.WriteAllBytesAsync(tempFile, BuildDocx("A/AS1-2505-088637-PHM"), TestContext.Current.CancellationToken);
        try
        {
            var result = await _extractor.ExtractFieldAsync(new DocxSource(tempFile), "Expediente");

            result.IsSuccess.ShouldBeTrue();
            result.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExtractFieldAsync_NoContentNoPath_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(new DocxSource(), "Expediente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileContent or valid FilePath");
    }

    [Fact]
    public async Task ExtractFieldAsync_InvalidDocxBytes_ReturnsFailure()
    {
        var result = await _extractor.ExtractFieldAsync(new DocxSource(new byte[] { 1, 2, 3, 4 }), "Expediente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Failed to extract text from DOCX");
    }

    [Fact]
    public async Task ExtractFieldAsync_NullFieldName_ReturnsFailureFromCatch()
    {
        // fieldName.ToLowerInvariant() on a null name throws -> exercises the ExtractFieldAsync catch block.
        var result = await _extractor.ExtractFieldAsync(Source("A/AS1-2505-088637-PHM"), null!);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Error extracting field from DOCX");
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

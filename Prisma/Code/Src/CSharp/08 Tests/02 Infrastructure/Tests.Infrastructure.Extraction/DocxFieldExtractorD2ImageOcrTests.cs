using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for the D2 image-OCR path of <see cref="DocxFieldExtractor"/>.
/// All OCR calls use NSubstitute — no live Tesseract engine (avoids same-process second-init deadlock).
/// </summary>
public class DocxFieldExtractorD2ImageOcrTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DocxFieldExtractor MakeExtractor(IOcrExecutor? ocrExecutor, ITestOutputHelper? output = null)
    {
        var logger = output != null
            ? XUnitLogger.CreateLogger<DocxFieldExtractor>(output)
            : Substitute.For<ILogger<DocxFieldExtractor>>();

        return new DocxFieldExtractor(logger, ocrExecutor);
    }

    /// <summary>Builds a minimal valid DOCX byte array whose body contains <paramref name="text"/>.</summary>
    private static byte[] BuildDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            body.AppendChild(new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Configures a substitute <see cref="IOcrExecutor"/> to return a canned <see cref="OCRResult"/>
    /// for every call (any <see cref="ImageData"/> / <see cref="OCRConfig"/>).
    /// </summary>
    private static IOcrExecutor StubOcr(string ocrText, float confidence = 80f)
    {
        var executor = Substitute.For<IOcrExecutor>();
        var ocrResult = new OCRResult(ocrText, confidence, confidence, new List<float> { confidence }, "spa");
        executor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
                .Returns(Task.FromResult(Result<OCRResult>.Success(ocrResult)));
        return executor;
    }

    private static IOcrExecutor StubOcrFailing()
    {
        var executor = Substitute.For<IOcrExecutor>();
        executor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
                .Returns(Task.FromResult(Result<OCRResult>.WithFailure("OCR engine error")));
        return executor;
    }

    private static IOcrExecutor StubOcrThrowing()
    {
        var executor = Substitute.For<IOcrExecutor>();
        executor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
                .Returns<Task<Result<OCRResult>>>(_ => throw new InvalidOperationException("Tesseract crash"));
        return executor;
    }

    // -----------------------------------------------------------------------
    // 1. Null executor → OCR path skipped; existing text extraction unaffected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_NullOcrExecutor_SkipsImageOcrAndExtractsTextFieldsNormally()
    {
        // Arrange: no OCR executor injected
        var extractor = MakeExtractor(null);
        var docxBytes = BuildDocx("A/AS1-2505-088637-PHM");
        var source = new DocxSource(docxBytes);

        // Act
        var result = await extractor.ExtractFieldsAsync(source, new[] { new FieldDefinition("Expediente") });

        // Assert: text extraction works; no Remitente in AdditionalFields (OCR path skipped)
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.AdditionalFields.ContainsKey("Remitente").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldAsync_NullOcrExecutorAndRemitenteField_ReturnsFailureWithoutThrow()
    {
        // Arrange: no OCR executor — "remitente" falls through to text-extraction path,
        // which won't find it in a plain-text DOCX → returns failure (not throw)
        var extractor = MakeExtractor(null);
        var source = new DocxSource(BuildDocx("plain text without remitente pattern"));

        // Act
        var result = await extractor.ExtractFieldAsync(source, "remitente");

        // Assert: graceful failure, no exception
        result.IsFailure.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // 2. Embedded images are enumerated and passed to the executor
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_DocxWithNoImageParts_DoesNotCallOcr()
    {
        // Arrange: DOCX with no embedded images
        var executor = Substitute.For<IOcrExecutor>();
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocx("A/AS1-2505-088637-PHM"));

        // Act
        var result = await extractor.ExtractFieldsAsync(source, new[] { new FieldDefinition("Expediente") });

        // Assert: success; OCR never called
        result.IsSuccess.ShouldBeTrue(result.Error);
        await executor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }

    // -----------------------------------------------------------------------
    // 3. Plausible remitente name is parsed from canned OCR text (atentamente path)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Atentamente\nJuan Garcia Lopez\nDirector General", "Juan Garcia Lopez")]
    [InlineData("Atentamente,\nMaria Fernanda Ruiz\nSubdirectora", "Maria Fernanda Ruiz")]
    [InlineData("texto previo\nAtentamente\n\nAlejandro Torres Mendoza\nFirmante", "Alejandro Torres Mendoza")]
    public void ParseRemitenteFromOcrText_AtentamenteContext_ReturnsNameOnNextLine(
        string ocrText, string expectedName)
    {
        var result = DocxFieldExtractor.ParseRemitenteFromOcrText(ocrText);

        result.ShouldBe(expectedName);
    }

    [Theory]
    [InlineData("Juan Garcia Lopez")]         // exactly 3 words, all capitalised
    [InlineData("Maria Fernanda Ruiz Perez")] // 4 words
    public void ParseRemitenteFromOcrText_NoAtentamente_FallsBackToFirstPlausibleName(string ocrText)
    {
        var result = DocxFieldExtractor.ParseRemitenteFromOcrText(ocrText);

        result.ShouldBe(ocrText.Trim());
    }

    // -----------------------------------------------------------------------
    // 4. OCR failure / low confidence / empty → remitente unset, no throw (fail-open)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_OcrReturnsFailure_DoesNotSetRemitenteAndDoesNotThrow()
    {
        var executor = StubOcrFailing();
        var extractor = MakeExtractor(executor);
        // Use the real 222AAA docx if available; otherwise a synthetic one with a fake image part
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("Remitente").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_OcrReturnsLowConfidence_DoesNotSetRemitente()
    {
        // Confidence below MinimumRemitenteConfidence (30f)
        var executor = StubOcr("Atentamente\nJose Perez Garcia", confidence: 10f);
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("Remitente").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_OcrReturnsEmptyText_DoesNotSetRemitente()
    {
        var executor = StubOcr(string.Empty, confidence: 80f);
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("Remitente").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_OcrReturnsNonNameText_DoesNotSetRemitente()
    {
        // Valid confidence, but text has no plausible person name
        var executor = StubOcr("SELLO OFICIAL\nMINISTERIO\n12345", confidence: 80f);
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ContainsKey("Remitente").ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractFieldsAsync_OcrExecutorThrows_FailsOpenAndDoesNotThrow()
    {
        var executor = StubOcrThrowing();
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        // Must not throw; must succeed without Remitente
        var result = await extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue("Image-OCR path must be fail-open — never propagate OCR exceptions.");
        result.Value!.AdditionalFields.ContainsKey("Remitente").ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // 5. Successful OCR → Remitente set in AdditionalFields
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_OcrReturnsPlausibleName_SetsRemitenteInAdditionalFields()
    {
        var executor = StubOcr("Atentamente\nCarlos Herrera Soto\nJefe de Division", confidence: 75f);
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldsAsync(source, Array.Empty<FieldDefinition>());

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.AdditionalFields.ShouldContainKey("Remitente");
        result.Value.AdditionalFields["Remitente"].ShouldBe("Carlos Herrera Soto");
    }

    // -----------------------------------------------------------------------
    // 6. ExtractFieldAsync("remitente") path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldAsync_RemitenteWithOcr_ReturnsFieldValueWithDocxImageSource()
    {
        var executor = StubOcr("Atentamente\nLuis Alberto Gomez\nComisionado", confidence: 70f);
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldAsync(source, "remitente");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("Luis Alberto Gomez");
        result.Value.SourceType.ShouldBe("DOCX-Image");
        result.Value.Origin.ShouldBe(ExxerCube.Prisma.Domain.Enums.FieldOrigin.Docx);
    }

    [Fact]
    public async Task ExtractFieldAsync_RemitenteWithFailingOcr_ReturnsFailureWithoutThrow()
    {
        var executor = StubOcrFailing();
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        var result = await extractor.ExtractFieldAsync(source, "remitente");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractFieldAsync_RemitenteWithThrowingOcr_ReturnsFailureWithoutThrow()
    {
        var executor = StubOcrThrowing();
        var extractor = MakeExtractor(executor);
        var source = new DocxSource(BuildDocxWithFakePng());

        // The outer catch in ExtractFieldAsync must catch re-thrown executor exceptions too.
        // ExtractRemitenteFromImagesAsync catches per-image throws → returns null → failure result.
        var result = await extractor.ExtractFieldAsync(source, "remitente");

        result.IsFailure.ShouldBeTrue("Throwing OCR executor should produce a graceful failure, never a throw.");
    }

    // -----------------------------------------------------------------------
    // 7. Existing text extraction is unaffected when OCR executor is present
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFieldsAsync_OcrExecutorPresent_TextFieldExtractionStillWorks()
    {
        var executor = StubOcr("Atentamente\nPedro Sanchez Vega\n", confidence: 80f);
        var extractor = MakeExtractor(executor);
        var docxBytes = BuildDocxWithFakePng(bodyText: "A/AS1-2505-088637-PHM");
        var source = new DocxSource(docxBytes);

        var result = await extractor.ExtractFieldsAsync(source, new[]
        {
            new FieldDefinition("Expediente"),
        });

        result.IsSuccess.ShouldBeTrue(result.Error);
        // Text field extracted correctly alongside image-OCR
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        // Remitente also set from image
        result.Value.AdditionalFields.ShouldContainKey("Remitente");
        result.Value.AdditionalFields["Remitente"].ShouldBe("Pedro Sanchez Vega");
    }

    // -----------------------------------------------------------------------
    // 8. ParseRemitenteFromOcrText — edge cases
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseRemitenteFromOcrText_NullOrWhitespace_ReturnsNull(string? input)
    {
        DocxFieldExtractor.ParseRemitenteFromOcrText(input).ShouldBeNull();
    }

    [Theory]
    [InlineData("SELLO OFICIAL REPUBLICA")]  // all-caps → not a name
    [InlineData("codigo: 12345")]             // has digit
    [InlineData("fin")]                       // too short / single word
    [InlineData("ACCIÓN SOLICITADA:")]        // ends with colon
    public void ParseRemitenteFromOcrText_NonNameLines_ReturnsNull(string input)
    {
        // When none of the lines are plausible names, result is null
        DocxFieldExtractor.ParseRemitenteFromOcrText(input).ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // DOCX builders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a DOCX with a single minimal PNG embedded as an ImagePart.
    /// The PNG content is a valid 1×1 red pixel (minimal valid PNG bytes).
    /// </summary>
    private static byte[] BuildDocxWithFakePng(string? bodyText = null)
    {
        // Minimal valid 1×1 PNG: IHDR + IDAT (red pixel) + IEND
        var minimalPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR length + type
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // bit depth 8, color type 2 (RGB)
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT length + type
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, // IDAT data
            0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, // IDAT CRC
            0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND length + type
            0x44, 0xAE, 0x42, 0x60, 0x82                     // IEND data + CRC
        };

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Add text content if requested
            if (!string.IsNullOrEmpty(bodyText))
            {
                body.AppendChild(new Paragraph(new Run(new Text(bodyText) { Space = SpaceProcessingModeValues.Preserve })));
            }
            else
            {
                body.AppendChild(new Paragraph(new Run(new Text("test content"))));
            }

            // Embed the PNG as an ImagePart
            var imagePart = mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png);
            using (var imgStream = new MemoryStream(minimalPng))
            {
                imagePart.FeedData(imgStream);
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }
}

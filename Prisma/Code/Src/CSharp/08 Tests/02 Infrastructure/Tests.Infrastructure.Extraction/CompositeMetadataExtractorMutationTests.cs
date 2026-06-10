using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Delegation tests for <see cref="CompositeMetadataExtractor"/>. The composite takes the CONCRETE
/// XML/DOCX/PDF extractors (not interfaces), so each route is driven through a real collaborator wired to
/// emit a uniquely identifiable result; the test then asserts the composite surfaced THAT collaborator's
/// output — pinning that each method delegates to the right extractor and that <c>ExtractTextAsync</c>
/// routes to the PDF extractor.
/// </summary>
public class CompositeMetadataExtractorMutationTests
{
    private readonly IXmlNullableParser<Expediente> _xmlParser = Substitute.For<IXmlNullableParser<Expediente>>();
    private readonly IImagePreprocessor _imagePreprocessor = Substitute.For<IImagePreprocessor>();
    private readonly IOcrExecutor _ocrExecutor = Substitute.For<IOcrExecutor>();
    private readonly CompositeMetadataExtractor _composite;

    public CompositeMetadataExtractorMutationTests()
    {
        var xml = new XmlMetadataExtractor(_xmlParser, Substitute.For<ILogger<XmlMetadataExtractor>>());
        var docx = new DocxMetadataExtractor(Substitute.For<ILogger<DocxMetadataExtractor>>());
        var pdf = new PdfMetadataExtractor(_ocrExecutor, _imagePreprocessor, Substitute.For<ILogger<PdfMetadataExtractor>>());
        _composite = new CompositeMetadataExtractor(xml, docx, pdf, Substitute.For<ILogger<CompositeMetadataExtractor>>());
    }

    private void OcrYields(string text)
    {
        _imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(new ImageData { Data = new byte[] { 1 }, SourcePath = "p" }));
        _ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(new OCRResult { Text = text }));
    }

    [Fact]
    public async Task ExtractFromXmlAsync_DelegatesToXmlExtractor()
    {
        _xmlParser.ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.Success(new Expediente { NumeroExpediente = "XML-ROUTE-7" }));

        var result = await _composite.ExtractFromXmlAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente!.NumeroExpediente.ShouldBe("XML-ROUTE-7");
        await _ocrExecutor.DidNotReceive().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }

    [Fact]
    public async Task ExtractFromDocxAsync_DelegatesToDocxExtractor()
    {
        var docxBytes = BuildDocx("A/DX1-1234-567890-ABC");

        var result = await _composite.ExtractFromDocxAsync(docxBytes, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente!.NumeroExpediente.ShouldBe("A/DX1-1234-567890-ABC");
        await _xmlParser.DidNotReceive().ParseAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractFromPdfAsync_DelegatesToPdfExtractor()
    {
        OcrYields("A/PD1-2222-222222-PPP");

        var result = await _composite.ExtractFromPdfAsync(new byte[] { 0x25, 0x50 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente!.NumeroExpediente.ShouldBe("A/PD1-2222-222222-PPP");
    }

    [Fact]
    public async Task ExtractTextAsync_DelegatesToPdfExtractor()
    {
        OcrYields("ROUTED-PDF-TEXT-via-composite");

        var result = await _composite.ExtractTextAsync(new byte[] { 0x25, 0x50 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("ROUTED-PDF-TEXT-via-composite");
        await _ocrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }

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
}

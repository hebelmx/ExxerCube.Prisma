namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Performance tests for <see cref="MetadataExtractionService"/> to verify NFR4 and NFR5 requirements.
/// NOTE: Temporarily disabled - uses Infrastructure.Extraction and Infrastructure.FileStorage types. Should be moved to appropriate Infrastructure test projects or refactored to use mocks.
/// </summary>
// TODO: Move to Tests.Infrastructure.Extraction or refactor to mock Infrastructure dependencies
/*
public class MetadataExtractionPerformanceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ILogger<FileTypeIdentifierService> _fileTypeLogger;
    private readonly ILogger<XmlExpedienteParser> _xmlParserLogger;
    private readonly ILogger<XmlMetadataExtractor> _xmlExtractorLogger;
    private readonly ILogger<DocxMetadataExtractor> _docxExtractorLogger;
    private readonly ILogger<PdfMetadataExtractor> _pdfExtractorLogger;
    private readonly ILogger<CompositeMetadataExtractor> _compositeExtractorLogger;
    private readonly ILogger<FileClassifierService> _classifierLogger;
    private readonly ILogger<SafeFileNamerService> _fileNamerLogger;
    private readonly ILogger<FileMoverService> _fileMoverLogger;
    private readonly ILogger<MetadataExtractionService> _serviceLogger;
    private readonly MetadataExtractionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataExtractionPerformanceTests"/> class.
    /// </summary>
    public MetadataExtractionPerformanceTests(ITestOutputHelper output)
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        _fileTypeLogger = XUnitLogger.CreateLogger<FileTypeIdentifierService>(output);
        _xmlParserLogger = XUnitLogger.CreateLogger<XmlExpedienteParser>(output);
        _xmlExtractorLogger = XUnitLogger.CreateLogger<XmlMetadataExtractor>(output);
        _docxExtractorLogger = XUnitLogger.CreateLogger<DocxMetadataExtractor>(output);
        _pdfExtractorLogger = XUnitLogger.CreateLogger<PdfMetadataExtractor>(output);
        _compositeExtractorLogger = XUnitLogger.CreateLogger<CompositeMetadataExtractor>(output);
        _classifierLogger = XUnitLogger.CreateLogger<FileClassifierService>(output);
        _fileNamerLogger = XUnitLogger.CreateLogger<SafeFileNamerService>(output);
        _fileMoverLogger = XUnitLogger.CreateLogger<FileMoverService>(output);
        _serviceLogger = XUnitLogger.CreateLogger<MetadataExtractionService>(output);

        var fileTypeIdentifier = new FileTypeIdentifierService(_fileTypeLogger);
        var xmlParser = new XmlExpedienteParser(_xmlParserLogger);
        var xmlExtractor = new XmlMetadataExtractor(xmlParser, _xmlExtractorLogger);
        var docxExtractor = new DocxMetadataExtractor(_docxExtractorLogger);
        var imagePreprocessor = Substitute.For<IImagePreprocessor>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var pdfExtractor = new PdfMetadataExtractor(ocrExecutor, imagePreprocessor, _pdfExtractorLogger);
        var metadataExtractor = new CompositeMetadataExtractor(xmlExtractor, docxExtractor, pdfExtractor, _compositeExtractorLogger);
        var fileClassifier = new FileClassifierService(_classifierLogger);
        var safeFileNamer = new SafeFileNamerService(_fileNamerLogger);
        var storageOptions = Options.Create(new FileStorageOptions { BaseStoragePath = _tempDirectory });
        var fileMover = new FileMoverService(_fileMoverLogger, storageOptions);
        var auditLogger = Substitute.For<IAuditLogger>();

        _service = new MetadataExtractionService(
            fileTypeIdentifier,
            metadataExtractor,
            fileClassifier,
            safeFileNamer,
            fileMover,
            auditLogger,
            _serviceLogger);
    }

    /// <summary>
    /// NFR4: Verifies that XML metadata extraction completes within 2 seconds.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessFileAsync_XmlFile_CompletesWithin2Seconds()
    {
        // Arrange
        var xmlContent = @"<?xml version=""1.0""?><Expediente><NumeroExpediente>TEST-001</NumeroExpediente><AreaDescripcion>DOCUMENTACION</AreaDescripcion></Expediente>";
        var testFile = Path.Combine(_tempDirectory, "test.xml");
        await File.WriteAllTextAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert - NFR4: XML extraction should complete within 2 seconds
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000,
            $"XML metadata extraction took {stopwatch.ElapsedMilliseconds}ms, exceeding 2 second target (NFR4)");
    }

    /// <summary>
    /// NFR4: Verifies that DOCX metadata extraction completes within 2 seconds.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ProcessFileAsync_DocxFile_CompletesWithin2Seconds()
    {
        // Arrange
        // Create minimal DOCX file for testing
        var docxBytes = CreateMinimalDocx();
        var testFile = Path.Combine(_tempDirectory, "test.docx");
        await File.WriteAllBytesAsync(testFile, docxBytes, TestContext.Current.CancellationToken);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.ProcessFileAsync(testFile, "test.docx", cancellationToken: TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert - NFR4: DOCX extraction should complete within 2 seconds
        // Note: May fail if DOCX extraction is slow, but documents performance requirement
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000,
            $"DOCX metadata extraction took {stopwatch.ElapsedMilliseconds}ms, exceeding 2 second target (NFR4)");
    }

    /// <summary>
    /// Creates a minimal valid DOCX file for testing.
    /// </summary>
    private static byte[] CreateMinimalDocx()
    {
        using var stream = new MemoryStream();
        using (var wordDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            var paragraph = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            paragraph.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run(
                new DocumentFormat.OpenXml.Wordprocessing.Text("Test")));
            mainPart.Document.Save();
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Disposes test resources.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
*/
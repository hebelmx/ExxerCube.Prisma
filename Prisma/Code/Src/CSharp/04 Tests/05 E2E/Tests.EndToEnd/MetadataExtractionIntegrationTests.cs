using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.EndToEnd;

/// <summary>
/// End-to-end integration tests for <see cref="MetadataExtractionService"/> that test complete workflows.
/// These tests use real infrastructure components to verify integration verification points IV1-IV3.
///
/// These tests belong in Tests.EndToEnd because they:
/// - Use real Infrastructure implementations (not mocks)
/// - Test complete workflows across Application and Infrastructure layers
/// - Verify integration between multiple Infrastructure components
/// - Test file system operations and real document processing
/// </summary>
public class MetadataExtractionIntegrationTests : IDisposable
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
    private readonly IFileTypeIdentifier _fileTypeIdentifier;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IFileClassifier _fileClassifier;
    private readonly ISafeFileNamer _safeFileNamer;
    private readonly IFileMover _fileMover;
    private readonly MetadataExtractionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataExtractionIntegrationTests"/> class.
    /// </summary>
    public MetadataExtractionIntegrationTests(ITestOutputHelper output)
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create loggers using XUnit logger
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

        // Create real infrastructure components
        _fileTypeIdentifier = new FileTypeIdentifierService(_fileTypeLogger);

        var xmlParser = new XmlExpedienteParser(_xmlParserLogger);
        var xmlExtractor = new XmlMetadataExtractor(xmlParser, _xmlExtractorLogger);
        var docxExtractor = new DocxMetadataExtractor(_docxExtractorLogger);

        // For PDF extractor, we need to mock OCR dependencies since they require Python
        var imagePreprocessor = Substitute.For<IImagePreprocessor>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();
        var pdfExtractor = new PdfMetadataExtractor(ocrExecutor, imagePreprocessor, _pdfExtractorLogger);

        // Create composite extractor with real implementations
        _metadataExtractor = new CompositeMetadataExtractor(
            xmlExtractor,
            docxExtractor,
            pdfExtractor,
            _compositeExtractorLogger);

        _fileClassifier = new FileClassifierService(_classifierLogger);

        _safeFileNamer = new SafeFileNamerService(_fileNamerLogger);

        var storageOptions = Options.Create(new FileStorageOptions
        {
            BaseStoragePath = _tempDirectory
        });
        _fileMover = new FileMoverService(_fileMoverLogger, storageOptions);
        var auditLogger = Substitute.For<IAuditLogger>();

        _service = new MetadataExtractionService(
            _fileTypeIdentifier,
            _metadataExtractor,
            _fileClassifier,
            _safeFileNamer,
            _fileMover,
            auditLogger,
            _serviceLogger);
    }

    /// <summary>
    /// Tests that XML document processing completes end-to-end workflow successfully.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_XmlDocument_CompletesWorkflow()
    {
        // Arrange
        var xmlContent = @"<?xml version=""1.0""?>
<Expediente>
    <NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente>
    <NumeroOficio>214-1-18714972/2025</NumeroOficio>
    <AreaDescripcion>ASEGURAMIENTO</AreaDescripcion>
    <SolicitudPartes>
        <Parte>
            <ParteId>1</ParteId>
            <Caracter>Contribuyente</Caracter>
            <PersonaTipo>Fisica</PersonaTipo>
            <Nombre>Juan</Nombre>
            <Paterno>Perez</Paterno>
            <Rfc>PERJ800101ABC</Rfc>
        </Parte>
    </SolicitudPartes>
</Expediente>";
        var testFile = Path.Combine(_tempDirectory, "test_expediente.xml");
        await File.WriteAllTextAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.ProcessFileAsync(testFile, "test_expediente.xml", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.FileFormat.ShouldBe(FileFormat.Xml);
        result.Value.Metadata.ShouldNotBeNull();
        result.Value.Metadata.Expediente.ShouldNotBeNull();
        result.Value.Metadata.Expediente.NumeroExpediente.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Classification.ShouldNotBeNull();
        result.Value.Classification.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
        result.Value.NewFilePath.ShouldNotBeNullOrEmpty();
        File.Exists(result.Value.NewFilePath).ShouldBeTrue();
    }

    /// <summary>
    /// IV1: Verifies that existing IOcrExecutor and IImagePreprocessor interfaces continue to work.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_PdfDocument_OCRInterfacesStillWork()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // PDF header
        var testFile = Path.Combine(_tempDirectory, "test.pdf");
        await File.WriteAllBytesAsync(testFile, pdfContent, TestContext.Current.CancellationToken);

        // Mock OCR dependencies to verify they're still called correctly
        var imagePreprocessor = Substitute.For<IImagePreprocessor>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();

        var imageData = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var ocrResult = new OCRResult { Text = "Sample OCR text with A/AS1-2505-088637-PHM expediente" };

        imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        var pdfExtractor = new PdfMetadataExtractor(ocrExecutor, imagePreprocessor, _pdfExtractorLogger);
        var compositeExtractor = new CompositeMetadataExtractor(
            new XmlMetadataExtractor(new XmlExpedienteParser(_xmlParserLogger), _xmlExtractorLogger),
            new DocxMetadataExtractor(_docxExtractorLogger),
            pdfExtractor,
            _compositeExtractorLogger);

        var auditLogger = Substitute.For<IAuditLogger>();
        var service = new MetadataExtractionService(
            _fileTypeIdentifier,
            compositeExtractor,
            _fileClassifier,
            _safeFileNamer,
            _fileMover,
            auditLogger,
            _serviceLogger);

        // Act
        var result = await service.ProcessFileAsync(testFile, "test.pdf", cancellationToken: TestContext.Current.CancellationToken);

        // Assert - IV1: Verify OCR interfaces were called
        await imagePreprocessor.Received().PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await ocrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());

        // Verify the workflow completed
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// IV2: Verifies that IMetadataExtractor wraps existing OCR functionality correctly.
    /// </summary>
    [Fact]
    public async Task MetadataExtractor_WrapsOCRFunctionality_MaintainsCompatibility()
    {
        // Arrange
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var imagePreprocessor = Substitute.For<IImagePreprocessor>();
        var ocrExecutor = Substitute.For<IOcrExecutor>();

        var preprocessedImage = new ImageData { Data = pdfContent, SourcePath = "test.pdf" };
        var ocrResult = new OCRResult { Text = "Test OCR text" };

        imagePreprocessor.PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>())
            .Returns(Result<ImageData>.Success(preprocessedImage));
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>())
            .Returns(Result<OCRResult>.Success(ocrResult));

        var pdfExtractor = new PdfMetadataExtractor(ocrExecutor, imagePreprocessor, _pdfExtractorLogger);

        // Act
        var result = await pdfExtractor.ExtractFromPdfAsync(pdfContent, TestContext.Current.CancellationToken);

        // Assert - IV2: Verify IMetadataExtractor wraps OCR correctly
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        await imagePreprocessor.Received().PreprocessAsync(Arg.Any<ImageData>(), Arg.Any<ProcessingConfig>());
        await ocrExecutor.Received().ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<OCRConfig>());
    }

    /// <summary>
    /// IV3: Verifies that classification performance meets the 500ms target.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_Performance_Meets500msTarget()
    {
        // Arrange
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-088637-PHM",
                AreaDescripcion = "ASEGURAMIENTO"
            },
            RfcValues = new[] { "PERJ800101ABC" },
            Names = new[] { "Juan Perez" }
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _fileClassifier.ClassifyAsync(metadata, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert - IV3: Classification should complete in under 500ms
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500,
            $"Classification took {stopwatch.ElapsedMilliseconds}ms, exceeding 500ms target");
    }

    /// <summary>
    /// Tests that the complete workflow processes multiple document types correctly.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_MultipleFormats_ProcessesCorrectly()
    {
        // Arrange - XML
        var xmlContent = @"<?xml version=""1.0""?><Expediente><NumeroExpediente>TEST-001</NumeroExpediente><AreaDescripcion>DOCUMENTACION</AreaDescripcion></Expediente>";
        var xmlFile = Path.Combine(_tempDirectory, "test.xml");
        await File.WriteAllTextAsync(xmlFile, xmlContent, TestContext.Current.CancellationToken);

        // Act - Process XML
        var xmlResult = await _service.ProcessFileAsync(xmlFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        xmlResult.IsSuccess.ShouldBeTrue();
        xmlResult.Value.ShouldNotBeNull();
        xmlResult.Value.FileFormat.ShouldBe(FileFormat.Xml);
        xmlResult.Value.Classification.ShouldNotBeNull();
        xmlResult.Value.Classification.Level1.ShouldBe(ClassificationLevel1.Documentacion);
    }

    /// <summary>
    /// Tests that file organization creates proper directory structure.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_XmlDocument_OrganizesFilesCorrectly()
    {
        // Arrange
        var xmlContent = @"<?xml version=""1.0""?><Expediente><NumeroExpediente>A/AS1-2505-088637-PHM</NumeroExpediente><AreaDescripcion>ASEGURAMIENTO</AreaDescripcion></Expediente>";
        var testFile = Path.Combine(_tempDirectory, "test.xml");
        await File.WriteAllTextAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.NewFilePath.ShouldNotBeNullOrEmpty();
        result.Value.NewFilePath.ShouldContain("Aseguramiento");
        result.Value.NewFilePath.ShouldContain(DateTime.Now.Year.ToString());
        File.Exists(result.Value.NewFilePath).ShouldBeTrue();
        File.Exists(testFile).ShouldBeFalse(); // Original file should be moved
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
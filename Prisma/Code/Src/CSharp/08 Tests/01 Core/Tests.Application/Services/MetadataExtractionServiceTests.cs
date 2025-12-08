namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="MetadataExtractionService"/> covering successful extraction/classification flows and failure scenarios.
/// </summary>
public class MetadataExtractionServiceTests : IDisposable
{
    private readonly IFileTypeIdentifier _fileTypeIdentifier;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IFileClassifier _fileClassifier;
    private readonly ISafeFileNamer _safeFileNamer;
    private readonly IFileMover _fileMover;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<MetadataExtractionService> _logger;
    private readonly MetadataExtractionService _service;
    private readonly string _testStoragePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataExtractionServiceTests"/> class with mocked collaborators and temp storage.
    /// </summary>
    public MetadataExtractionServiceTests()
    {
        _fileTypeIdentifier = Substitute.For<IFileTypeIdentifier>();
        _metadataExtractor = Substitute.For<IMetadataExtractor>();
        _fileClassifier = Substitute.For<IFileClassifier>();
        _safeFileNamer = Substitute.For<ISafeFileNamer>();
        _fileMover = Substitute.For<IFileMover>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = Substitute.For<ILogger<MetadataExtractionService>>();
        _testStoragePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _service = new MetadataExtractionService(
            _fileTypeIdentifier,
            _metadataExtractor,
            _fileClassifier,
            _safeFileNamer,
            _fileMover,
            _auditLogger,
            _logger);
    }

    /// <summary>
    /// Tests that complete workflow succeeds for XML file.
    /// </summary>
    /// <returns>A task that completes after verifying successful XML extraction workflow.</returns>
    [Fact]
    public async Task ProcessFileAsync_XmlFile_CompletesWorkflow()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        await File.WriteAllBytesAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-088637-PHM",
            AreaDescripcion = "ASEGURAMIENTO"
        };
        var metadata = new ExtractedMetadata
        {
            Expediente = expediente
        };
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Especial,
            Confidence = 90
        };
        var safeFileName = "ASEGURAMIENTO_ESPECIAL_test.xml";
        var newFilePath = Path.Combine(_testStoragePath, safeFileName);

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Xml));
        _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(metadata));
        _fileClassifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classification));
        _safeFileNamer.GenerateSafeFileNameAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(safeFileName));
        _fileMover.MoveFileAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(newFilePath));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.OriginalFilePath.ShouldBe(testFile);
            result.Value.NewFilePath.ShouldBe(newFilePath);
            result.Value.Classification.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
            result.Value.FileFormat.ShouldBe(FileFormat.Xml);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow fails when file type identification fails.
    /// </summary>
    /// <returns>A task that completes after asserting file-type identification failure is surfaced.</returns>
    [Fact]
    public async Task ProcessFileAsync_FileTypeIdentificationFails_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.unknown");
        var content = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(testFile, content, TestContext.Current.CancellationToken);

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.WithFailure("Unknown file type"));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.unknown", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("Unknown file type");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow fails when metadata extraction fails.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_MetadataExtractionFails_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        await File.WriteAllBytesAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Xml));
        _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.WithFailure("Extraction failed"));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("Extraction failed");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow fails when classification fails.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_ClassificationFails_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        await File.WriteAllBytesAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = "TEST-001" }
        };

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Xml));
        _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(metadata));
        _fileClassifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.WithFailure("Classification failed"));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("Classification failed");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow routes DOCX files correctly.
    /// </summary>
    [Fact]
    public async Task ProcessFileAsync_DocxFile_RoutesToDocxExtractor()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.docx");
        var docxContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        await File.WriteAllBytesAsync(testFile, docxContent, TestContext.Current.CancellationToken);

        var metadata = new ExtractedMetadata { Expediente = new Expediente { NumeroExpediente = "DOCX-001" } };
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Documentacion };
        var safeFileName = "test.docx";
        var newFilePath = Path.Combine(_testStoragePath, safeFileName);

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Docx));
        _metadataExtractor.ExtractFromDocxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(metadata));
        _fileClassifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classification));
        _safeFileNamer.GenerateSafeFileNameAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(safeFileName));
        _fileMover.MoveFileAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(newFilePath));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.docx", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.FileFormat.ShouldBe(FileFormat.Docx);
            await _metadataExtractor.Received().ExtractFromDocxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            await _metadataExtractor.DidNotReceive().ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            await _metadataExtractor.DidNotReceive().ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow routes PDF files correctly.
    /// </summary>
    /// <returns>A task that completes after verifying PDF extraction is invoked.</returns>
    [Fact]
    public async Task ProcessFileAsync_PdfFile_RoutesToPdfExtractor()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        await File.WriteAllBytesAsync(testFile, pdfContent, TestContext.Current.CancellationToken);

        var metadata = new ExtractedMetadata { Expediente = new Expediente { NumeroExpediente = "PDF-001" } };
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Informacion };
        var safeFileName = "test.pdf";
        var newFilePath = Path.Combine(_testStoragePath, safeFileName);

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Pdf));
        _metadataExtractor.ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(metadata));
        _fileClassifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classification));
        _safeFileNamer.GenerateSafeFileNameAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(safeFileName));
        _fileMover.MoveFileAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(newFilePath));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.pdf", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.FileFormat.ShouldBe(FileFormat.Pdf);
            await _metadataExtractor.Received().ExtractFromPdfAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            await _metadataExtractor.DidNotReceive().ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
            await _metadataExtractor.DidNotReceive().ExtractFromDocxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow fails when safe file naming fails.
    /// </summary>
    /// <returns>A task that completes after asserting safe-file-name failures are surfaced.</returns>
    [Fact]
    public async Task ProcessFileAsync_SafeFileNamingFails_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        await File.WriteAllBytesAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        var metadata = new ExtractedMetadata { Expediente = new Expediente { NumeroExpediente = "TEST-001" } };
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Documentacion };

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Xml));
        _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(metadata));
        _fileClassifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classification));
        _safeFileNamer.GenerateSafeFileNameAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.WithFailure("File naming failed"));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("File naming failed");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow fails when file move fails.
    /// </summary>
    /// <returns>A task that completes after asserting file move failures are surfaced.</returns>
    [Fact]
    public async Task ProcessFileAsync_FileMoveFails_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        await File.WriteAllBytesAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        var metadata = new ExtractedMetadata { Expediente = new Expediente { NumeroExpediente = "TEST-001" } };
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Documentacion };
        var safeFileName = "test.xml";

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success(FileFormat.Xml));
        _metadataExtractor.ExtractFromXmlAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtractedMetadata>.Success(metadata));
        _fileClassifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(classification));
        _safeFileNamer.GenerateSafeFileNameAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(safeFileName));
        _fileMover.MoveFileAsync(Arg.Any<string>(), Arg.Any<ClassificationResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.WithFailure("File move failed"));

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("File move failed");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow handles unsupported file formats.
    /// </summary>
    /// <returns>A task that completes after asserting unsupported formats are rejected.</returns>
    [Fact]
    public async Task ProcessFileAsync_UnsupportedFileFormat_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        var content = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(testFile, content, TestContext.Current.CancellationToken);

        _fileTypeIdentifier.IdentifyFileTypeAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileFormat>.Success((FileFormat)999)); // Unsupported format

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.txt", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldContain("Unsupported file format");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow handles cancellation token.
    /// </summary>
    /// <returns>A task that completes after cancellation handling assertions are evaluated.</returns>
    [Fact]
    public async Task ProcessFileAsync_CancellationRequested_ReturnsFailure()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
        var xmlContent = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root></root>");
        await File.WriteAllBytesAsync(testFile, xmlContent, TestContext.Current.CancellationToken);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            var result = await _service.ProcessFileAsync(testFile, "test.xml", cancellationToken: cts.Token);

            // Assert
            result.IsFailure.ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests that workflow handles null or empty file path.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessFileAsync_InvalidFilePath_ReturnsFailure(string? filePath)
    {
        // Act
        var result = await _service.ProcessFileAsync(filePath!, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that workflow handles file not found.
    /// </summary>
    /// <returns>A task that completes after asserting missing files are handled.</returns>
    [Fact]
    public async Task ProcessFileAsync_FileNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.xml");

        // Act
        var result = await _service.ProcessFileAsync(nonExistentFile, "test.xml", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Disposes temporary storage used during extraction tests.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, true);
        }
    }
}

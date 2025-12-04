namespace ExxerCube.Prisma.Tests.System.Ocr.Pipeline;

/// <summary>
/// System-level integration tests for <see cref="DocumentIngestionService"/> that test complete workflows.
/// These tests use real infrastructure components (in-memory database, file system) with mocked browser automation.
///
/// These tests belong in Tests.System because they:
/// - Use real Infrastructure components (Database + FileStorage)
/// - Test system-level integration across multiple Infrastructure layers
/// - Verify database persistence and file system operations together
/// - Test complete workflows with real Infrastructure components
/// </summary>
public class DocumentIngestionIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<DocumentIngestionService> _serviceLogger;
    private readonly ILogger<DownloadTrackerService> _trackerLogger;
    private readonly ILogger<FileMetadataLoggerService> _loggerLogger;
    private readonly ILogger<FileSystemDownloadStorageAdapter> _storageLogger;
    private readonly IBrowserAutomationAgent _browserAutomationAgent;
    private readonly DocumentIngestionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentIngestionIntegrationTests"/> class.
    /// </summary>
    public DocumentIngestionIntegrationTests(ITestOutputHelper output)
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _serviceLogger = XUnitLogger.CreateLogger<DocumentIngestionService>(output);
        _trackerLogger = XUnitLogger.CreateLogger<DownloadTrackerService>(output);
        _loggerLogger = XUnitLogger.CreateLogger<FileMetadataLoggerService>(output);
        _storageLogger = XUnitLogger.CreateLogger<FileSystemDownloadStorageAdapter>(output);

        var downloadTracker = new DownloadTrackerService(_dbContext, _trackerLogger);
        var fileMetadataLogger = new FileMetadataLoggerService(_dbContext, _loggerLogger);
        var storageOptions = Options.Create(new FileStorageOptions
        {
            StorageBasePath = _tempDirectory
        });
        var downloadStorage = new FileSystemDownloadStorageAdapter(_storageLogger, storageOptions);

        // Mock browser automation agent for system tests
        _browserAutomationAgent = Substitute.For<IBrowserAutomationAgent>();
        var auditLogger = Substitute.For<IAuditLogger>();
        var eventPublisher = Substitute.For<IEventPublisher>();

        _service = new DocumentIngestionService(
            _browserAutomationAgent,
            downloadTracker,
            downloadStorage,
            fileMetadataLogger,
            auditLogger,
            eventPublisher,
            _serviceLogger);
    }

    /// <summary>
    /// Tests that the end-to-end workflow successfully ingests a new document.
    /// </summary>
    [Fact]
    public async Task IngestDocumentsAsync_NewDocument_CompletesSuccessfully()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "test-document.pdf",
            Url = "https://example.com/test-document.pdf",
            Format = FileFormat.Pdf
        };
        var fileContent = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var downloadedFile = new DownloadedFile
        {
            Url = downloadableFile.Url,
            FileName = downloadableFile.FileName,
            Format = downloadableFile.Format,
            Content = fileContent
        };

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(downloadedFile));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(1);

            // Verify file was saved to storage
            var fileMetadata = result.Value[0];
            fileMetadata.ShouldNotBeNull();
            File.Exists(fileMetadata.FilePath).ShouldBeTrue();

            // Verify metadata was logged to database
            var savedMetadata = await _dbContext.FileMetadata.FindAsync(new object[] { fileMetadata.FileId }, TestContext.Current.CancellationToken);
            savedMetadata.ShouldNotBeNull();
            savedMetadata!.FileName.ShouldBe("test-document.pdf");
            savedMetadata.Checksum.ShouldNotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// Tests that duplicate files are skipped during ingestion.
    /// </summary>
    [Fact]
    public async Task IngestDocumentsAsync_DuplicateFile_SkipsFile()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "duplicate.pdf",
            Url = "https://example.com/duplicate.pdf",
            Format = FileFormat.Pdf
        };
        var fileContent = new byte[] { 1, 2, 3 };
        var downloadedFile = new DownloadedFile
        {
            Url = downloadableFile.Url,
            FileName = downloadableFile.FileName,
            Format = downloadableFile.Format,
            Content = fileContent
        };

        // Pre-populate database with duplicate
        var checksum = ComputeChecksum(fileContent);
        var existingMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = "duplicate.pdf",
            FilePath = "/existing/path/duplicate.pdf",
            Checksum = checksum,
            FileSize = 3,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.FileMetadata.Add(existingMetadata);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(downloadedFile));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(0); // Duplicate skipped
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static string ComputeChecksum(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(content);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
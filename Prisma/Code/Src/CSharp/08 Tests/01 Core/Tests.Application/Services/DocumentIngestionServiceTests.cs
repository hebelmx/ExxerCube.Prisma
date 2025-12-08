namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="DocumentIngestionService"/> covering success paths, validation failures, duplicates, and cancellation handling.
/// </summary>
public class DocumentIngestionServiceTests
{
    private readonly IBrowserAutomationAgent _browserAutomationAgent;
    private readonly IDownloadTracker _downloadTracker;
    private readonly IDownloadStorage _downloadStorage;
    private readonly IFileMetadataLogger _fileMetadataLogger;
    private readonly IAuditLogger _auditLogger;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<DocumentIngestionService> _logger;
    private readonly DocumentIngestionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentIngestionServiceTests"/> class with mocked ingestion dependencies.
    /// </summary>
    public DocumentIngestionServiceTests()
    {
        _browserAutomationAgent = Substitute.For<IBrowserAutomationAgent>();
        _downloadTracker = Substitute.For<IDownloadTracker>();
        _downloadStorage = Substitute.For<IDownloadStorage>();
        _fileMetadataLogger = Substitute.For<IFileMetadataLogger>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _logger = Substitute.For<ILogger<DocumentIngestionService>>();
        _service = new DocumentIngestionService(
            _browserAutomationAgent,
            _downloadTracker,
            _downloadStorage,
            _fileMetadataLogger,
            _auditLogger,
            _eventPublisher,
            _logger);
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> successfully ingests documents when all steps succeed.
    /// </summary>
    /// <returns>A task that completes after verifying successful ingestion.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_AllStepsSucceed_ReturnsIngestedFiles()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "test.pdf",
            Url = "https://example.com/test.pdf",
            Format = FileFormat.Pdf
        };
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var storagePath = "/storage/test.pdf";
        var checksum = ComputeChecksum(fileContent);
        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = "test.pdf",
            FilePath = storagePath,
            Url = downloadableFile.Url,
            Checksum = checksum,
            FileSize = fileContent.LongLength,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        var downloadedFile = new DownloadedFile
        {
            Url = downloadableFile.Url,
            FileName = downloadableFile.FileName,
            Format = downloadableFile.Format,
            Content = fileContent
        };
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(downloadedFile));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _downloadTracker.IsDuplicateAsync(checksum, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _downloadStorage.SaveFileAsync(fileContent, downloadableFile.FileName, Arg.Any<FileFormat>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<string>.Success(storagePath));
        _fileMetadataLogger.LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(1);
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> skips duplicate files.
    /// </summary>
    /// <returns>A task that completes after ensuring duplicates are ignored.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_DuplicateFile_SkipsFile()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "test.pdf",
            Url = "https://example.com/test.pdf"
        };
        var fileContent = new byte[] { 1, 2, 3 };
        var checksum = ComputeChecksum(fileContent);

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        var downloadedFile = new DownloadedFile
        {
            Url = downloadableFile.Url,
            FileName = downloadableFile.FileName,
            Format = downloadableFile.Format,
            Content = fileContent
        };
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(downloadedFile));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _downloadTracker.IsDuplicateAsync(checksum, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<bool>.Success(true)); // Duplicate

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(0); // No files ingested due to duplicate
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> handles browser launch failure.
    /// </summary>
    /// <returns>A task that completes after asserting failure on launch issues.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_BrowserLaunchFails_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.WithFailure("Failed to launch browser"));

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> handles navigation failure.
    /// </summary>
    /// <returns>A task that completes after asserting navigation failures are surfaced.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_NavigationFails_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.WithFailure("Failed to navigate"));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> handles file download failure gracefully.
    /// </summary>
    /// <returns>A task that completes after verifying download failures skip affected files.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_FileDownloadFails_SkipsFileAndContinues()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "test.pdf",
            Url = "https://example.com/test.pdf"
        };

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.WithFailure("Download failed"));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(0); // No files ingested due to download failure
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> handles storage save failure gracefully.
    /// </summary>
    /// <returns>A task that completes after ensuring storage failures are reported per file.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_StorageSaveFails_ReturnsFailureForFile()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "test.pdf",
            Url = "https://example.com/test.pdf"
        };
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var downloadedFile = new DownloadedFile
        {
            Url = downloadableFile.Url,
            FileName = downloadableFile.FileName,
            Format = downloadableFile.Format,
            Content = fileContent
        };
        var checksum = ComputeChecksum(fileContent);

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(downloadedFile));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _downloadTracker.IsDuplicateAsync(checksum, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _downloadStorage.SaveFileAsync(fileContent, downloadableFile.FileName, FileFormat.Pdf, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<string>.WithFailure("Storage save failed"));

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(0); // No files ingested due to storage failure
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> properly handles cancellation token at start.
    /// </summary>
    /// <returns>A task that completes after cancellation behavior is asserted.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_CancellationRequestedAtStart_ReturnsCancelled()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> processes multiple files correctly.
    /// </summary>
    /// <returns>A task that completes after verifying multiple files are ingested.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_MultipleFiles_ProcessesAllFiles()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile1 = new DownloadableFile { FileName = "file1.pdf", Url = "https://example.com/file1.pdf" };
        var downloadableFile2 = new DownloadableFile { FileName = "file2.pdf", Url = "https://example.com/file2.pdf" };
        var fileContent1 = new byte[] { 1, 2, 3 };
        var fileContent2 = new byte[] { 4, 5, 6 };
        var storagePath1 = "/storage/file1.pdf";
        var storagePath2 = "/storage/file2.pdf";
        var checksum1 = ComputeChecksum(fileContent1);
        var checksum2 = ComputeChecksum(fileContent2);

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile1, downloadableFile2 }));

        _browserAutomationAgent.DownloadFileAsync(downloadableFile1.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(new DownloadedFile
            {
                Url = downloadableFile1.Url,
                FileName = downloadableFile1.FileName,
                Format = FileFormat.Pdf,
                Content = fileContent1
            }));
        _browserAutomationAgent.DownloadFileAsync(downloadableFile2.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(new DownloadedFile
            {
                Url = downloadableFile2.Url,
                FileName = downloadableFile2.FileName,
                Format = FileFormat.Pdf,
                Content = fileContent2
            }));

        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());

        _downloadTracker.IsDuplicateAsync(checksum1, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _downloadTracker.IsDuplicateAsync(checksum2, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<bool>.Success(false));

        _downloadStorage.SaveFileAsync(fileContent1, downloadableFile1.FileName, FileFormat.Pdf, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<string>.Success(storagePath1));
        _downloadStorage.SaveFileAsync(fileContent2, downloadableFile2.FileName, FileFormat.Pdf, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<string>.Success(storagePath2));

        _fileMetadataLogger.LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(2); // Both files ingested
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> handles file identification failure.
    /// </summary>
    /// <returns>A task that completes after asserting identification failures are surfaced.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_FileIdentificationFails_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.WithFailure("Failed to identify files"));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> handles duplicate check failure gracefully.
    /// </summary>
    /// <returns>A task that completes after asserting duplicate check failures are handled.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_DuplicateCheckFails_ReturnsFailureForFile()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf" };
        var downloadableFile = new DownloadableFile
        {
            FileName = "test.pdf",
            Url = "https://example.com/test.pdf"
        };
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var downloadedFile = new DownloadedFile
        {
            Url = downloadableFile.Url,
            FileName = downloadableFile.FileName,
            Format = downloadableFile.Format,
            Content = fileContent
        };
        var checksum = ComputeChecksum(fileContent);

        _browserAutomationAgent.LaunchBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.NavigateToAsync(websiteUrl, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _browserAutomationAgent.IdentifyDownloadableFilesAsync(filePatterns, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<List<DownloadableFile>>.Success(new List<DownloadableFile> { downloadableFile }));
        _browserAutomationAgent.DownloadFileAsync(downloadableFile.Url, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<DownloadedFile>.Success(downloadedFile));
        _browserAutomationAgent.CloseBrowserAsync(Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result.Success());
        _downloadTracker.IsDuplicateAsync(checksum, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Result<bool>.WithFailure("Database error"));

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Count.ShouldBe(0); // No files ingested due to duplicate check failure
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> validates null website URL.
    /// </summary>
    /// <returns>A task that completes after validating null URL input handling.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_NullWebsiteUrl_ReturnsFailure()
    {
        // Arrange
        string? websiteUrl = null;
        var filePatterns = new[] { "*.pdf" };

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl!, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        if (result.IsFailure)
        {
            result.Error.ShouldContain("Website URL cannot be null or empty");
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> validates empty website URL.
    /// </summary>
    /// <returns>A task that completes after validating empty URL handling.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_EmptyWebsiteUrl_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = string.Empty;
        var filePatterns = new[] { "*.pdf" };

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        if (result.IsFailure)
        {
            result.Error.ShouldContain("Website URL cannot be null or empty");
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> validates invalid URL format.
    /// </summary>
    /// <returns>A task that completes after validating invalid URL handling.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_InvalidUrlFormat_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "not-a-valid-url";
        var filePatterns = new[] { "*.pdf" };

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        if (result.IsFailure)
        {
            result.Error.ShouldContain("Invalid URL format");
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> validates null file patterns.
    /// </summary>
    /// <returns>A task that completes after validating null file pattern handling.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_NullFilePatterns_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        string[]? filePatterns = null;

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        if (result.IsFailure)
        {
            result.Error.ShouldContain("File patterns cannot be null or empty");
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> validates empty file patterns array.
    /// </summary>
    /// <returns>A task that completes after validating empty pattern handling.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_EmptyFilePatterns_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = Array.Empty<string>();

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        if (result.IsFailure)
        {
            result.Error.ShouldContain("File patterns cannot be null or empty");
        }
    }

    /// <summary>
    /// Tests that <see cref="DocumentIngestionService.IngestDocumentsAsync"/> validates file patterns containing null values.
    /// </summary>
    /// <returns>A task that completes after validating null entries in pattern list.</returns>
    [Fact]
    public async Task IngestDocumentsAsync_FilePatternsWithNullValues_ReturnsFailure()
    {
        // Arrange
        var websiteUrl = "https://example.com";
        var filePatterns = new[] { "*.pdf", null!, "*.xml" };

        // Act
        var result = await _service.IngestDocumentsAsync(websiteUrl, filePatterns, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        if (result.IsFailure)
        {
            result.Error.ShouldContain("File patterns cannot contain null or empty values");
        }
    }

    private static string ComputeChecksum(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(content);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}

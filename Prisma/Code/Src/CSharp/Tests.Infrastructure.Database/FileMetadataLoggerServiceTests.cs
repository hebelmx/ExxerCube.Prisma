namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Unit tests for <see cref="FileMetadataLoggerService"/>.
/// </summary>
public class FileMetadataLoggerServiceTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<FileMetadataLoggerService> _logger;
    private readonly FileMetadataLoggerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMetadataLoggerServiceTests"/> class.
    /// </summary>
    public FileMetadataLoggerServiceTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<FileMetadataLoggerService>(output);
        _service = new FileMetadataLoggerService(_dbContext, _logger);
    }

    /// <summary>
    /// Tests that <see cref="FileMetadataLoggerService.LogFileMetadataAsync"/> successfully logs file metadata.
    /// </summary>
    [Fact]
    public async Task LogFileMetadataAsync_ValidMetadata_ReturnsSuccess()
    {
        // Arrange
        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = "test-document.pdf",
            FilePath = "/storage/test-document.pdf",
            Url = "https://example.com/test-document.pdf",
            Checksum = "test-checksum-123",
            FileSize = 1024,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };

        // Act
        var result = await _service.LogFileMetadataAsync(fileMetadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify the metadata was saved
        var savedMetadata = await _dbContext.FileMetadata.FindAsync(new object[] { fileMetadata.FileId }, TestContext.Current.CancellationToken);
        savedMetadata.ShouldNotBeNull();
        savedMetadata!.FileName.ShouldBe("test-document.pdf");
        savedMetadata.Checksum.ShouldBe("test-checksum-123");
    }

    /// <summary>
    /// Tests that <see cref="FileMetadataLoggerService.LogFileMetadataAsync"/> handles duplicate file IDs gracefully.
    /// </summary>
    [Fact]
    public async Task LogFileMetadataAsync_DuplicateFileId_ReturnsFailure()
    {
        // Arrange
        var fileId = Guid.NewGuid().ToString();
        var fileMetadata1 = new FileMetadata
        {
            FileId = fileId,
            FileName = "test1.pdf",
            FilePath = "/storage/test1.pdf",
            Checksum = "checksum1",
            FileSize = 1024,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };
        _dbContext.FileMetadata.Add(fileMetadata1);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fileMetadata2 = new FileMetadata
        {
            FileId = fileId, // Same FileId
            FileName = "test2.pdf",
            FilePath = "/storage/test2.pdf",
            Checksum = "checksum2",
            FileSize = 2048,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };

        // Act
        var result = await _service.LogFileMetadataAsync(fileMetadata2, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}


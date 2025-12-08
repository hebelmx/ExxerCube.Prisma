namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Unit tests for <see cref="DownloadTrackerService"/>.
/// </summary>
public class DownloadTrackerServiceTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<DownloadTrackerService> _logger;
    private readonly DownloadTrackerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadTrackerServiceTests"/> class.
    /// </summary>
    public DownloadTrackerServiceTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<DownloadTrackerService>(output);
        _service = new DownloadTrackerService(_dbContext, _logger);
    }

    /// <summary>
    /// Tests that <see cref="DownloadTrackerService.IsDuplicateAsync"/> returns false for a new checksum.
    /// </summary>
    [Fact]
    public async Task IsDuplicateAsync_NewChecksum_ReturnsFalse()
    {
        // Arrange
        var checksum = "test-checksum-123";

        // Act
        var result = await _service.IsDuplicateAsync(checksum, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldBeFalse();
        }
    }

    /// <summary>
    /// Tests that <see cref="DownloadTrackerService.IsDuplicateAsync"/> returns true for an existing checksum.
    /// </summary>
    [Fact]
    public async Task IsDuplicateAsync_ExistingChecksum_ReturnsTrue()
    {
        // Arrange
        var checksum = "existing-checksum-456";
        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = "test.pdf",
            FilePath = "/path/to/test.pdf",
            Checksum = checksum,
            FileSize = 1024,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };
        _dbContext.FileMetadata.Add(fileMetadata);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.IsDuplicateAsync(checksum, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldBeTrue();
        }
    }

    /// <summary>
    /// Tests that <see cref="DownloadTrackerService.GetFileMetadataByChecksumAsync"/> returns null for a non-existent checksum.
    /// </summary>
    [Fact]
    public async Task GetFileMetadataByChecksumAsync_NonExistentChecksum_ReturnsNull()
    {
        // Arrange
        var checksum = "non-existent-checksum";

        // Act
        var result = await _service.GetFileMetadataByChecksumAsync(checksum, CancellationToken.None);

        // Assert
        // For nullable types, use IsSuccessMayBeNull to check success even with null value
        result.IsSuccessMayBeNull.ShouldBeTrue($"Expected success but got failure. Error: {result.Error ?? "No error message"}");
        if (result.IsSuccessMayBeNull)
        {
            result.Value.ShouldBeNull();
        }
    }

    /// <summary>
    /// Tests that <see cref="DownloadTrackerService.GetFileMetadataByChecksumAsync"/> returns file metadata for an existing checksum.
    /// </summary>
    [Fact]
    public async Task GetFileMetadataByChecksumAsync_ExistingChecksum_ReturnsFileMetadata()
    {
        // Arrange
        var checksum = "existing-checksum-789";
        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = "test.pdf",
            FilePath = "/path/to/test.pdf",
            Checksum = checksum,
            FileSize = 2048,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };
        _dbContext.FileMetadata.Add(fileMetadata);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetFileMetadataByChecksumAsync(checksum, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        if (result.IsSuccess)
        {
            result.Value.ShouldNotBeNull();
            result.Value!.FileName.ShouldBe("test.pdf");
            result.Value.Checksum.ShouldBe(checksum);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}


namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Tests for <see cref="ManualReviewerService"/> C3 behaviour: hydrating real field annotations
/// from the <see cref="IUnifiedMetadataStore"/> in <see cref="ManualReviewerService.GetFieldAnnotationsAsync"/>.
/// </summary>
public class ManualReviewerServiceC3Tests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _serviceLogger;
    private readonly ILogger<EfCoreUnifiedMetadataStore> _storeLogger;

    /// <summary>Initializes with a unique in-memory database.</summary>
    public ManualReviewerServiceC3Tests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _serviceLogger = XUnitLogger.CreateLogger<ManualReviewerService>(output);
        _storeLogger = XUnitLogger.CreateLogger<EfCoreUnifiedMetadataStore>(output);
    }

    /// <inheritdoc />
    public void Dispose() => _dbContext.Dispose();

    /// <summary>
    /// When a UnifiedMetadataRecord exists for the file, GetFieldAnnotationsAsync returns annotations
    /// that include the real field values from AdditionalFields in addition to the ConfidenceLevel stub.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithStoredRecord_HydratesRealFields()
    {
        // Arrange — seed a review case and its file metadata
        var fileId = "FILE-C3-001";
        var caseId = "CASE-C3-001";

        await _dbContext.FileMetadata.AddAsync(new FileMetadata
        {
            FileId = fileId,
            FileName = "doc.pdf",
            FilePath = "/data/doc.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "abc123",
            FileSize = 2048,
            Format = FileFormat.Pdf,
        }, TestContext.Current.CancellationToken);

        await _dbContext.ReviewCases.AddAsync(new ReviewCase
        {
            CaseId = caseId,
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 70,
            ClassificationAmbiguity = false,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Seed a unified metadata record via the store
        var store = new EfCoreUnifiedMetadataStore(_dbContext, _storeLogger);
        await store.SaveAsync(fileId, new UnifiedMetadataRecord
        {
            AdditionalFields = new Dictionary<string, string?>
            {
                ["AreaDescripcion"] = "Dirección Adjunta de Supervisión",
                ["NumeroExpediente"] = "EXP-2026-042",
            },
        }, TestContext.Current.CancellationToken);

        var service = new ManualReviewerService(_dbContext, _serviceLogger, store);

        // Act
        var result = await service.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        var annotations = result.Value!.FieldAnnotationsDict;

        // Real fields from AdditionalFields should be present
        annotations.ShouldContainKey("AreaDescripcion");
        annotations["AreaDescripcion"].Value.ShouldBe("Dirección Adjunta de Supervisión");
        annotations["AreaDescripcion"].Source.ShouldBe("Fusion");

        annotations.ShouldContainKey("NumeroExpediente");
        annotations["NumeroExpediente"].Value.ShouldBe("EXP-2026-042");

        // The ConfidenceLevel baseline annotation must always be present
        annotations.ShouldContainKey("ConfidenceLevel");
        annotations["ConfidenceLevel"].Confidence.ShouldBe(70);
    }

    /// <summary>
    /// When no UnifiedMetadataRecord has been stored for the file, GetFieldAnnotationsAsync degrades
    /// to the existing stub behaviour (only ConfidenceLevel) rather than failing.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithoutStoredRecord_FallsBackToStub()
    {
        // Arrange — seed review case and file metadata, but NO unified metadata record
        var fileId = "FILE-C3-NOSTUB-001";
        var caseId = "CASE-C3-NOSTUB-001";

        await _dbContext.FileMetadata.AddAsync(new FileMetadata
        {
            FileId = fileId,
            FileName = "doc2.pdf",
            FilePath = "/data/doc2.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "def456",
            FileSize = 1024,
            Format = FileFormat.Pdf,
        }, TestContext.Current.CancellationToken);

        await _dbContext.ReviewCases.AddAsync(new ReviewCase
        {
            CaseId = caseId,
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 55,
            ClassificationAmbiguity = false,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var store = new EfCoreUnifiedMetadataStore(_dbContext, _storeLogger);
        var service = new ManualReviewerService(_dbContext, _serviceLogger, store);

        // Act
        var result = await service.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert — should succeed with at least the ConfidenceLevel stub annotation
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FieldAnnotationsDict.ShouldContainKey("ConfidenceLevel");
        result.Value.FieldAnnotationsDict["ConfidenceLevel"].Confidence.ShouldBe(55);
    }

    /// <summary>
    /// When no store is provided at all (null), GetFieldAnnotationsAsync still returns the
    /// ConfidenceLevel stub annotation without throwing.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithNoStore_FallsBackToStub()
    {
        // Arrange
        var fileId = "FILE-C3-NOSTORE-001";
        var caseId = "CASE-C3-NOSTORE-001";

        await _dbContext.FileMetadata.AddAsync(new FileMetadata
        {
            FileId = fileId,
            FileName = "doc3.pdf",
            FilePath = "/data/doc3.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "ghi789",
            FileSize = 512,
            Format = FileFormat.Pdf,
        }, TestContext.Current.CancellationToken);

        await _dbContext.ReviewCases.AddAsync(new ReviewCase
        {
            CaseId = caseId,
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 42,
            ClassificationAmbiguity = false,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        }, TestContext.Current.CancellationToken);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // No store — tests degraded path
        var service = new ManualReviewerService(_dbContext, _serviceLogger);

        // Act
        var result = await service.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.FieldAnnotationsDict.ShouldContainKey("ConfidenceLevel");
        result.Value.FieldAnnotationsDict["ConfidenceLevel"].Confidence.ShouldBe(42);
    }
}

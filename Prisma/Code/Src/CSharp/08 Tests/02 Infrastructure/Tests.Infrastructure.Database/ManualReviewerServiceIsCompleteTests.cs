namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Integration tests for the GH #6 IsComplete / IncompleteCase dimension of
/// <see cref="ManualReviewerService.IdentifyReviewCasesAsync"/>.
/// </summary>
/// <remarks>
/// These tests use an EF Core in-memory database (the same fixture pattern as
/// <see cref="ManualReviewerServiceTests"/> and <see cref="ManualReviewerServiceIntegrationTests"/>).
/// Testcontainers SQL are NOT required here because:
/// <list type="bullet">
///   <item>The behaviour under test is pure EF Core / LINQ — no SQL-dialect-specific clauses.</item>
///   <item>The existing real-SQL coverage is provided by <c>Tests.System.Storage</c> / EfCoreRepository
///         integration tests which already run against a real SQL Server container.</item>
/// </list>
/// Docker availability note: if Docker IS available, see also the Testcontainers suites in
/// <c>Tests.Infrastructure.Database</c> (EfCoreRepositoryIntegrationTests) for the real-SQL path.
/// </remarks>
public class ManualReviewerServiceIsCompleteTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _logger;
    private readonly ManualReviewerService _service;

    /// <summary>Initializes with a unique in-memory database per test.</summary>
    public ManualReviewerServiceIsCompleteTests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<ManualReviewerService>(output);
        _service = new ManualReviewerService(_dbContext, _logger);
    }

    /// <inheritdoc />
    public void Dispose() => _dbContext.Dispose();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Seeds a <see cref="FileMetadata"/> row required as FK by <see cref="ReviewCase"/>.
    /// </summary>
    private async Task SeedFileMetadataAsync(string fileId)
    {
        await _dbContext.FileMetadata.AddAsync(new FileMetadata
        {
            FileId = fileId,
            FileName = "test.pdf",
            FilePath = "/data/test.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "checksum-" + fileId,
            FileSize = 1024,
            Format = FileFormat.Pdf,
        }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Returns a high-confidence classification that would NOT trigger any other review reason.</summary>
    private static ClassificationResult HighConfidenceClassification() => new()
    {
        Level1 = ClassificationLevel1.Aseguramiento,
        Level2 = ClassificationLevel2.Judicial, // non-null → not ambiguous
        Confidence = Confidence.FromInt(90), // >= 80 → not low-confidence
    };

    /// <summary>Returns a clean metadata record with no conflicting or missing fields.</summary>
    private static UnifiedMetadataRecord CleanMetadata() => new()
    {
        Classification = HighConfidenceClassification(),
        MatchedFields = new MatchedFields
        {
            ConflictingFields = new List<string>(),
            MissingFields = new List<string>(),
        },
    };

    // -----------------------------------------------------------------------
    // TC-1: isComplete=false creates exactly one IncompleteCase row (Pending)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Given isComplete=false and no prior review cases for the file,
    /// when IdentifyReviewCasesAsync is called,
    /// then exactly one ReviewCase with RequiresReviewReason == IncompleteCase and Status == Pending
    /// is persisted in the database — even when confidence is high and no extraction issues exist.
    /// This proves the incomplete dimension is orthogonal to the confidence/ambiguity/extraction path.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WhenIncomplete_PersistsIncompleteCaseRow()
    {
        // Arrange
        var fileId = "FILE-IC-001";
        await SeedFileMetadataAsync(fileId);

        var metadata = CleanMetadata();
        var classification = HighConfidenceClassification();

        // Act — isComplete=false, high-confidence, no extraction issues
        var result = await _service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: false,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — method returns success
        result.IsSuccess.ShouldBeTrue();

        // Assert — DB has exactly one IncompleteCase row
        var allCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId)
            .ToListAsync(TestContext.Current.CancellationToken);

        var incompleteCases = allCases
            .Where(c => c.RequiresReviewReason == ReviewReason.IncompleteCase)
            .ToList();

        incompleteCases.Count.ShouldBe(1,
            "exactly one IncompleteCase row must be created when isComplete=false, " +
            "regardless of confidence/ambiguity");

        incompleteCases[0].Status.ShouldBe(ReviewStatus.Pending,
            "the IncompleteCase row must be Pending — it is awaiting resolution");

        incompleteCases[0].FileId.ShouldBe(fileId);

        // Sanity: no LowConfidence/ExtractionError/AmbiguousClassification rows created
        // (high-confidence + no conflicts → the other dimensions should be silent)
        allCases.ShouldAllBe(c => c.RequiresReviewReason == ReviewReason.IncompleteCase,
            "only the IncompleteCase reason should be present; no confidence/extraction rows expected");
    }

    // -----------------------------------------------------------------------
    // TC-2: Calling twice with isComplete=false produces exactly one row (idempotency)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Given a first call with isComplete=false for a file,
    /// when the same call is repeated (isComplete=false again, same fileId),
    /// then the second call does NOT create a second IncompleteCase row — still exactly one Pending row.
    /// This verifies the idempotency guard ("already flagged" path in the service).
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_RepeatedIncomplete_DoesNotDuplicate()
    {
        // Arrange
        var fileId = "FILE-IC-002";
        await SeedFileMetadataAsync(fileId);

        var metadata = CleanMetadata();
        var classification = HighConfidenceClassification();

        // Act — call #1
        var result1 = await _service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: false,
            cancellationToken: TestContext.Current.CancellationToken);
        result1.IsSuccess.ShouldBeTrue();

        // Act — call #2 (same file, same isComplete=false)
        var result2 = await _service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: false,
            cancellationToken: TestContext.Current.CancellationToken);
        result2.IsSuccess.ShouldBeTrue();

        // Assert — still exactly one Pending IncompleteCase row in the DB
        var incompleteCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId
                        && c.RequiresReviewReason == ReviewReason.IncompleteCase
                        && c.Status == ReviewStatus.Pending)
            .ToListAsync(TestContext.Current.CancellationToken);

        incompleteCases.Count.ShouldBe(1,
            "a second call with isComplete=false must not create a duplicate Pending IncompleteCase row");
    }

    // -----------------------------------------------------------------------
    // TC-3: isComplete=true after isComplete=false heals the row (Pending→Completed)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Given a prior call with isComplete=false that created a Pending IncompleteCase row,
    /// when isComplete=true is called for the same fileId (re-emit after the companion file arrives),
    /// then the existing Pending IncompleteCase row's Status is changed to Completed (healed),
    /// and no lingering Pending IncompleteCase row remains.
    /// This proves the partial-→complete idempotency (the "heal" branch).
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_CompleteAfterIncomplete_HealsRow()
    {
        // Arrange
        var fileId = "FILE-IC-003";
        await SeedFileMetadataAsync(fileId);

        var metadata = CleanMetadata();
        var classification = HighConfidenceClassification();

        // Act — first call: mark as incomplete
        var incompleteResult = await _service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: false,
            cancellationToken: TestContext.Current.CancellationToken);
        incompleteResult.IsSuccess.ShouldBeTrue();

        // Verify pre-condition: one Pending IncompleteCase row
        var beforeHeal = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId
                        && c.RequiresReviewReason == ReviewReason.IncompleteCase)
            .ToListAsync(TestContext.Current.CancellationToken);
        beforeHeal.Count.ShouldBe(1, "pre-condition: exactly one IncompleteCase row should exist");
        beforeHeal[0].Status.ShouldBe(ReviewStatus.Pending, "pre-condition: row must be Pending before heal");

        // Act — second call: case is now complete
        var completeResult = await _service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: true,
            cancellationToken: TestContext.Current.CancellationToken);
        completeResult.IsSuccess.ShouldBeTrue();

        // Assert — the IncompleteCase row must now be Completed (healed)
        var afterHeal = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId
                        && c.RequiresReviewReason == ReviewReason.IncompleteCase)
            .ToListAsync(TestContext.Current.CancellationToken);

        afterHeal.Count.ShouldBe(1, "the original IncompleteCase row must still exist (status changed, not deleted)");
        afterHeal[0].Status.ShouldBe(ReviewStatus.Completed,
            "the IncompleteCase row must be healed to Completed when isComplete=true");

        // Assert — no Pending IncompleteCase row remains
        var pendingAfter = afterHeal.Where(c => c.Status == ReviewStatus.Pending).ToList();
        pendingAfter.Count.ShouldBe(0,
            "no Pending IncompleteCase row must remain after the heal call");
    }

    // -----------------------------------------------------------------------
    // TC-4: isComplete=true with high confidence creates zero review cases
    // -----------------------------------------------------------------------

    /// <summary>
    /// Given isComplete=true, confidence >= 80, no conflicting/missing fields, and Level2 non-null,
    /// when IdentifyReviewCasesAsync is called,
    /// then zero ReviewCase rows are persisted.
    /// Guards against spurious cases being created for healthy documents.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WhenCompleteHighConfidence_CreatesNoReviewCase()
    {
        // Arrange
        var fileId = "FILE-IC-004";
        await SeedFileMetadataAsync(fileId);

        var metadata = CleanMetadata();
        var classification = HighConfidenceClassification();

        // Act — isComplete=true, no reasons to flag
        var result = await _service.IdentifyReviewCasesAsync(
            fileId, metadata, classification, isComplete: true,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — method returns success
        result.IsSuccess.ShouldBeTrue();

        // Assert — zero rows in the DB for this file
        var persistedCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId)
            .ToListAsync(TestContext.Current.CancellationToken);

        persistedCases.Count.ShouldBe(0,
            "a healthy complete document must not produce any ReviewCase rows");

        // Assert — return value is an empty list, not null
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0,
            "the returned list must be empty when no cases are created");
    }
}

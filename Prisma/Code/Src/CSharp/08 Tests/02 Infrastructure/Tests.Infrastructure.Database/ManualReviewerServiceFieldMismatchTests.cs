namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

using ExxerCube.Prisma.Domain.Services;

/// <summary>
/// Integration tests for the FieldMismatch dimension of
/// <see cref="ManualReviewerService.IdentifyReviewCasesAsync"/> (Item C — alertamiento, #9).
/// </summary>
/// <remarks>
/// Mirrors the <see cref="ManualReviewerServiceIsCompleteTests"/> pattern: in-memory EF Core DB,
/// orthogonal to the confidence/ambiguity/extraction path.
/// One alert present → flag an idempotent Pending FieldMismatch row.
/// Alerts cleared → heal (close) any pending FieldMismatch rows.
/// No alerts → no FieldMismatch row created.
/// Existing IncompleteCase dimension is unaffected.
/// </remarks>
public sealed class ManualReviewerServiceFieldMismatchTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _logger;
    private readonly ManualReviewerService _service;

    /// <summary>Initializes with a unique in-memory database per test.</summary>
    public ManualReviewerServiceFieldMismatchTests(ITestOutputHelper output)
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

    private static ClassificationResult HighConfidenceClassification() => new()
    {
        Level1 = ClassificationLevel1.Aseguramiento,
        Level2 = ClassificationLevel2.Judicial,
        Confidence = Confidence.FromInt(90),
    };

    /// <summary>Returns a metadata record with one <see cref="FieldConflictAlert"/>.</summary>
    private static UnifiedMetadataRecord MetadataWithOneAlert() => new()
    {
        FieldConflictAlerts = new List<FieldConflictAlert>
        {
            new(
                fieldName: "RFC",
                conflictingValues: new List<ConflictingSourceValue>
                {
                    new(SourceType.XML_HandFilled, "ABCD820101ABC"),
                    new(SourceType.PDF_OCR_CNBV, "ABCD820101XYZ"),
                },
                agreementLevel: 0.0f),
        },
        MatchedFields = new MatchedFields
        {
            ConflictingFields = new List<string>(),
            MissingFields = new List<string>(),
        },
    };

    /// <summary>Returns a metadata record with an empty <see cref="FieldConflictAlert"/> list.</summary>
    private static UnifiedMetadataRecord MetadataWithNoAlerts() => new()
    {
        FieldConflictAlerts = new List<FieldConflictAlert>(),
        MatchedFields = new MatchedFields
        {
            ConflictingFields = new List<string>(),
            MissingFields = new List<string>(),
        },
    };

    // -----------------------------------------------------------------------
    // TC-FM-1: FieldConflictAlerts non-empty → one Pending FieldMismatch row
    // -----------------------------------------------------------------------

    /// <summary>
    /// When metadata has at least one FieldConflictAlert,
    /// IdentifyReviewCasesAsync creates exactly one Pending FieldMismatch ReviewCase row.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithAlerts_PersistsFieldMismatchRow()
    {
        var fileId = "FILE-FM-001";
        await SeedFileMetadataAsync(fileId);

        var result = await _service.IdentifyReviewCasesAsync(
            fileId,
            MetadataWithOneAlert(),
            HighConfidenceClassification(),
            isComplete: true,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        var mismatchCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId && c.RequiresReviewReason == ReviewReason.FieldMismatch)
            .ToListAsync(TestContext.Current.CancellationToken);

        mismatchCases.Count.ShouldBe(1, "exactly one FieldMismatch row must be created when alerts are present");
        mismatchCases[0].Status.ShouldBe(ReviewStatus.Pending);
        mismatchCases[0].FileId.ShouldBe(fileId);
    }

    // -----------------------------------------------------------------------
    // TC-FM-2: Calling twice with alerts → idempotent (still one row)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Calling IdentifyReviewCasesAsync twice with the same alerts must not create a duplicate row.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithAlerts_Repeated_DoesNotDuplicate()
    {
        var fileId = "FILE-FM-002";
        await SeedFileMetadataAsync(fileId);

        var ct = TestContext.Current.CancellationToken;
        var metadata = MetadataWithOneAlert();
        var classification = HighConfidenceClassification();

        var result1 = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, isComplete: true, cancellationToken: ct);
        result1.IsSuccess.ShouldBeTrue();

        var result2 = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, isComplete: true, cancellationToken: ct);
        result2.IsSuccess.ShouldBeTrue();

        var mismatchCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId && c.RequiresReviewReason == ReviewReason.FieldMismatch)
            .ToListAsync(ct);

        mismatchCases.Count.ShouldBe(1, "a second call must not duplicate the FieldMismatch row");
    }

    // -----------------------------------------------------------------------
    // TC-FM-3: Alerts cleared → heal existing Pending FieldMismatch row
    // -----------------------------------------------------------------------

    /// <summary>
    /// After a first call with alerts (creates a Pending FieldMismatch row),
    /// a subsequent call with empty alerts heals (sets to Completed) the pending row.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_AlertsCleared_HealsRow()
    {
        var fileId = "FILE-FM-003";
        await SeedFileMetadataAsync(fileId);

        var ct = TestContext.Current.CancellationToken;
        var classification = HighConfidenceClassification();

        // First call — alerts present → creates Pending FieldMismatch row
        var resultWith = await _service.IdentifyReviewCasesAsync(
            fileId, MetadataWithOneAlert(), classification, isComplete: true, cancellationToken: ct);
        resultWith.IsSuccess.ShouldBeTrue();

        var before = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId && c.RequiresReviewReason == ReviewReason.FieldMismatch)
            .ToListAsync(ct);
        before.Count.ShouldBe(1, "pre-condition: one FieldMismatch row should exist");
        before[0].Status.ShouldBe(ReviewStatus.Pending, "pre-condition: row must be Pending before heal");

        // Second call — no alerts → heal
        var resultWithout = await _service.IdentifyReviewCasesAsync(
            fileId, MetadataWithNoAlerts(), classification, isComplete: true, cancellationToken: ct);
        resultWithout.IsSuccess.ShouldBeTrue();

        var after = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId && c.RequiresReviewReason == ReviewReason.FieldMismatch)
            .ToListAsync(ct);

        after.Count.ShouldBe(1, "the original FieldMismatch row must still exist (status changed, not deleted)");
        after[0].Status.ShouldBe(ReviewStatus.Completed, "the row must be healed to Completed when alerts are empty");

        var stillPending = after.Where(c => c.Status == ReviewStatus.Pending).ToList();
        stillPending.Count.ShouldBe(0, "no Pending FieldMismatch row must remain after the heal call");
    }

    // -----------------------------------------------------------------------
    // TC-FM-4: No alerts → no FieldMismatch row created
    // -----------------------------------------------------------------------

    /// <summary>
    /// When metadata has no FieldConflictAlerts and no prior FieldMismatch row,
    /// calling IdentifyReviewCasesAsync must not create any FieldMismatch case.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_NoAlerts_CreatesNoFieldMismatchRow()
    {
        var fileId = "FILE-FM-004";
        await SeedFileMetadataAsync(fileId);

        var result = await _service.IdentifyReviewCasesAsync(
            fileId,
            MetadataWithNoAlerts(),
            HighConfidenceClassification(),
            isComplete: true,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        var mismatchCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId && c.RequiresReviewReason == ReviewReason.FieldMismatch)
            .ToListAsync(TestContext.Current.CancellationToken);

        mismatchCases.Count.ShouldBe(0, "zero FieldMismatch rows when no alerts are present");
    }

    // -----------------------------------------------------------------------
    // TC-FM-5: FieldMismatch is orthogonal to IncompleteCase
    // -----------------------------------------------------------------------

    /// <summary>
    /// When both alerts are present AND isComplete=false, both FieldMismatch and IncompleteCase
    /// rows are created independently. The dimensions must not interfere.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_AlertsAndIncomplete_CreatesBothDimensions()
    {
        var fileId = "FILE-FM-005";
        await SeedFileMetadataAsync(fileId);

        var result = await _service.IdentifyReviewCasesAsync(
            fileId,
            MetadataWithOneAlert(),
            HighConfidenceClassification(),
            isComplete: false, // also triggers IncompleteCase dimension
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        var allCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId)
            .ToListAsync(TestContext.Current.CancellationToken);

        var mismatchRows = allCases.Where(c => c.RequiresReviewReason == ReviewReason.FieldMismatch).ToList();
        var incompleteRows = allCases.Where(c => c.RequiresReviewReason == ReviewReason.IncompleteCase).ToList();

        mismatchRows.Count.ShouldBe(1, "one FieldMismatch row when alerts are present");
        incompleteRows.Count.ShouldBe(1, "one IncompleteCase row when isComplete=false");

        mismatchRows[0].Status.ShouldBe(ReviewStatus.Pending);
        incompleteRows[0].Status.ShouldBe(ReviewStatus.Pending);
    }
}

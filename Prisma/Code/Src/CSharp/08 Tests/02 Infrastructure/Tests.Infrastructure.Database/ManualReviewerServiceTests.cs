namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Implementation-specific tests for <see cref="ManualReviewerService"/> that need direct fixture
/// seeding or verify implementation details.
/// </summary>
/// <remarks>
/// The cross-implementation contract behaviours (validation, cancellation, the identify rules, the
/// identify→submit flow) live in <see cref="ManualReviewerServiceContractTests"/> (inherited from
/// <c>ManualReviewerPanelContract</c>, Phase 5 of the ITDD refactor). The tests retained here seed the
/// <c>PrismaDbContext</c> directly and/or assert implementation specifics that are not contract-grade:
/// arbitrary status/confidence filtering, pagination counts, status-mapping verification,
/// duplicate-decision prevention, the notes-required-on-override rule, and field-annotation retrieval
/// (which needs seeded <c>FileMetadata</c>).
/// </remarks>
public class ManualReviewerServiceTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _logger;
    private readonly ManualReviewerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualReviewerServiceTests"/> class.
    /// </summary>
    public ManualReviewerServiceTests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<ManualReviewerService>(output);
        _service = new ManualReviewerService(_dbContext, _logger);
    }

     // GetReviewCasesAsync Tests

    /// <summary>
    /// Tests that GetReviewCasesAsync returns review cases successfully.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithNoFilters_ReturnsAllCases()
    {
        // Arrange
        var fileId = "FILE-001";
        var reviewCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            ClassificationAmbiguity = false,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        //Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken to allow test cancellation to be more responsive.
        // Act
        var result = await _service.GetReviewCasesAsync(null, 1, 50, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThan(0);
        result.Value.Any(c => c.CaseId == "CASE-001").ShouldBeTrue();
    }

    /// <summary>
    /// Tests that GetReviewCasesAsync filters by status correctly.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithStatusFilter_ReturnsFilteredCases()
    {
        // Arrange
        var fileId = "FILE-001";
        var pendingCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var completedCase = new ReviewCase
        {
            CaseId = "CASE-002",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 80,
            Status = ReviewStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ReviewCases.AddRangeAsync(new[] { pendingCase, completedCase }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filters = new ReviewFilters { Status = ReviewStatus.Pending };

        // Act
        var result = await _service.GetReviewCasesAsync(filters, 1, 50, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.All(c => c.Status == ReviewStatus.Pending).ShouldBeTrue();
        result.Value.Any(c => c.CaseId == "CASE-001").ShouldBeTrue();
        result.Value.Any(c => c.CaseId == "CASE-002").ShouldBeFalse();
    }

    /// <summary>
    /// Tests that GetReviewCasesAsync filters by confidence level correctly.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithConfidenceFilter_ReturnsFilteredCases()
    {
        // Arrange
        var fileId = "FILE-001";
        var lowConfidenceCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 70,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var highConfidenceCase = new ReviewCase
        {
            CaseId = "CASE-002",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 85,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ReviewCases.AddRangeAsync(new[] { lowConfidenceCase, highConfidenceCase }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filters = new ReviewFilters { MinConfidenceLevel = 70, MaxConfidenceLevel = 80 };

        // Act
        var result = await _service.GetReviewCasesAsync(filters, 1, 50, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.All(c => c.ConfidenceLevel >= 70 && c.ConfidenceLevel <= 80).ShouldBeTrue();
        result.Value.Any(c => c.CaseId == "CASE-001").ShouldBeTrue();
        result.Value.Any(c => c.CaseId == "CASE-002").ShouldBeFalse();
    }

    /// <summary>
    /// Tests that GetReviewCasesAsync supports pagination correctly.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        var fileId = "FILE-001";
        var cases = new List<ReviewCase>();
        for (int i = 1; i <= 10; i++)
        {
            cases.Add(new ReviewCase
            {
                CaseId = $"CASE-{i:D3}",
                FileId = fileId,
                RequiresReviewReason = ReviewReason.LowConfidence,
                ConfidenceLevel = 75,
                Status = ReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await _dbContext.ReviewCases.AddRangeAsync(cases, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Get first page
        var page1Result = await _service.GetReviewCasesAsync(null, 1, 5, TestContext.Current.CancellationToken);

        // Assert
        page1Result.IsSuccess.ShouldBeTrue();
        page1Result.Value.ShouldNotBeNull();
        page1Result.Value.Count.ShouldBe(5);

        // Act - Get second page
        var page2Result = await _service.GetReviewCasesAsync(null, 2, 5, TestContext.Current.CancellationToken);

        // Assert
        page2Result.IsSuccess.ShouldBeTrue();
        page2Result.Value.ShouldNotBeNull();
        page2Result.Value.Count.ShouldBe(5);
    }

     //  GetReviewCasesAsync Tests

     // SubmitReviewDecisionAsync Tests

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync submits decision successfully.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithValidDecision_SubmitsSuccessfully()
    {
        // Arrange
        var fileId = "FILE-001";
        var reviewCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved after review"
        };

        // Act
        var result = await _service.SubmitReviewDecisionAsync("CASE-001", decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedCase = await _dbContext.ReviewCases.FindAsync(new object[] { "CASE-001" }, TestContext.Current.CancellationToken);
        updatedCase.ShouldNotBeNull();
        updatedCase.Status.ShouldBe(ReviewStatus.Completed);

        var savedDecision = await _dbContext.ReviewDecisions.FirstOrDefaultAsync(d => d.DecisionId == "DEC-001", TestContext.Current.CancellationToken);
        savedDecision.ShouldNotBeNull();
        savedDecision.DecisionType.ShouldBe(DecisionType.Approve);
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync updates case status based on decision type.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithRejectDecision_UpdatesStatusToRejected()
    {
        // Arrange
        var fileId = "FILE-001";
        var reviewCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Reject,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Rejected due to errors"
        };

        // Act
        var result = await _service.SubmitReviewDecisionAsync("CASE-001", decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedCase = await _dbContext.ReviewCases.FindAsync(new object[] { "CASE-001" }, TestContext.Current.CancellationToken);
        updatedCase.ShouldNotBeNull();
        updatedCase.Status.ShouldBe(ReviewStatus.Rejected);
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync prevents duplicate decisions.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithExistingDecision_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        var reviewCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var existingDecision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Already approved"
        };

        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.ReviewDecisions.AddAsync(existingDecision, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newDecision = new ReviewDecision
        {
            DecisionId = "DEC-002",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Reject,
            ReviewerId = "REVIEWER-002",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Trying to reject"
        };

        // Act
        var result = await _service.SubmitReviewDecisionAsync("CASE-001", newDecision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("already been submitted");
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync requires notes when overrides are present.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithOverridesButNoNotes_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        var reviewCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = string.Empty, // Empty notes
            OverriddenFields = new Dictionary<string, object> { { "Expediente", "EXP-001" } }
        };

        // Act
        var result = await _service.SubmitReviewDecisionAsync("CASE-001", decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Notes are required when overriding");
    }

     //  SubmitReviewDecisionAsync Tests

     // GetFieldAnnotationsAsync Tests

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync returns field annotations successfully.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithValidCaseId_ReturnsAnnotations()
    {
        // Arrange
        var fileId = "FILE-001";
        var fileMetadata = new FileMetadata
        {
            FileId = fileId,
            FileName = "test.pdf",
            FilePath = "/path/to/test.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "test-checksum",
            FileSize = 1024,
            Format = FileFormat.Pdf
        };

        var reviewCase = new ReviewCase
        {
            CaseId = "CASE-001",
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 75,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.FileMetadata.AddAsync(fileMetadata, TestContext.Current.CancellationToken);
        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetFieldAnnotationsAsync("CASE-001", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.CaseId.ShouldBe("CASE-001");
        result.Value.FieldAnnotationsDict.ShouldNotBeNull();
    }

     //  GetFieldAnnotationsAsync Tests

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

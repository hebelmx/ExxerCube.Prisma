namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Unit tests for <see cref="ManualReviewerService"/>.
/// </summary>
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
    /// Tests that GetReviewCasesAsync handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await _service.GetReviewCasesAsync(null, 1, 50, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
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

    /// <summary>
    /// Tests that GetReviewCasesAsync validates pagination parameters.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithInvalidPagination_ReturnsFailure()
    {
        // Act - Invalid page number
        var result1 = await _service.GetReviewCasesAsync(null, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        result1.IsFailure.ShouldBeTrue();
        result1.Error.ShouldContain("Page number must be greater than 0");

        // Act - Invalid page size
        var result2 = await _service.GetReviewCasesAsync(null, 1, 0, TestContext.Current.CancellationToken);

        // Assert
        result2.IsFailure.ShouldBeTrue();
        result2.Error.ShouldContain("Page size must be between 1 and 1000");
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
    /// Tests that SubmitReviewDecisionAsync returns failure for invalid case ID.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithInvalidCaseId_ReturnsFailure()
    {
        // Arrange
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "INVALID-CASE",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };

        // Act
        var result = await _service.SubmitReviewDecisionAsync("INVALID-CASE", decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("not found");
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await _service.SubmitReviewDecisionAsync("CASE-001", decision, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
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

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync returns failure for invalid case ID.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithInvalidCaseId_ReturnsFailure()
    {
        // Act
        var result = await _service.GetFieldAnnotationsAsync("INVALID-CASE", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await _service.GetFieldAnnotationsAsync("CASE-001", cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

     //  GetFieldAnnotationsAsync Tests

     // IdentifyReviewCasesAsync Tests

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync identifies low confidence cases.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithLowConfidence_IdentifiesCase()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord
        {
            Classification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = 75
            }
        };

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Confidence = 75
        };

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThan(0);
        result.Value.Any(c => c.RequiresReviewReason == ReviewReason.LowConfidence).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync identifies ambiguous classification cases.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithAmbiguousClassification_IdentifiesCase()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord
        {
            Classification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Level2 = null,
                Confidence = 85
            }
        };

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = null,
            Confidence = 85
        };

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Any(c => c.RequiresReviewReason == ReviewReason.AmbiguousClassification && c.ClassificationAmbiguity).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync identifies extraction error cases.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithExtractionErrors_IdentifiesCase()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord
        {
            MatchedFields = new MatchedFields
            {
                ConflictingFields = new List<string> { "Expediente", "Causa" }
            },
            Classification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = 90
            }
        };

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Confidence = 90
        };

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Any(c => c.RequiresReviewReason == ReviewReason.ExtractionError).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync returns empty list for high confidence cases.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithHighConfidenceNoIssues_ReturnsEmptyList()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord
        {
            Classification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Level2 = ClassificationLevel2.Judicial,
                Confidence = 95
            },
            MatchedFields = new MatchedFields
            {
                ConflictingFields = new List<string>()
            }
        };

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Judicial,
            Confidence = 95
        };

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync returns failure for null metadata.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithNullMetadata_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        UnifiedMetadataRecord? metadata = null;
        var classification = new ClassificationResult();

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata!, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Metadata cannot be null");
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync returns failure for null classification.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithNullClassification_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord();
        ClassificationResult? classification = null;

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Classification cannot be null");
    }

     //  IdentifyReviewCasesAsync Tests

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
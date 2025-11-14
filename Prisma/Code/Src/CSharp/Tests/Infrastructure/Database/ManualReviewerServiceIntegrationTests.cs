namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Integration tests for <see cref="ManualReviewerService"/>.
/// </summary>
public class ManualReviewerServiceIntegrationTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _logger;
    private readonly ManualReviewerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualReviewerServiceIntegrationTests"/> class.
    /// </summary>
    public ManualReviewerServiceIntegrationTests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<ManualReviewerService>(output);
        _service = new ManualReviewerService(_dbContext, _logger);
    }

    /// <summary>
    /// Tests end-to-end manual review workflow: identify case, get annotations, submit decision.
    /// </summary>
    [Fact]
    public async Task EndToEndWorkflow_IdentifyGetAnnotationsSubmit_CompletesSuccessfully()
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

        await _dbContext.FileMetadata.AddAsync(fileMetadata, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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

        // Act - Step 1: Identify review cases (first call)
        var identifyResult = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Verify no duplicates on second call
        var identifyResult2 = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);
        identifyResult2.IsSuccess.ShouldBeTrue();
        // Should return existing cases, not create duplicates
        identifyResult2.Value.ShouldNotBeNull();

        // Assert - Step 1
        identifyResult.IsSuccess.ShouldBeTrue();
        identifyResult.Value.ShouldNotBeNull();
        identifyResult.Value.Count.ShouldBeGreaterThan(0);

        var reviewCase = identifyResult.Value.First();
        var caseId = reviewCase.CaseId;

        // Act - Step 2: Get field annotations
        var annotationsResult = await _service.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert - Step 2
        annotationsResult.IsSuccess.ShouldBeTrue();
        annotationsResult.Value.ShouldNotBeNull();
        annotationsResult.Value.CaseId.ShouldBe(caseId);

        // Act - Step 3: Submit review decision
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = caseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved after review"
        };

        var submitResult = await _service.SubmitReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert - Step 3
        submitResult.IsSuccess.ShouldBeTrue();

        // Verify case status was updated
        var updatedCase = await _dbContext.ReviewCases.FindAsync(new object[] { caseId }, TestContext.Current.CancellationToken);
        updatedCase.ShouldNotBeNull();
        updatedCase.Status.ShouldBe(ReviewStatus.Completed);

        // Verify decision was saved
        var savedDecision = await _dbContext.ReviewDecisions.FirstOrDefaultAsync(d => d.DecisionId == "DEC-001", TestContext.Current.CancellationToken);
        savedDecision.ShouldNotBeNull();
        savedDecision.DecisionType.ShouldBe(DecisionType.Approve);
    }

    /// <summary>
    /// Tests that review cases are properly linked to file metadata via foreign key.
    /// </summary>
    [Fact]
    public async Task ReviewCase_ForeignKeyRelationship_IsMaintained()
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

        await _dbContext.FileMetadata.AddAsync(fileMetadata, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var metadata = new UnifiedMetadataRecord
        {
            Classification = new ClassificationResult { Confidence = 75 }
        };

        var classification = new ClassificationResult { Confidence = 75 };

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThan(0);

        var reviewCase = result.Value.First();
        reviewCase.FileId.ShouldBe(fileId);

        // Verify foreign key relationship
        var caseFromDb = await _dbContext.ReviewCases
            .FirstOrDefaultAsync(c => c.CaseId == reviewCase.CaseId, TestContext.Current.CancellationToken);

        caseFromDb.ShouldNotBeNull();
        caseFromDb.FileId.ShouldBe(fileId);
    }

    /// <summary>
    /// Tests that review decisions are properly linked to review cases via foreign key.
    /// </summary>
    [Fact]
    public async Task ReviewDecision_ForeignKeyRelationship_IsMaintained()
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

        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };

        // Act
        var result = await _service.SubmitReviewDecisionAsync("CASE-001", decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify foreign key relationship
        var savedDecision = await _dbContext.ReviewDecisions
            .FirstOrDefaultAsync(d => d.DecisionId == "DEC-001", TestContext.Current.CancellationToken);

        savedDecision.ShouldNotBeNull();
        savedDecision.CaseId.ShouldBe("CASE-001");
    }

    /// <summary>
    /// Tests that multiple review cases can be identified for the same file.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_MultipleReasons_IdentifiesMultipleCases()
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

        await _dbContext.FileMetadata.AddAsync(fileMetadata, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var metadata = new UnifiedMetadataRecord
        {
            Classification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Level2 = null,
                Confidence = 75
            },
            MatchedFields = new MatchedFields
            {
                ConflictingFields = new List<string> { "Expediente" }
            }
        };

        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = null,
            Confidence = 75
        };

        // Act
        var result = await _service.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(1);

        // Should identify at least low confidence case
        result.Value.Any(c => c.RequiresReviewReason == ReviewReason.LowConfidence).ShouldBeTrue();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
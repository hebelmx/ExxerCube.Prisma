namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="DecisionLogicService"/> manual review workflows, including case identification and cancellation handling.
/// </summary>
public class DecisionLogicServiceManualReviewTests
{
    private readonly IPersonIdentityResolver _personIdentityResolver;
    private readonly ILegalDirectiveClassifier _legalDirectiveClassifier;
    private readonly IManualReviewerPanel _manualReviewerPanel;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DecisionLogicService> _logger;
    private readonly DecisionLogicService _service;

    /// <summary>
    /// Initializes the test suite with mocked collaborators and audit logging.
    /// </summary>
    public DecisionLogicServiceManualReviewTests()
    {
        _personIdentityResolver = Substitute.For<IPersonIdentityResolver>();
        _legalDirectiveClassifier = Substitute.For<ILegalDirectiveClassifier>();
        _manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = Substitute.For<ILogger<DecisionLogicService>>();
        _service = new DecisionLogicService(_personIdentityResolver, _legalDirectiveClassifier, _manualReviewerPanel, _auditLogger, _logger);
    }

     // IdentifyAndQueueReviewCasesAsync Tests

    /// <summary>
    /// Verifies <see cref="DecisionLogicService.IdentifyAndQueueReviewCasesAsync"/> returns review cases when confidence is low.
    /// </summary>
    /// <returns>A task that completes after review-case assertions are evaluated.</returns>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WithLowConfidence_ReturnsReviewCases()
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

        var expectedCases = new List<ReviewCase>
        {
            new ReviewCase
            {
                CaseId = "CASE-001",
                FileId = fileId,
                RequiresReviewReason = ReviewReason.LowConfidence,
                ConfidenceLevel = 75,
                Status = ReviewStatus.Pending
            }
        };

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Act
        var result = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].RequiresReviewReason.ShouldBe(ReviewReason.LowConfidence);
    }

    /// <summary>
    /// Verifies cancellation is honored when identifying review cases.
    /// </summary>
    /// <returns>A task that completes after cancellation assertions are evaluated.</returns>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, cancellationTokenSource.Token)
            .Returns(ResultExtensions.Cancelled<List<ReviewCase>>());

        // Act
        var result = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata, classification, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Verifies failures from the manual reviewer panel are surfaced to callers.
    /// </summary>
    /// <returns>A task that completes after failure handling assertions are evaluated.</returns>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WhenIdentificationFails_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult();

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.WithFailure("Identification failed"));

        // Act
        var result = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Identification failed");
    }

    /// <summary>
    /// Tests that IdentifyAndQueueReviewCasesAsync returns failure for null fileId.
    /// </summary>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WithNullFileId_ReturnsFailure()
    {
        // Arrange
        string fileId = null!;
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult();

        // Act
        var result = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileId cannot be null or empty");
    }

    /// <summary>
    /// Tests that IdentifyAndQueueReviewCasesAsync returns failure for null metadata.
    /// </summary>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WithNullMetadata_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        UnifiedMetadataRecord? metadata = null;
        var classification = new ClassificationResult();

        // Act
        var result = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata!, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Metadata cannot be null");
    }

    /// <summary>
    /// Tests that IdentifyAndQueueReviewCasesAsync returns failure for null classification.
    /// </summary>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WithNullClassification_ReturnsFailure()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord();
        ClassificationResult? classification = null;

        // Act
        var result = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata, classification!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Classification cannot be null");
    }

     // 

     // ProcessReviewDecisionAsync Tests

    /// <summary>
    /// Tests that ProcessReviewDecisionAsync processes decision successfully.
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WithValidDecision_ProcessesSuccessfully()
    {
        // Arrange
        var caseId = "CASE-001";
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = caseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved after review"
        };

        _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.ProcessReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ProcessReviewDecisionAsync handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var caseId = "CASE-001";
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = caseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, cancellationTokenSource.Token)
            .Returns(ResultExtensions.Cancelled());

        // Act
        var result = await _service.ProcessReviewDecisionAsync(caseId, decision, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ProcessReviewDecisionAsync returns failure when submission fails.
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WhenSubmissionFails_ReturnsFailure()
    {
        // Arrange
        var caseId = "CASE-001";
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = caseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };

        _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("Submission failed"));

        // Act
        var result = await _service.ProcessReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Submission failed");
    }

    /// <summary>
    /// Tests that ProcessReviewDecisionAsync returns failure for null caseId.
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WithNullCaseId_ReturnsFailure()
    {
        // Arrange
        string caseId = null!;
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
        var result = await _service.ProcessReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("CaseId cannot be null or empty");
    }

    /// <summary>
    /// Tests that ProcessReviewDecisionAsync returns failure for null decision.
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WithNullDecision_ReturnsFailure()
    {
        // Arrange
        var caseId = "CASE-001";
        ReviewDecision? decision = null;

        // Act
        var result = await _service.ProcessReviewDecisionAsync(caseId, decision!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Decision cannot be null");
    }

     // 
}

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// IITDD contract tests for <see cref="IManualReviewerPanel"/> interface.
/// These tests validate the interface contract (WHAT) using mocks, not implementation details (HOW).
/// Tests must pass for ANY valid implementation (Liskov Substitution Principle).
/// </summary>
public class IIManualReviewerPanelTests
{
    private readonly IManualReviewerPanel _manualReviewerPanel;

    /// <summary>
    /// Initializes a new instance of the <see cref="IIManualReviewerPanelTests"/> class.
    /// </summary>
    public IIManualReviewerPanelTests()
    {
        _manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
    }

     // GetReviewCasesAsync Contract Tests

    /// <summary>
    /// Tests that GetReviewCasesAsync returns Result&lt;List&lt;ReviewCase&gt;&gt; on success.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithValidFilters_ReturnsSuccessResult()
    {
        // Arrange
        var filters = new ReviewFilters
        {
            Status = ReviewStatus.Pending,
            MinConfidenceLevel = 70,
            MaxConfidenceLevel = 80
        };

        var expectedCases = new List<ReviewCase>
        {
            new ReviewCase
            {
                CaseId = "CASE-001",
                FileId = "FILE-001",
                Status = ReviewStatus.Pending,
                ConfidenceLevel = 75
            }
        };

        _manualReviewerPanel.GetReviewCasesAsync(filters, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Act
        var result = await _manualReviewerPanel.GetReviewCasesAsync(filters, 1, 50, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].CaseId.ShouldBe("CASE-001");
    }

    /// <summary>
    /// Tests that GetReviewCasesAsync returns failure Result on query errors.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_OnQueryError_ReturnsFailureResult()
    {
        // Arrange
        var filters = new ReviewFilters { Status = ReviewStatus.Pending };

        _manualReviewerPanel.GetReviewCasesAsync(filters, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.WithFailure("Database query failed"));

        // Act
        var result = await _manualReviewerPanel.GetReviewCasesAsync(filters, 1, 50, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Database query failed");
    }

    /// <summary>
    /// Tests that GetReviewCasesAsync propagates cancellation token.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var filters = new ReviewFilters();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _manualReviewerPanel.GetReviewCasesAsync(filters, Arg.Any<int>(), Arg.Any<int>(), cancellationTokenSource.Token)
            .Returns(ResultExtensions.Cancelled<List<ReviewCase>>());

        // Act
        var result = await _manualReviewerPanel.GetReviewCasesAsync(filters, 1, 50, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that GetReviewCasesAsync accepts null filters.
    /// </summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithNullFilters_ReturnsSuccessResult()
    {
        // Arrange
        ReviewFilters? filters = null;
        var expectedCases = new List<ReviewCase>();

        _manualReviewerPanel.GetReviewCasesAsync(filters, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Act
        var result = await _manualReviewerPanel.GetReviewCasesAsync(filters, 1, 50, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

     // 

     // SubmitReviewDecisionAsync Contract Tests

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync returns Result (success) on successful submission.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithValidDecision_ReturnsSuccessResult()
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
        var result = await _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync returns failure Result on invalid case ID.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithInvalidCaseId_ReturnsFailureResult()
    {
        // Arrange
        var caseId = "INVALID-CASE";
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
            .Returns(Result.WithFailure("Case not found"));

        // Act
        var result = await _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Case not found");
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync returns failure Result on save errors.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_OnSaveError_ReturnsFailureResult()
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
            .Returns(Result.WithFailure("Database save failed"));

        // Act
        var result = await _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Database save failed");
    }

    /// <summary>
    /// Tests that SubmitReviewDecisionAsync propagates cancellation token.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WhenCancelled_ReturnsCancelledResult()
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
        var result = await _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

     // 

     // GetFieldAnnotationsAsync Contract Tests

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync returns Result&lt;FieldAnnotations&gt; for valid case ID.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithValidCaseId_ReturnsSuccessResult()
    {
        // Arrange
        var caseId = "CASE-001";
        var expectedAnnotations = new FieldAnnotations
        {
            CaseId = caseId,
            FieldAnnotationsDict = new Dictionary<string, FieldAnnotation>
            {
                {
                    "Expediente",
                    new FieldAnnotation
                    {
                        FieldName = "Expediente",
                        Value = "EXP-001",
                        Confidence = 85,
                        Source = "XML",
                        HasConflict = false,
                        AgreementLevel = 1.0f
                    }
                }
            }
        };

        _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, Arg.Any<CancellationToken>())
            .Returns(Result<FieldAnnotations>.Success(expectedAnnotations));

        // Act
        var result = await _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.CaseId.ShouldBe(caseId);
        result.Value.FieldAnnotationsDict.Count.ShouldBe(1);
    }

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync returns failure Result on invalid case ID.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithInvalidCaseId_ReturnsFailureResult()
    {
        // Arrange
        var caseId = "INVALID-CASE";

        _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, Arg.Any<CancellationToken>())
            .Returns(Result<FieldAnnotations>.WithFailure("Case not found"));

        // Act
        var result = await _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Case not found");
    }

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync returns failure Result on query errors.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_OnQueryError_ReturnsFailureResult()
    {
        // Arrange
        var caseId = "CASE-001";

        _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, Arg.Any<CancellationToken>())
            .Returns(Result<FieldAnnotations>.WithFailure("Database query failed"));

        // Act
        var result = await _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Database query failed");
    }

    /// <summary>
    /// Tests that GetFieldAnnotationsAsync propagates cancellation token.
    /// </summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var caseId = "CASE-001";
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, cancellationTokenSource.Token)
            .Returns(ResultExtensions.Cancelled<FieldAnnotations>());

        // Act
        var result = await _manualReviewerPanel.GetFieldAnnotationsAsync(caseId, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

     // 

     // IdentifyReviewCasesAsync Contract Tests

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync identifies low confidence cases (< 80%).
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithLowConfidence_ReturnsReviewCase()
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
        var result = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].RequiresReviewReason.ShouldBe(ReviewReason.LowConfidence);
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync identifies ambiguous classification cases.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithAmbiguousClassification_ReturnsReviewCase()
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

        var expectedCases = new List<ReviewCase>
        {
            new ReviewCase
            {
                CaseId = "CASE-001",
                FileId = fileId,
                RequiresReviewReason = ReviewReason.AmbiguousClassification,
                ClassificationAmbiguity = true,
                ConfidenceLevel = 85,
                Status = ReviewStatus.Pending
            }
        };

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Act
        var result = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].RequiresReviewReason.ShouldBe(ReviewReason.AmbiguousClassification);
        result.Value[0].ClassificationAmbiguity.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync identifies extraction error cases.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithExtractionErrors_ReturnsReviewCase()
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

        var expectedCases = new List<ReviewCase>
        {
            new ReviewCase
            {
                CaseId = "CASE-001",
                FileId = fileId,
                RequiresReviewReason = ReviewReason.ExtractionError,
                ConfidenceLevel = 90,
                Status = ReviewStatus.Pending
            }
        };

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Act
        var result = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        result.Value[0].RequiresReviewReason.ShouldBe(ReviewReason.ExtractionError);
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync returns empty list when no review cases are identified.
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

        var expectedCases = new List<ReviewCase>();

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Act
        var result = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync returns failure Result on identification errors.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_OnIdentificationError_ReturnsFailureResult()
    {
        // Arrange
        var fileId = "FILE-001";
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult();

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.WithFailure("Identification failed"));

        // Act
        var result = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Identification failed");
    }

    /// <summary>
    /// Tests that IdentifyReviewCasesAsync propagates cancellation token.
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

        _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, cancellationTokenSource.Token)
            .Returns(ResultExtensions.Cancelled<List<ReviewCase>>());

        // Act
        var result = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, cancellationTokenSource.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

     // 
}


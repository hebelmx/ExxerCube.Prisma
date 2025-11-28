namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Integration tests for manual review workflows verifying IV1 and IV2 requirements.
/// </summary>
public class ManualReviewIntegrationTests
{
    private readonly IPersonIdentityResolver _personIdentityResolver;
    private readonly ILegalDirectiveClassifier _legalDirectiveClassifier;
    private readonly IManualReviewerPanel _manualReviewerPanel;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DecisionLogicService> _logger;
    private readonly DecisionLogicService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualReviewIntegrationTests"/> class with mocked collaborators.
    /// </summary>
    public ManualReviewIntegrationTests()
    {
        _personIdentityResolver = Substitute.For<IPersonIdentityResolver>();
        _legalDirectiveClassifier = Substitute.For<ILegalDirectiveClassifier>();
        _manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = Substitute.For<ILogger<DecisionLogicService>>();
        _service = new DecisionLogicService(_personIdentityResolver, _legalDirectiveClassifier, _manualReviewerPanel, _auditLogger, _logger);
    }

    /// <summary>
    /// Tests IV1: Manual review interface does not disrupt existing document processing workflows.
    /// </summary>
    /// <returns>A task that completes after validating the main workflow is intact.</returns>
    [Fact]
    public async Task ProcessDecisionLogicAsync_WithManualReview_DoesNotDisruptExistingWorkflow()
    {
        // Arrange
        var persons = new List<Persona>
        {
            new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" }
        };

        var documentText = "Se ordena el BLOQUEO de la cuenta 1234567890";
        var expediente = new Expediente { NumeroExpediente = "EXP-001", NumeroOficio = "OF-001" };

        var resolvedPerson = new Persona { ParteId = 1, Nombre = "Juan", Rfc = "PEGJ850101ABC" };
        var resolvedList = new List<Persona> { resolvedPerson };
        var actions = new List<ComplianceAction>
        {
            new ComplianceAction { ActionType = ComplianceActionKind.Block, Confidence = 80 }
        };

        _personIdentityResolver.ResolveIdentityAsync(Arg.Any<Persona>(), Arg.Any<CancellationToken>())
            .Returns(Result<Persona>.Success(resolvedPerson));

        _personIdentityResolver.DeduplicatePersonsAsync(Arg.Any<List<Persona>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<Persona>>.Success(resolvedList));

        _legalDirectiveClassifier.DetectLegalInstrumentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<string>>.Success(new List<string>()));

        _legalDirectiveClassifier.ClassifyDirectivesAsync(Arg.Any<string>(), Arg.Any<Expediente>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ComplianceAction>>.Success(actions));

        // Act
        var result = await _service.ProcessDecisionLogicAsync(persons, documentText, expediente, TestContext.Current.CancellationToken);

        // Assert - Existing workflow should still work
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ResolvedPersons.Count.ShouldBe(1);
        result.Value.ComplianceActions.Count.ShouldBe(1);
    }

    /// <summary>
    /// Tests IV2: Review decisions integrate with existing data models without breaking existing functionality.
    /// </summary>
    /// <returns>A task that completes after verifying review decisions integrate correctly.</returns>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WithExistingDataModels_IntegratesCorrectly()
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
            Notes = "Approved",
            OverriddenFields = new Dictionary<string, object>
            {
                { "Expediente", "EXP-001" }
            },
            OverriddenClassification = new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Level2 = ClassificationLevel2.Judicial,
                Confidence = 90
            }
        };

        _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.ProcessReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken);

        // Assert - Decision should integrate with existing models
        result.IsSuccess.ShouldBeTrue();

        // Verify decision contains valid data compatible with existing models
        decision.OverriddenFields.ShouldNotBeNull();
        decision.OverriddenClassification.ShouldNotBeNull();
        decision.OverriddenClassification.Level1.ShouldBe(ClassificationLevel1.Aseguramiento);
    }

    /// <summary>
    /// Tests end-to-end workflow: identify cases, process decision, verify integration.
    /// </summary>
    [Fact]
    public async Task EndToEndWorkflow_IdentifyAndProcessDecision_CompletesSuccessfully()
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

        var reviewCases = new List<ReviewCase>
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
            .Returns(Result<List<ReviewCase>>.Success(reviewCases));

        _manualReviewerPanel.SubmitReviewDecisionAsync(Arg.Any<string>(), Arg.Any<ReviewDecision>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act - Step 1: Identify review cases
        var identifyResult = await _service.IdentifyAndQueueReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        // Assert - Step 1
        identifyResult.IsSuccess.ShouldBeTrue();
        identifyResult.Value.ShouldNotBeNull();
        identifyResult.Value.Count.ShouldBe(1);

        var reviewCase = identifyResult.Value.First();

        // Act - Step 2: Process review decision
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = reviewCase.CaseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };

        var processResult = await _service.ProcessReviewDecisionAsync(reviewCase.CaseId, decision, TestContext.Current.CancellationToken);

        // Assert - Step 2
        processResult.IsSuccess.ShouldBeTrue();
    }
}


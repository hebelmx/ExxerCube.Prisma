namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Tests for <see cref="DecisionLogicService"/> C2 behaviour: persisting the
/// <see cref="UnifiedMetadataRecord"/> via <see cref="IUnifiedMetadataStore"/> after review-case
/// identification, and applying reviewer overrides when processing a decision.
/// </summary>
public class DecisionLogicServiceUnifiedMetadataStoreTests
{
    private readonly IPersonIdentityResolver _personIdentityResolver;
    private readonly ILegalDirectiveClassifier _legalDirectiveClassifier;
    private readonly IManualReviewerPanel _manualReviewerPanel;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DecisionLogicService> _logger;

    /// <summary>Initializes test dependencies via NSubstitute.</summary>
    public DecisionLogicServiceUnifiedMetadataStoreTests()
    {
        _personIdentityResolver = Substitute.For<IPersonIdentityResolver>();
        _legalDirectiveClassifier = Substitute.For<ILegalDirectiveClassifier>();
        _manualReviewerPanel = Substitute.For<IManualReviewerPanel>();
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = Substitute.For<ILogger<DecisionLogicService>>();
    }

    // ──────────────────────────────────────────────────────────────────
    // IdentifyAndQueueReviewCasesAsync — C2 persist
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the store is provided and the panel succeeds, SaveAsync is called with the supplied record.
    /// </summary>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WithStore_PersistsMetadataRecord()
    {
        // Arrange
        const string fileId = "FILE-C2-001";
        var metadata = new UnifiedMetadataRecord
        {
            AdditionalFields = { ["AreaDescripcion"] = "NUEVA" },
        };
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Confidence = Confidence.FromInt(75),
        };
        var expectedCases = new List<ReviewCase>
        {
            new() { CaseId = "CASE-001", FileId = fileId, Status = ReviewStatus.Pending },
        };

        _manualReviewerPanel
            .IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // Use an in-memory fake store so we can inspect what was saved
        var fakeStore = new InMemoryUnifiedMetadataStore();
        var service = new DecisionLogicService(
            _personIdentityResolver, _legalDirectiveClassifier,
            _manualReviewerPanel, _auditLogger, _logger,
            fakeStore);

        // Act
        var result = await service.IdentifyAndQueueReviewCasesAsync(
            fileId, metadata, classification, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — identification succeeded
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(1);

        // Assert — store received the record
        var stored = fakeStore.Get(fileId);
        stored.ShouldNotBeNull();
        stored!.AdditionalFields["AreaDescripcion"].ShouldBe("NUEVA");
    }

    /// <summary>
    /// When the store is null (optional param not provided), identification succeeds without any store call.
    /// </summary>
    [Fact]
    public async Task IdentifyAndQueueReviewCasesAsync_WithoutStore_SucceedsGracefully()
    {
        // Arrange
        const string fileId = "FILE-C2-NOSTOP-001";
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult { Confidence = Confidence.FromInt(90) };
        var expectedCases = new List<ReviewCase>
        {
            new() { CaseId = "CASE-002", FileId = fileId, Status = ReviewStatus.Pending },
        };

        _manualReviewerPanel
            .IdentifyReviewCasesAsync(fileId, metadata, classification, Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(expectedCases));

        // No store — the optional param defaults to null
        var service = new DecisionLogicService(
            _personIdentityResolver, _legalDirectiveClassifier,
            _manualReviewerPanel, _auditLogger, _logger);

        // Act
        var result = await service.IdentifyAndQueueReviewCasesAsync(
            fileId, metadata, classification, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────
    // ProcessReviewDecisionAsync — C2 apply overrides
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// When a decision carries OverriddenFields and a record exists in the store, the store receives
    /// SaveAsync with those fields applied to AdditionalFields.
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WithOverriddenFields_UpdatesStoreRecord()
    {
        // Arrange
        const string fileId = "FILE-C2-OVERRIDE-001";
        const string caseId = "CASE-OVR-001";

        // Seed the fake store with a pre-existing record
        var fakeStore = new InMemoryUnifiedMetadataStore();
        await fakeStore.SaveAsync(fileId, new UnifiedMetadataRecord
        {
            AdditionalFields =
            {
                ["AreaDescripcion"] = "ORIGINAL",
                ["OtherField"] = "unchanged",
            },
        }, TestContext.Current.CancellationToken);

        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = caseId,
            FileId = fileId,
            ReviewerId = "rev1",
            Notes = "Correcting area",
            DecisionType = DecisionType.Approve,
            ReviewReason = ReviewReason.LowConfidence,
            OverriddenFields = new Dictionary<string, object> { ["AreaDescripcion"] = "NUEVA" },
        };

        _manualReviewerPanel
            .SubmitReviewDecisionAsync(caseId, decision, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _auditLogger
            .LogAuditAsync(
                Arg.Any<AuditActionType>(), Arg.Any<ProcessingStage>(),
                Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<bool>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var service = new DecisionLogicService(
            _personIdentityResolver, _legalDirectiveClassifier,
            _manualReviewerPanel, _auditLogger, _logger,
            fakeStore);

        // Act
        var result = await service.ProcessReviewDecisionAsync(
            caseId, decision, TestContext.Current.CancellationToken);

        // Assert — decision processing succeeded
        result.IsSuccess.ShouldBeTrue();

        // Assert — store has updated the overridden field
        var stored = fakeStore.Get(fileId);
        stored.ShouldNotBeNull();
        stored!.AdditionalFields["AreaDescripcion"].ShouldBe("NUEVA");
        // Non-overridden field is untouched
        stored.AdditionalFields["OtherField"].ShouldBe("unchanged");
    }

    /// <summary>
    /// When the store fails to load the record, ProcessReviewDecisionAsync still returns success
    /// (fail-open — the decision itself was persisted, only the metadata update is skipped).
    /// </summary>
    [Fact]
    public async Task ProcessReviewDecisionAsync_WhenStoreFails_StillSucceeds()
    {
        // Arrange
        const string fileId = "FILE-C2-STOREFAIL-001";
        const string caseId = "CASE-STOREFAIL-001";

        // Store is empty — GetByFileIdAsync will return failure
        var fakeStore = new InMemoryUnifiedMetadataStore();

        var decision = new ReviewDecision
        {
            DecisionId = "DEC-002",
            CaseId = caseId,
            FileId = fileId,
            ReviewerId = "rev1",
            Notes = "Test",
            DecisionType = DecisionType.Approve,
            ReviewReason = ReviewReason.LowConfidence,
            OverriddenFields = new Dictionary<string, object> { ["AreaDescripcion"] = "X" },
        };

        _manualReviewerPanel
            .SubmitReviewDecisionAsync(caseId, decision, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _auditLogger
            .LogAuditAsync(
                Arg.Any<AuditActionType>(), Arg.Any<ProcessingStage>(),
                Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<bool>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var service = new DecisionLogicService(
            _personIdentityResolver, _legalDirectiveClassifier,
            _manualReviewerPanel, _auditLogger, _logger,
            fakeStore);

        // Act
        var result = await service.ProcessReviewDecisionAsync(
            caseId, decision, TestContext.Current.CancellationToken);

        // Assert — still succeeds (fail-open)
        result.IsSuccess.ShouldBeTrue();
    }
}

/// <summary>
/// Simple in-memory <see cref="IUnifiedMetadataStore"/> for Application-layer tests that want to
/// inspect what was saved without a database.
/// </summary>
internal sealed class InMemoryUnifiedMetadataStore : IUnifiedMetadataStore
{
    private readonly Dictionary<string, UnifiedMetadataRecord> _data = new(StringComparer.Ordinal);

    /// <summary>Returns the stored record for <paramref name="fileId"/>, or <c>null</c> if absent.</summary>
    public UnifiedMetadataRecord? Get(string fileId)
        => _data.TryGetValue(fileId, out var r) ? r : null;

    /// <inheritdoc />
    public Task<Result> SaveAsync(string fileId, UnifiedMetadataRecord record, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled());
        }

        _data[fileId] = record;
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result<UnifiedMetadataRecord?>> GetByFileIdAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<UnifiedMetadataRecord?>());
        }

        if (_data.TryGetValue(fileId, out var record))
        {
            return Task.FromResult(Result<UnifiedMetadataRecord?>.Success(record));
        }

        return Task.FromResult(Result<UnifiedMetadataRecord?>.WithFailure($"No unified metadata record for file {fileId}"));
    }
}

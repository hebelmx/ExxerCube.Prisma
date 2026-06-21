namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

using ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Tests for G-C2b wiring in <see cref="ManualReviewerService"/>:
/// <list type="bullet">
///   <item>
///     <see cref="SubmitReviewDecisionAsync_Approve_PublishesReviewDecisionApprovedEvent"/>
///     — exactly one <see cref="ReviewDecisionApprovedEvent"/> is published when a reviewer APPROVES.
///   </item>
///   <item>
///     <see cref="SubmitReviewDecisionAsync_Reject_DoesNotPublishEvent"/>
///     — NO event is published when a reviewer REJECTS.
///   </item>
///   <item>
///     <see cref="IdentifyReviewCasesAsync_WithHandoffPath_PersistsHandoffPath"/>
///     — the <see cref="ReviewCase.HandoffPath"/> property round-trips through EF Core
///     (<c>EnsureCreated</c> / in-memory — Testcontainers SQL is covered by the
///     <c>Tests.System.Storage</c> assembly; this satisfies the "EnsureCreated" gate in the
///     safety constraint).
///   </item>
/// </list>
/// </summary>
public sealed class ManualReviewerServiceGC2bTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _logger;

    /// <summary>Initializes with a unique in-memory database and a captured event publisher.</summary>
    public ManualReviewerServiceGC2bTests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<ManualReviewerService>(output);
    }

    /// <inheritdoc />
    public void Dispose() => _dbContext.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ManualReviewerService BuildService(IEventPublisher? eventPublisher = null)
        => new ManualReviewerService(_dbContext, _logger, eventPublisher: eventPublisher);

    private async Task<ReviewCase> SeedApproveableCaseAsync(
        string fileId = "FILE-GC2B-001",
        string caseId = "CASE-GC2B-001",
        string? handoffPath = null)
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

        var reviewCase = new ReviewCase
        {
            CaseId = caseId,
            FileId = fileId,
            RequiresReviewReason = ReviewReason.LowConfidence,
            ConfidenceLevel = 50,
            ClassificationAmbiguity = false,
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            HandoffPath = handoffPath,
        };

        await _dbContext.ReviewCases.AddAsync(reviewCase, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return reviewCase;
    }

    // -------------------------------------------------------------------------
    // TC-GC2b-P: SubmitReviewDecisionAsync Approve → publishes ReviewDecisionApprovedEvent
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a reviewer submits an APPROVE decision for a held case,
    /// <see cref="ManualReviewerService.SubmitReviewDecisionAsync"/> MUST publish exactly one
    /// <see cref="ReviewDecisionApprovedEvent"/> via <see cref="IEventPublisher"/>.
    ///
    /// This event is the release signal consumed by <c>ReviewApprovalExportHandler</c> (G-C2b).
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_Approve_PublishesReviewDecisionApprovedEvent()
    {
        // Arrange
        // FileId MUST be a Guid string — ManualReviewerService does Guid.TryParse(reviewCase.FileId)
        // before publishing ReviewDecisionApprovedEvent; a non-Guid string silently skips the publish.
        var fileId = Guid.NewGuid().ToString();
        var caseId = "CASE-GC2B-PUB-001";
        const string handoffPath = "2026/06/20/abc.fusion.json";

        var publishedEvents = new List<DomainEvent>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        await SeedApproveableCaseAsync(fileId, caseId, handoffPath);

        var service = BuildService(eventPublisher);
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-PUB-001",
            CaseId = caseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "reviewer@example.com",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved for export",
        };

        // Act
        var result = await service.SubmitReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken)
            ;

        // Assert: submission succeeded
        result.IsSuccess.ShouldBeTrue();

        // Assert: exactly one ReviewDecisionApprovedEvent published
        var approvalEvents = publishedEvents.OfType<ReviewDecisionApprovedEvent>().ToList();
        approvalEvents.Count.ShouldBe(1, "exactly one ReviewDecisionApprovedEvent must be published on Approve");

        var approvalEvent = approvalEvents[0];
        approvalEvent.CaseId.ShouldBe(caseId);
        approvalEvent.ReviewerId.ShouldBe("reviewer@example.com");
        approvalEvent.HandoffPath.ShouldBe(handoffPath);

        // Assert: total event count is 1 (no extra events)
        publishedEvents.Count.ShouldBe(1, "no extra events must be published");
    }

    // -------------------------------------------------------------------------
    // TC-GC2b-R: SubmitReviewDecisionAsync Reject → does NOT publish any event
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a reviewer REJECTS a case, <see cref="ManualReviewerService.SubmitReviewDecisionAsync"/>
    /// MUST NOT publish any <see cref="ReviewDecisionApprovedEvent"/>.
    ///
    /// Only the Approve decision releases the export; Reject and RequestMoreInfo must be silent.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_Reject_DoesNotPublishEvent()
    {
        // Arrange
        var fileId = "FILE-GC2B-REJ-001";
        var caseId = "CASE-GC2B-REJ-001";

        var publishedEvents = new List<DomainEvent>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        await SeedApproveableCaseAsync(fileId, caseId);

        var service = BuildService(eventPublisher);
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-REJ-001",
            CaseId = caseId,
            DecisionType = DecisionType.Reject,
            ReviewerId = "reviewer@example.com",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Rejected — invalid document",
        };

        // Act
        var result = await service.SubmitReviewDecisionAsync(caseId, decision, TestContext.Current.CancellationToken)
            ;

        // Assert: submission succeeded
        result.IsSuccess.ShouldBeTrue();

        // Assert: NO event published on Reject
        publishedEvents.Count.ShouldBe(0, "no event must be published when a reviewer REJECTS");
        eventPublisher.DidNotReceive().Publish(Arg.Any<DomainEvent>());
    }

    // -------------------------------------------------------------------------
    // TC-GC2b-H: IdentifyReviewCasesAsync with handoffPath → HandoffPath persisted
    // -------------------------------------------------------------------------

    /// <summary>
    /// When <see cref="ManualReviewerService.IdentifyReviewCasesAsync"/> is called with a non-null
    /// <paramref name="handoffPath"/>, the created <see cref="ReviewCase"/> entity must persist
    /// that path in the <see cref="ReviewCase.HandoffPath"/> column.
    ///
    /// Uses EF Core in-memory (EnsureCreated) — satisfies the safety constraint
    /// "Testcontainers/EnsureCreated only".  A real-SQL round-trip is covered by the migration
    /// <c>20260621040718_AddReviewCaseHandoffPath</c>.
    /// </summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithHandoffPath_PersistsHandoffPath()
    {
        // Arrange
        const string fileId = "FILE-GC2B-HP-001";
        const string expectedHandoffPath = "2026/06/20/fused-expediente-abc123.json";

        await _dbContext.FileMetadata.AddAsync(new FileMetadata
        {
            FileId = fileId,
            FileName = "test.pdf",
            FilePath = "/data/test.pdf",
            DownloadTimestamp = DateTime.UtcNow,
            Checksum = "checksum-hp-001",
            FileSize = 2048,
            Format = FileFormat.Pdf,
        }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = BuildService();
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Confidence = 50, // intentionally low so at least one ReviewCase is created
        };

        // Act
        var result = await service.IdentifyReviewCasesAsync(
            fileId, metadata, classification,
            isComplete: true,
            handoffPath: expectedHandoffPath,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: IdentifyReviewCasesAsync succeeded and created at least one case
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThan(0, "at least one review case must be created for low-confidence input");

        // Assert: every persisted ReviewCase for this fileId carries the handoffPath
        var persistedCases = await _dbContext.ReviewCases
            .Where(c => c.FileId == fileId)
            .ToListAsync(TestContext.Current.CancellationToken);

        persistedCases.Count.ShouldBeGreaterThan(0, "at least one ReviewCase must be persisted in the database");
        persistedCases.ShouldAllBe(
            c => c.HandoffPath == expectedHandoffPath,
            "all ReviewCase rows for this fileId must carry the expected handoffPath");
    }
}

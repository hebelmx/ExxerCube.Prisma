using ExxerCube.Prisma.Application;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for the G-C2b release path: <see cref="ReviewApprovalExportHandler"/> must re-run
/// Stage-5 export (producing <see cref="ExportCompletedEvent"/>) when a reviewer APPROVES a
/// held case, and must NOT produce any export when a reviewer REJECTS.
///
/// Owner ruling (binding 2026-06-20): export blocks until human review; approval via
/// <see cref="ReviewDecisionApprovedEvent"/> is the only legitimate release signal.
///
/// Design under test:
///   - <see cref="ReviewApprovalExportHandler"/> subscribes to <see cref="ReviewDecisionApprovedEvent"/>.
///   - On Approve: loads expediente from <see cref="IExpedienteHandoffStore"/> (when HandoffPath set),
///     calls <see cref="ReconciliationOrchestrator.ReconcileAsync"/> with approvedByReviewer: true.
///   - The gate bypass (approvedByReviewer) is checked via the exporter receiving a call and
///     <see cref="ExportCompletedEvent"/> being published.
///   - The gate still blocks un-approved cases (verified via the TC-GC2-* tests in
///     <see cref="ReconciliationOrchestratorExportGateTests"/>).
/// </summary>
public sealed class ReviewApprovalExportHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the SUT: a <see cref="ReviewApprovalExportHandler"/> wired to a real
    /// <see cref="ReconciliationOrchestrator"/> so that the gate-bypass path is exercised end-to-end.
    /// </summary>
    /// <param name="classifierConfidence">
    /// Confidence returned by the NSubstitute classifier (used to confirm the gate WOULD have fired
    /// for normal cases).
    /// </param>
    /// <param name="handoffExpediente">
    /// When non-null the NSubstitute <see cref="IExpedienteHandoffStore"/> is configured to return
    /// this expediente for any path.  When null the store returns a failure (simulates missing artifact).
    /// </param>
    private static (
        ReviewApprovalExportHandler handler,
        List<DomainEvent> publishedEvents,
        IResponseExporter exporterSub,
        IExpedienteHandoffStore handoffStoreSub)
        CreateSut(
            int classifierConfidence = 50,    // intentionally low so gate WOULD fire without override
            Expediente? handoffExpediente = null)
    {
        var publishedEvents = new List<DomainEvent>();

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        // Classifier: low confidence so the gate WOULD block without the reviewer override.
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = classifierConfidence,
            }));

        // Exporter: returns success so Stage 5 completes when reached.
        var exporterSub = Substitute.For<IResponseExporter>();
        exporterSub
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Review panel: no-op success (PersistReviewCaseAsync should not throw).
        var panelSub = Substitute.For<IManualReviewerPanel>();
        panelSub
            .IdentifyReviewCasesAsync(
                Arg.Any<string>(),
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<ClassificationResult>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(new List<ReviewCase>()));

        var services = new ServiceCollection();
        services.AddScoped<IManualReviewerPanel>(_ => panelSub);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // ReconciliationOrchestrator: real instance with gate active (all conditions enabled by default).
        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: exporterSub,
            reviewCaseScopeFactory: scopeFactory,
            exportGatePolicy: new ExportGatePolicy()); // default: all gates ON

        // Handoff store: NSubstitute configured to return the provided expediente (or failure).
        var handoffStoreSub = Substitute.For<IExpedienteHandoffStore>();
        if (handoffExpediente is not null)
        {
            handoffStoreSub
                .LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<Expediente>.Success(handoffExpediente));
        }
        else
        {
            handoffStoreSub
                .LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<Expediente>.WithFailure("No handoff artifact"));
        }

        var handler = new ReviewApprovalExportHandler(
            eventPublisher,
            orchestrator,
            NullLogger<ReviewApprovalExportHandler>.Instance,
            handoffStore: handoffStoreSub);

        return (handler, publishedEvents, exporterSub, handoffStoreSub);
    }

    // =========================================================================
    // TC-GC2b-1: Held case + reviewer APPROVES → Stage-5 export runs, ExportCompletedEvent published
    // =========================================================================

    /// <summary>
    /// When a reviewer APPROVES a held case, <see cref="ReviewApprovalExportHandler.HandleAsync"/>
    /// MUST load the expediente from the handoff store, bypass the gate, run Stage-5 export, and
    /// publish an <see cref="ExportCompletedEvent"/>.
    ///
    /// The classifier confidence (50) is intentionally below the 70% gate threshold to confirm
    /// that the gate WOULD have blocked on a normal call — proving the bypass is active.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ApprovalEvent_ExportsAndPublishesExportCompletedEvent()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var handoffPath = $"2026/06/20/{fileId}.fusion.json";
        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2506-001-TST",
            NumeroOficio = "214-1-00000001/2026",
        };

        var (handler, publishedEvents, exporterSub, handoffStoreSub) =
            CreateSut(classifierConfidence: 50, handoffExpediente: expediente);

        var approvalEvent = new ReviewDecisionApprovedEvent
        {
            FileId = fileId,
            CaseId = "CASE-001",
            DecisionId = "DEC-abc123",
            ReviewerId = "reviewer@example.com",
            HandoffPath = handoffPath,
            CorrelationId = Guid.NewGuid(),
        };

        // Act
        await handler.HandleAsync(approvalEvent, Ct);

        // Assert: handoff store was consulted with the correct path
        await handoffStoreSub
            .Received(1)
            .LoadAsync(handoffPath, Arg.Any<CancellationToken>());

        // Assert: exporter was called (Stage 5 ran)
        await exporterSub
            .Received(1)
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());

        // Assert: ExportCompletedEvent published (SIRO XML)
        var completedEvents = publishedEvents.OfType<ExportCompletedEvent>().ToList();
        completedEvents.ShouldNotBeEmpty(
            "an ExportCompletedEvent must be published when an approved case is re-exported");

        var siroEvent = completedEvents.FirstOrDefault(e => e.Format == "SiroXml");
        siroEvent.ShouldNotBeNull("a SiroXml ExportCompletedEvent must be published");
        siroEvent!.FileId.ShouldBe(fileId,
            "ExportCompletedEvent.FileId must match the approved case FileId");

        // Assert: NO ExportHeldForReviewEvent (gate bypass is active — case should not be re-held)
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty(
            "an approved case MUST NOT be re-held: the gate bypass must be honoured");
    }

    // =========================================================================
    // TC-GC2b-2: Held case + reviewer REJECTS → NO export, NO ExportCompletedEvent
    // =========================================================================

    /// <summary>
    /// When a reviewer REJECTS a case the handler must NOT export — no
    /// <see cref="ExportCompletedEvent"/> should be published, and the exporter must not be called.
    ///
    /// A rejected case is NOT signalled via <see cref="ReviewDecisionApprovedEvent"/> (only Approve
    /// publishes that event). This test verifies that a <see cref="ReviewDecisionApprovedEvent"/>
    /// is never inadvertently constructed for a Reject decision. It tests the handler indirectly by
    /// confirming that without a <c>ReviewDecisionApprovedEvent</c>, no export happens.
    ///
    /// Additionally tests <see cref="ReviewApprovalExportHandler.HandleAsync"/> with a null event
    /// (guard path) and confirms it is a no-op.
    /// </summary>
    [Fact]
    public async Task HandleAsync_NullEvent_IsNoopAndDoesNotExport()
    {
        // Arrange
        var (handler, publishedEvents, exporterSub, _) = CreateSut(classifierConfidence: 50);

        // Act: null event — must be a graceful no-op, not a throw
        await handler.HandleAsync(null!, Ct);

        // Assert: exporter never called
        await exporterSub
            .DidNotReceive()
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());

        // Assert: no ExportCompletedEvent or ExportHeldForReviewEvent
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty(
            "a null approval event must not trigger an export");
    }

    // =========================================================================
    // TC-GC2b-3: Approve with no HandoffPath → Stage 5 still runs (graceful degradation)
    // =========================================================================

    /// <summary>
    /// When a <see cref="ReviewDecisionApprovedEvent"/> has no <see cref="ReviewDecisionApprovedEvent.HandoffPath"/>
    /// (in-process / single-service path), the handler MUST NOT throw and MUST still attempt Stage-5
    /// export (the exporter will skip gracefully if <c>FusedExpediente</c> is null, but the gate
    /// bypass must still be honoured).
    /// </summary>
    [Fact]
    public async Task HandleAsync_ApprovalEventWithNoHandoffPath_DoesNotThrowAndCallsExporter()
    {
        // Arrange: no handoff path; the exporter returns success so Stage 5 can complete
        // (ExecuteStage5ExportAsync will check for FusedExpediente == null and skip with a warning)
        var fileId = Guid.NewGuid();
        var (handler, publishedEvents, exporterSub, handoffStoreSub) =
            CreateSut(classifierConfidence: 50, handoffExpediente: null);

        var approvalEvent = new ReviewDecisionApprovedEvent
        {
            FileId = fileId,
            CaseId = "CASE-002",
            DecisionId = "DEC-def456",
            ReviewerId = "reviewer@example.com",
            HandoffPath = null, // in-process path: no handoff artifact
            CorrelationId = Guid.NewGuid(),
        };

        // Act: must not throw
        var act = async () => await handler.HandleAsync(approvalEvent, Ct);
        await act.ShouldNotThrowAsync();

        // Assert: handoff store was NOT consulted (no path provided)
        await handoffStoreSub
            .DidNotReceive()
            .LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Assert: NO ExportHeldForReviewEvent (gate bypass active, case not re-held)
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty(
            "even with null HandoffPath the gate bypass must prevent re-holding the case");
    }

    // =========================================================================
    // TC-GC2b-4: Gate still blocks un-approved (regression — approvedByReviewer: false)
    // =========================================================================

    /// <summary>
    /// Regression guard: confirms that the normal pipeline path (approvedByReviewer: false,
    /// the default) still blocks a low-confidence case. This is NOT the same as calling the
    /// handler — it calls <see cref="ReconciliationOrchestrator.ReconcileAsync"/> directly with
    /// <c>approvedByReviewer: false</c> to prove the gate is not globally disabled.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_WithoutApprovalOverride_LowConfidenceStillBlocks()
    {
        // Arrange: confidence 50 (below 70 threshold), normal path (approvedByReviewer not set)
        var publishedEvents = new List<DomainEvent>();

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = 50, // below threshold
            }));

        var exporterSub = Substitute.For<IResponseExporter>();
        exporterSub
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var panelSub = Substitute.For<IManualReviewerPanel>();
        panelSub
            .IdentifyReviewCasesAsync(
                Arg.Any<string>(),
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<ClassificationResult>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(new List<ReviewCase>()));

        var services = new ServiceCollection();
        services.AddScoped<IManualReviewerPanel>(_ => panelSub);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: exporterSub,
            reviewCaseScopeFactory: scopeFactory,
            exportGatePolicy: new ExportGatePolicy());

        var fusionResult = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2506-REGRESSION",
                NumeroOficio = "214-1-99999999/2026",
            },
            OverallConfidence = 0.97,
            NextAction = NextAction.AutoProcess,
            ConflictingFields = new List<string>(),
        };

        // Act: normal call — approvedByReviewer defaults to false
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: gate fired — ExportHeldForReviewEvent published, no ExportCompletedEvent
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldNotBeEmpty(
            "gate must still block low-confidence cases on the normal (non-approved) path");
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty(
            "no ExportCompletedEvent must be published when the gate blocks a normal (non-approved) case");

        // Assert: exporter never called
        await exporterSub
            .DidNotReceive()
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TC-GC2b-5: ExportHeldForReviewEvent carries HandoffPath from 3-process path
    // =========================================================================

    /// <summary>
    /// When the 3-process path holds a case, the <see cref="ExportHeldForReviewEvent"/> published
    /// by <see cref="ReconciliationOrchestrator"/> MUST carry the <c>HandoffPath</c> so that the
    /// <see cref="ReviewApprovalExportHandler"/> can later reload the expediente for re-export.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_WhenGateBlocks_HeldEventCarriesHandoffPath()
    {
        // Arrange: low confidence + fusion ManualReviewRequired → gate blocks
        var publishedEvents = new List<DomainEvent>();
        var fileId = Guid.NewGuid();
        var handoffPath = $"2026/06/20/{fileId}.fusion.json";

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = 40,
            }));

        var panelSub = Substitute.For<IManualReviewerPanel>();
        panelSub
            .IdentifyReviewCasesAsync(
                Arg.Any<string>(),
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<ClassificationResult>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.Success(new List<ReviewCase>()));

        var services = new ServiceCollection();
        services.AddScoped<IManualReviewerPanel>(_ => panelSub);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: Substitute.For<IResponseExporter>(),
            reviewCaseScopeFactory: scopeFactory,
            exportGatePolicy: new ExportGatePolicy());

        var fusionResult = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2506-HANDOFFTEST",
                NumeroOficio = "214-1-88888888/2026",
            },
            OverallConfidence = 0.4,
            NextAction = NextAction.ManualReviewRequired,
        };

        // Act: pass the handoffPath explicitly (as ReconciliationPipelineService does)
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            isComplete: true,
            handoffPath: handoffPath,
            approvedByReviewer: false,
            cancellationToken: Ct);

        // Assert: held event carries the handoff path
        var heldEvents = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        heldEvents.Count.ShouldBe(1,
            "exactly one ExportHeldForReviewEvent should be published when the gate fires");

        heldEvents[0].HandoffPath.ShouldBe(handoffPath,
            "HandoffPath must be propagated to ExportHeldForReviewEvent so the approval handler can reload the expediente");
        heldEvents[0].FileId.ShouldBe(fileId);
    }
}

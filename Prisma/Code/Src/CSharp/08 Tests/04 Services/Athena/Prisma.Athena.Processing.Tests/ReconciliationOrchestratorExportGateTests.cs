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
/// Tests the Stage-5 export gate introduced for G-C2 / FR14 / FR20 / INV-6.
/// Owner ruling (binding 2026-06-20): unreviewed / low-confidence / conflicted cases
/// MUST NOT produce regulatory output.
///
/// Policy under test: <see cref="ExportGatePolicy"/> (all three conditions enabled by default).
/// </summary>
/// <remarks>
/// These are pure unit tests.  The exporter is an NSubstitute that would record calls
/// if Stage 5 was reached — we assert it is <em>never</em> called on blocked cases.
/// The event publisher captures published events so we can verify:
///   (a) blocked case  → <see cref="ExportHeldForReviewEvent"/> published, no <see cref="ExportCompletedEvent"/>
///   (b) approved case → <see cref="ExportCompletedEvent"/> published, no <see cref="ExportHeldForReviewEvent"/>
/// </remarks>
public sealed class ReconciliationOrchestratorExportGateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="ReconciliationOrchestrator"/> with:
    /// <list type="bullet">
    ///   <item>A captured event list so we can inspect what was published.</item>
    ///   <item>A real NSubstitute exporter that records successful calls.</item>
    ///   <item>A high-confidence NSubstitute classifier (for cases where Stage 4 should succeed).</item>
    ///   <item>A real <see cref="IManualReviewerPanel"/> NSubstitute (review-case persistence no-op).</item>
    /// </list>
    /// </summary>
    private static (
        ReconciliationOrchestrator orchestrator,
        List<DomainEvent> publishedEvents,
        IResponseExporter exporterSub)
        CreateSut(
            ExportGatePolicy? policy = null,
            int classifierConfidence = 90)
    {
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
                Confidence = Confidence.FromInt(classifierConfidence),
            }));

        // Exporter: returns a successful empty MemoryStream so Stage 5 can complete when reached.
        var exporterSub = Substitute.For<IResponseExporter>();
        exporterSub
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Review panel: no-op success so PersistReviewCaseAsync never throws.
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
            exportGatePolicy: policy ?? new ExportGatePolicy());

        return (orchestrator, publishedEvents, exporterSub);
    }

    // Minimal fused expediente; NextAction defaults to ManualReviewRequired in the domain model.
    private static FusionResult FusionRequiringManualReview(int conflictCount = 0)
    {
        var result = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-001-TST",
                NumeroOficio = "214-1-00000001/2026",
            },
            Confidence = Confidence.FromFusion(0.5),
            NextAction = NextAction.ManualReviewRequired,
        };

        for (var i = 0; i < conflictCount; i++)
        {
            result.ConflictingFields.Add($"Field{i}");
        }

        return result;
    }

    // A "clean" fusion result with AutoProcess and no conflicts.
    private static FusionResult FusionAutoProcess() => new()
    {
        FusedExpediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-002-TST",
            NumeroOficio = "214-1-00000002/2026",
        },
        Confidence = Confidence.FromFusion(0.97),
        NextAction = NextAction.AutoProcess,
        ConflictingFields = new List<string>(), // no conflicts
    };

    // =========================================================================
    // TC-GC2-1: Low-confidence classification blocks export
    // =========================================================================

    /// <summary>
    /// When classification confidence is below the 70% threshold,
    /// Stage 5 MUST NOT run and an <see cref="ExportHeldForReviewEvent"/> MUST be published
    /// instead of an <see cref="ExportCompletedEvent"/>.
    /// This covers the G-C2 low-confidence path (FR14, INV-6).
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_LowConfidenceClassification_BlocksExportAndPublishesHeldEvent()
    {
        // Arrange: confidence = 50 (below threshold 70); fusion is AutoProcess/clean so
        // only the confidence gate fires.
        const int lowConfidence = 50;
        var (orchestrator, publishedEvents, exporterSub) = CreateSut(classifierConfidence: lowConfidence);

        var fileId = Guid.NewGuid();

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionAutoProcess(),
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: ExportHeldForReviewEvent was published
        var heldEvents = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        heldEvents.Count.ShouldBe(1, "exactly one ExportHeldForReviewEvent must be published");

        var held = heldEvents[0];
        held.FileId.ShouldBe(fileId);
        held.BlockReasons.ShouldNotBeEmpty("at least one block reason must be recorded");
        held.ClassificationConfidence.ShouldBe(lowConfidence);
        held.BlockReasons
            .Any(r => r.Contains("BlockOnLowConfidence"))
            .ShouldBeTrue("block reason must identify the low-confidence policy condition");

        // Assert: NO ExportCompletedEvent published
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty(
            "export MUST NOT complete when case is held for review");

        // Assert: the exporter was never called (Stage 5 is skipped)
        await exporterSub
            .DidNotReceive()
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TC-GC2-2: Fusion ManualReviewRequired blocks export
    // =========================================================================

    /// <summary>
    /// When the fusion result has <c>NextAction == ManualReviewRequired</c>,
    /// Stage 5 MUST NOT run even if classification confidence is above the threshold.
    /// This covers the G-C2 fusion-state path (FR20).
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_FusionManualReviewRequired_BlocksExportAndPublishesHeldEvent()
    {
        // Arrange: high-confidence classifier (would pass the confidence gate alone),
        // but fusion says ManualReviewRequired.
        const int highConfidence = 95;
        var (orchestrator, publishedEvents, exporterSub) = CreateSut(classifierConfidence: highConfidence);

        var fileId = Guid.NewGuid();
        var fusionResult = FusionRequiringManualReview(conflictCount: 0);

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: ExportHeldForReviewEvent published
        var heldEvents = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        heldEvents.Count.ShouldBe(1);

        var held = heldEvents[0];
        held.FileId.ShouldBe(fileId);
        held.BlockReasons
            .Any(r => r.Contains("BlockOnFusionManualReviewRequired"))
            .ShouldBeTrue("block reason must identify the fusion-state policy condition");

        // Assert: NO ExportCompletedEvent
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty();

        // Assert: exporter never called
        await exporterSub
            .DidNotReceive()
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TC-GC2-3: Unresolved conflicts block export
    // =========================================================================

    /// <summary>
    /// When the fusion result has unresolved field conflicts (non-empty <c>ConflictingFields</c>),
    /// Stage 5 MUST NOT run even if confidence and NextAction would otherwise permit it.
    /// This covers the G-C2 conflict path (INV-6).
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_UnresolvedFusionConflicts_BlocksExportAndPublishesHeldEvent()
    {
        // Arrange: high confidence, but conflicting fields present.
        const int highConfidence = 95;
        var (orchestrator, publishedEvents, exporterSub) = CreateSut(classifierConfidence: highConfidence);

        var fileId = Guid.NewGuid();

        // Fusion: AutoProcess NextAction but has conflicts (contradicts real domain logic,
        // but the gate must be independently defensive).
        var fusionWithConflicts = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-003-TST",
                NumeroOficio = "214-1-00000003/2026",
            },
            Confidence = Confidence.FromFusion(0.90),
            NextAction = NextAction.AutoProcess,
            ConflictingFields = new List<string> { "RFC", "CURP" },
        };

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionWithConflicts,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: ExportHeldForReviewEvent published
        var heldEvents = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        heldEvents.Count.ShouldBe(1);

        var held = heldEvents[0];
        held.FileId.ShouldBe(fileId);
        held.BlockReasons
            .Any(r => r.Contains("BlockOnUnresolvedConflicts"))
            .ShouldBeTrue("block reason must identify the conflict policy condition");

        // Assert: NO ExportCompletedEvent
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty();

        // Assert: exporter never called
        await exporterSub
            .DidNotReceive()
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TC-GC2-4: High-confidence / AutoProcess / no conflicts → export proceeds
    // =========================================================================

    /// <summary>
    /// When confidence is above the threshold, fusion is <c>AutoProcess</c>, and there are no
    /// unresolved conflicts, Stage 5 MUST run and an <see cref="ExportCompletedEvent"/> MUST
    /// be published.  No <see cref="ExportHeldForReviewEvent"/> should be emitted.
    /// This is the "approved case proceeds to export" path required by G-C2.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_HighConfidenceAutoProcessNoConflicts_ExportsAndPublishesCompletedEvent()
    {
        // Arrange: confidence 90 (above 70 threshold), AutoProcess, no conflicts.
        const int highConfidence = 90;
        var (orchestrator, publishedEvents, exporterSub) = CreateSut(classifierConfidence: highConfidence);

        var fileId = Guid.NewGuid();

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionAutoProcess(),
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: ExportCompletedEvent published (SIRO XML)
        var completedEvents = publishedEvents.OfType<ExportCompletedEvent>().ToList();
        completedEvents.ShouldNotBeEmpty("an ExportCompletedEvent must be published for high-confidence AutoProcess cases");

        var siroEvent = completedEvents.FirstOrDefault(e => e.Format == "SiroXml");
        siroEvent.ShouldNotBeNull("a SiroXml ExportCompletedEvent must be published");
        siroEvent!.FileId.ShouldBe(fileId);

        // Assert: NO ExportHeldForReviewEvent
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty(
            "a high-confidence AutoProcess case must NOT be held for review");

        // Assert: exporter was called (Stage 5 ran)
        await exporterSub
            .Received(1)
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TC-GC2-5: Gate disabled via policy → export proceeds even at low confidence
    // =========================================================================

    /// <summary>
    /// Verifies that the gate is policy-controlled: when <see cref="ExportGatePolicy.BlockOnLowConfidence"/>
    /// is <see langword="false"/>, low-confidence cases are NOT blocked.
    /// This tests the escape hatch for environments where the gate should be soft.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_GateDisabledByPolicy_LowConfidenceDoesNotBlockExport()
    {
        // Arrange: confidence = 40 (way below threshold), but gate is disabled.
        var permissivePolicy = new ExportGatePolicy
        {
            BlockOnLowConfidence = false,
            BlockOnFusionManualReviewRequired = false,
            BlockOnUnresolvedConflicts = false,
            BlockOnLowAggregateConfidence = false, // Story 2.7: the aggregate sub-gate is part of the policy escape hatch
        };

        var (orchestrator, publishedEvents, exporterSub) =
            CreateSut(policy: permissivePolicy, classifierConfidence: 40);

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionAutoProcess(),
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        // Assert: export completed (gate was off)
        publishedEvents.OfType<ExportCompletedEvent>().ShouldNotBeEmpty();
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty();
        await exporterSub
            .Received(1)
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TC-GC2-6: All three gate conditions fire simultaneously
    // =========================================================================

    /// <summary>
    /// When all gate conditions are active simultaneously (low classification confidence + low weighted
    /// aggregate + ManualReviewRequired + conflicts), the <see cref="ExportHeldForReviewEvent.BlockReasons"/>
    /// list must contain an entry for every condition. Verifies that gate evaluation is NOT short-circuited.
    /// Since Story 2.7 the weighted document-confidence aggregate (gate 1b) is a fourth active condition:
    /// classification 0.30 with a low-confidence fusion result aggregates well below 0.65.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_AllGateConditionsActive_BlockReasonsContainsEveryCondition()
    {
        // Arrange: confidence 30 (below threshold) + ManualReviewRequired + 2 conflicts
        const int veryLowConfidence = 30;
        var (orchestrator, publishedEvents, _) = CreateSut(classifierConfidence: veryLowConfidence);

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionRequiringManualReview(conflictCount: 2),
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        // Assert: one held event with every active reason represented
        var heldEvents = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        heldEvents.Count.ShouldBe(1);

        var reasons = heldEvents[0].BlockReasons;
        reasons.Count.ShouldBe(4, "all four active gate conditions must produce a reason entry");

        reasons.ShouldContain(r => r.Contains("BlockOnLowConfidence"),
            "low-confidence reason must be present");
        reasons.ShouldContain(r => r.Contains("BlockOnLowAggregateConfidence"),
            "low-aggregate reason must be present (Story 2.7)");
        reasons.ShouldContain(r => r.Contains("BlockOnFusionManualReviewRequired"),
            "fusion-state reason must be present");
        reasons.ShouldContain(r => r.Contains("BlockOnUnresolvedConflicts"),
            "conflict reason must be present");
    }

    // =========================================================================
    // Story 2.7 — weighted document-confidence aggregate (gate 1b)
    // =========================================================================

    /// <summary>
    /// Story 2.7 core behaviour: a document whose CLASSIFICATION confidence passes gate 1 (0.75 ≥ 0.70)
    /// is still held when the OCR and fusion evidence is weak enough that the weighted aggregate falls
    /// below 0.65. This proves OCR + fusion confidence now influence the export decision (they were
    /// previously discarded). OCR confidence reaches the gate via the handoff Expediente.OcrConfidence.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_HighClassificationButLowOcrAndFusion_BlocksOnAggregate()
    {
        var (orchestrator, publishedEvents, exporterSub) = CreateSut(classifierConfidence: 75);

        var weakEvidenceFusion = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-003-TST",
                NumeroOficio = "214-1-00000003/2026",
                OcrConfidence = Confidence.FromOcr(40), // 0.40 — poor OCR
            },
            Confidence = Confidence.FromFusion(0.50),   // weak fusion
            NextAction = NextAction.AutoProcess,        // gates 2/3 open
            ConflictingFields = new List<string>(),
        };

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: weakEvidenceFusion,
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        // aggregate = (0.75*0.50 + 0.40*0.30 + 0.50*0.20) / 1.0 = 0.595 < 0.65 → held, gate 1 (0.75) passes.
        var held = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        held.Count.ShouldBe(1);
        held[0].BlockReasons.ShouldContain(r => r.Contains("BlockOnLowAggregateConfidence"),
            "weak OCR + fusion must trip the aggregate gate even though classification (0.75) passed");
        held[0].BlockReasons.ShouldNotContain(r => r.Contains("BlockOnLowConfidence"),
            "classification at 0.75 must NOT trip gate 1");
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty();
        await exporterSub.DidNotReceive().ExportSiroXmlAsync(
            Arg.Any<UnifiedMetadataRecord>(), Arg.Any<System.IO.Stream>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Counterpart: when classification, OCR and fusion are all strong the weighted aggregate clears
    /// 0.65 and the document auto-exports — the aggregate gate does not over-block healthy documents.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_AllSignalsStrong_AggregatePasses_Exports()
    {
        var (orchestrator, publishedEvents, exporterSub) = CreateSut(classifierConfidence: 90);

        var strongFusion = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-004-TST",
                NumeroOficio = "214-1-00000004/2026",
                OcrConfidence = Confidence.FromOcr(95), // 0.95
            },
            Confidence = Confidence.FromFusion(0.90),
            NextAction = NextAction.AutoProcess,
            ConflictingFields = new List<string>(),
        };

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: strongFusion,
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        // aggregate = (0.90*0.50 + 0.95*0.30 + 0.90*0.20) / 1.0 = 0.915 ≥ 0.65 → export.
        publishedEvents.OfType<ExportCompletedEvent>().ShouldNotBeEmpty();
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty();
        await exporterSub.Received(1).ExportSiroXmlAsync(
            Arg.Any<UnifiedMetadataRecord>(), Arg.Any<System.IO.Stream>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Story 2.8 — unit test: ExportGatePolicy default thresholds
    // =========================================================================

    /// <summary>
    /// A default-constructed <see cref="ExportGatePolicy"/> must expose exactly the documented
    /// threshold values so runtime behaviour matches the specification without any configuration.
    /// </summary>
    [Fact]
    public void ExportGatePolicy_DefaultConstructed_HasExpectedThresholds()
    {
        var policy = new ExportGatePolicy();

        policy.ClassificationConfidenceThreshold.ShouldBe(0.70,
            "default ClassificationConfidenceThreshold must be 0.70 (70%)");
        policy.ManualReviewThreshold.ShouldBe(0.80,
            "default ManualReviewThreshold must be 0.80 (80%)");

        // Boolean defaults (unchanged from pre-2.8 behaviour)
        policy.BlockOnLowConfidence.ShouldBeTrue();
        policy.BlockOnFusionManualReviewRequired.ShouldBeTrue();
        policy.BlockOnUnresolvedConflicts.ShouldBeTrue();
    }

    // =========================================================================
    // Story 2.8 — integration test: non-default policy lowers the gate threshold
    // =========================================================================

    /// <summary>
    /// When a non-default <see cref="ExportGatePolicy"/> is injected (threshold 0.50 = 50%),
    /// a document whose classification confidence is 0.60 (60 on the 0–100 int scale)
    /// must PASS the gate and produce an <see cref="ExportCompletedEvent"/>.
    /// This verifies that the threshold is truly read from the injected policy and not
    /// from a hardcoded constant.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_NonDefaultPolicy_LoweredThreshold_DocumentWithSufficientConfidencePasses()
    {
        // Arrange: custom policy with a lower 50% threshold.
        // confidence=60 > 50 → gate must NOT block on confidence.
        // Fusion is AutoProcess with no conflicts → other gates also clear.
        const int classificationConfidence = 60; // 0–100 int scale
        var customPolicy = new ExportGatePolicy
        {
            BlockOnLowConfidence = true,
            BlockOnFusionManualReviewRequired = true,
            BlockOnUnresolvedConflicts = true,
            ClassificationConfidenceThreshold = 0.50,  // 50% — lower than the default 70%
            ManualReviewThreshold = 0.80,
        };
        var (orchestrator, publishedEvents, exporterSub) =
            CreateSut(policy: customPolicy, classifierConfidence: classificationConfidence);

        // Act
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: FusionAutoProcess(),
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        // Assert: Stage 5 ran — export event published, no held-for-review event.
        publishedEvents.OfType<ExportCompletedEvent>().ShouldNotBeEmpty(
            "confidence 60 > threshold 50: export gate must pass and Stage 5 must run");
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty(
            "no gate condition should fire when confidence exceeds the injected threshold");
        await exporterSub
            .Received(1)
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>());
    }
}

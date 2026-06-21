using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;
using Prisma.Athena.Processing.Reconciliation;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for <see cref="ReconciliationPipelineService"/> (MVP-PATH 1.4 Reconciliator edge): on a handoff event
/// it loads the fused expediente from shared storage, runs Classification → Export, and emits the terminal
/// completion event — and fails closed when the handoff cannot be loaded.
/// </summary>
public sealed class ReconciliationPipelineServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ExtractionCompletedEvent CreateEvent(Guid? fileId = null, Guid? correlationId = null) => new()
    {
        FileId = fileId ?? Guid.NewGuid(),
        CorrelationId = correlationId ?? Guid.NewGuid(),
        Path = "2026/06/12/doc.fusion.json",
    };

    private static (ReconciliationPipelineService sut, IExpedienteHandoffStore store, IFileClassifier classifier,
        IResponseExporter exporter, IEventPublisher eventPublisher) CreateSut()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
                Confidence = 95
            }));

        // Stage 5 now calls ExportSiroXmlAsync — return Success so Stage 5 completes.
        var exporter = Substitute.For<IResponseExporter>();
        exporter.ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher, NullLogger<ReconciliationOrchestrator>.Instance, classifier, exporter);

        var store = Substitute.For<IExpedienteHandoffStore>();

        var sut = new ReconciliationPipelineService(
            eventPublisher, orchestrator, store, NullLogger<ReconciliationPipelineService>.Instance);
        return (sut, store, classifier, exporter, eventPublisher);
    }

    [Fact]
    public async Task ProcessAsync_LoadsHandoffAndReconciles_EmitsCompletion()
    {
        var fileId = Guid.NewGuid();
        var (sut, store, classifier, exporter, eventPublisher) = CreateSut();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.Success(new Expediente
            {
                NumeroExpediente = "EXP-1",
                NumeroOficio = "OF-1"
            }));

        var result = await sut.ProcessAsync(CreateEvent(fileId: fileId), Ct);

        result.IsSuccess.ShouldBeTrue();
        await classifier.Received(1).ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>());
        await exporter.Received(1).ExportSiroXmlAsync(
            Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        eventPublisher.Received(1).Publish(
            Arg.Is<DocumentProcessingCompletedEvent>(e => e.FileId == fileId && e.AutoProcessed));
    }

    [Fact]
    public async Task ProcessAsync_HandoffLoadFails_ReturnsFailure_NoReconcileNoCompletion()
    {
        var (sut, store, classifier, _, eventPublisher) = CreateSut();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.WithFailure("missing artifact"));

        var result = await sut.ProcessAsync(CreateEvent(), Ct);

        result.IsFailure.ShouldBeTrue();
        await classifier.DidNotReceive().ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>());
        eventPublisher.DidNotReceive().Publish(Arg.Any<DocumentProcessingCompletedEvent>());
    }

    [Fact]
    public async Task ProcessAsync_NullEvent_ReturnsFailure()
    {
        var (sut, _, _, _, _) = CreateSut();

        var result = await sut.ProcessAsync(null!, Ct);

        result.IsFailure.ShouldBeTrue();
    }

    // =========================================================================
    // TC-GC2c: 3-process path honours fusion decision across process boundary
    // =========================================================================

    /// <summary>
    /// When the Extractor stamps <see cref="ExtractionCompletedEvent.RequiresManualReview"/>=true
    /// (and/or <see cref="ExtractionCompletedEvent.ConflictsDetected"/> &gt; 0),
    /// <see cref="ReconciliationPipelineService.ProcessAsync"/> MUST NOT emit an
    /// <see cref="ExportCompletedEvent"/>; instead it MUST emit an
    /// <see cref="ExportHeldForReviewEvent"/> (gate fires in the Reconciliator actor).
    ///
    /// This is the regression guard for the G-C2c staging blocker: before the fix, the
    /// pipeline fabricated a <c>NextAction.AutoProcess</c> stub, making Gates 2 and 3 dead
    /// code — conflicted/manual-review documents were exported silently.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RequiresManualReview_DoesNotExport_PublishesHeldEvent()
    {
        // Arrange: capture all published events.
        var publishedEvents = new List<DomainEvent>();

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        // Classifier returns high confidence — only the fusion gate (Gate 2) fires.
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
                Confidence = 95,
            }));

        // Exporter would record calls if Stage 5 were (incorrectly) reached.
        var exporter = Substitute.For<IResponseExporter>();
        exporter.ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: classifier,
            exporter: exporter);

        // Handoff store: returns a valid expediente so the load path succeeds.
        var store = Substitute.For<IExpedienteHandoffStore>();
        store.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Expediente>.Success(new Expediente
            {
                NumeroExpediente = "A/AS1-2505-C2C-TST",
                NumeroOficio = "214-1-99999999/2026",
            }));

        var sut = new ReconciliationPipelineService(
            eventPublisher, orchestrator, store, NullLogger<ReconciliationPipelineService>.Instance);

        var fileId = Guid.NewGuid();

        // ExtractionCompletedEvent carries RequiresManualReview=true (and a conflict count > 0
        // to exercise Gate 3 as well) — this is what the Extractor now stamps when fusion
        // yields NextAction.ManualReviewRequired.
        var handoffEvent = new ExtractionCompletedEvent
        {
            FileId = fileId,
            CorrelationId = Guid.NewGuid(),
            Path = "2026/06/12/conflict-case.fusion.json",
            RequiresManualReview = true,
            ConflictsDetected = 2,
            IsComplete = true,
        };

        // Act
        var result = await sut.ProcessAsync(handoffEvent, Ct);

        // Assert: pipeline succeeded (loaded the handoff, ran reconciliation path) — not a pipeline error.
        result.IsSuccess.ShouldBeTrue("the reconciliation pipeline itself must succeed even when export is blocked");

        // Assert: Stage 5 must not run — no ExportCompletedEvent published.
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty(
            "a fusion-conflicted/manual-review case MUST NOT produce regulatory output in the 3-process path");

        // Assert: the export gate must have fired — ExportHeldForReviewEvent published.
        var heldEvents = publishedEvents.OfType<ExportHeldForReviewEvent>().ToList();
        heldEvents.Count.ShouldBe(1, "exactly one ExportHeldForReviewEvent must be published when the gate blocks");

        var held = heldEvents[0];
        held.FileId.ShouldBe(fileId);
        held.BlockReasons.ShouldNotBeEmpty("at least one block reason must be recorded");

        // Gate 2 (ManualReviewRequired) must appear in the reasons.
        held.BlockReasons
            .ShouldContain(r => r.Contains("BlockOnFusionManualReviewRequired"),
                "block reason must identify the fusion-state gate (Gate 2)");

        // Gate 3 (conflicts) must also appear — ConflictsDetected=2 was stamped on the event.
        held.BlockReasons
            .ShouldContain(r => r.Contains("BlockOnUnresolvedConflicts"),
                "block reason must identify the conflict gate (Gate 3) when conflicts were detected");

        // Assert: the exporter was never invoked.
        await exporter
            .DidNotReceive()
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>());
    }
}

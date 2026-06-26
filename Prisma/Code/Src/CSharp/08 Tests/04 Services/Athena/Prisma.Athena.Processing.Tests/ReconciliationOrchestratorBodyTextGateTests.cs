using ExxerCube.Prisma.Application;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Classification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Story 2.1b gate-path proof: verifies that when <see cref="ReconciliationOrchestrator.ReconcileAsync"/>
/// is called with <c>ocrResult: null</c> (the 3-process path) but the fused
/// <see cref="Expediente.BodyText"/> contains an ASEGURAMIENTO keyword, the REAL
/// <see cref="FileClassifierService"/> scores confidence ≥ 70 and the export gate passes
/// (i.e. <see cref="ExportCompletedEvent"/> is published and
/// <see cref="ExportHeldForReviewEvent"/> is NOT published).
///
/// This is the minimal reproducible proof of the blocker identified in the §2 max-fidelity gate:
/// without the <c>ocrResult?.Text ?? FusedExpediente?.BodyText</c> fallback the classifier
/// receives an empty <c>LegalReferences</c> → every category scores 10 → Unknown, 0 confidence
/// → the BlockOnLowConfidence gate fires → no export.
/// </summary>
public sealed class ReconciliationOrchestratorBodyTextGateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Builds a <see cref="ReconciliationOrchestrator"/> wired with the REAL
    /// <see cref="FileClassifierService"/> so keyword scoring is exercised end-to-end.
    /// The exporter is a no-op substitute; the event publisher captures published events.
    /// </summary>
    private static (ReconciliationOrchestrator orchestrator, List<DomainEvent> publishedEvents)
        CreateSutWithRealClassifier()
    {
        var publishedEvents = new List<DomainEvent>();

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher
            .When(p => p.Publish(Arg.Any<DomainEvent>()))
            .Do(call => publishedEvents.Add(call.Arg<DomainEvent>()));

        // Real classifier — not a substitute — so keyword scoring actually runs.
        var realClassifier = new FileClassifierService(NullLogger<FileClassifierService>.Instance);

        // Exporter: returns success so Stage 5 can complete when the gate allows it.
        var exporterSub = Substitute.For<IResponseExporter>();
        exporterSub
            .ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<System.IO.Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Review panel: no-op so persistence never blocks.
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
            classifier: realClassifier,
            exporter: exporterSub,
            reviewCaseScopeFactory: scopeFactory,
            exportGatePolicy: new ExportGatePolicy());

        return (orchestrator, publishedEvents);
    }

    // =========================================================================
    // TC-2.1b-GATE: BodyText carries ASEGURAMIENTO → confidence ≥ 70 → export passes
    // =========================================================================

    /// <summary>
    /// Gate-path proof (Story 2.1b): with <c>ocrResult == null</c> and
    /// <c>FusedExpediente.BodyText</c> containing "ASEGURAMIENTO", the real classifier must
    /// score confidence ≥ 70 (expected: 90) and Level1 == Aseguramiento so the export gate does
    /// NOT fire BlockOnLowConfidence and Stage 5 runs — proving the blocker is fixed.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_OcrResultNull_BodyTextHasAseguramiento_ExportGatePasses()
    {
        // Arrange
        var (orchestrator, publishedEvents) = CreateSutWithRealClassifier();

        var fusionResult = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "EXP-GATE-TST",
                // BodyText carries the OCR body from the Extractor process — no ocrResult here.
                BodyText = "Este oficio notifica ASEGURAMIENTO de bienes por disposición judicial."
            },
            OverallConfidence = 0.90,
            NextAction = NextAction.AutoProcess,
            ConflictingFields = new List<string>(), // no conflicts → Gate 3 passes
        };

        var fileId = Guid.NewGuid();

        // Act — ocrResult is null, replicating the Reconciliator process path.
        var stagesCompleted = await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: Stage 4 + Stage 5 both ran (2 stages).
        stagesCompleted.ShouldBe(2,
            "Stage 4 (classification) and Stage 5 (export) must both complete when " +
            "confidence ≥ 70 and no gate condition fires");

        // Assert: export completed — BodyText signal reached the classifier.
        var completedEvents = publishedEvents.OfType<ExportCompletedEvent>().ToList();
        completedEvents.Count.ShouldBe(1,
            "ExportCompletedEvent must be published when the gate passes");
        completedEvents[0].FileId.ShouldBe(fileId);

        // Assert: export was NOT held — BlockOnLowConfidence did NOT fire.
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldBeEmpty(
            "ExportHeldForReviewEvent must NOT be published when BodyText carries the ASEGURAMIENTO signal");
    }

    // =========================================================================
    // TC-2.1b-BASELINE: No BodyText + ocrResult null → Unknown/0 → gate blocks
    // =========================================================================

    /// <summary>
    /// Baseline (regression): with <c>ocrResult == null</c> AND <c>BodyText == null</c> the
    /// classifier sees no signal → confidence 0 → BlockOnLowConfidence fires → export is held.
    /// This was the pre-2.1b blocker and must remain the behaviour for the no-signal case.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_OcrResultNull_BodyTextNull_GateBlocksExport()
    {
        // Arrange
        var (orchestrator, publishedEvents) = CreateSutWithRealClassifier();

        var fusionResult = new FusionResult
        {
            FusedExpediente = new Expediente
            {
                NumeroExpediente = "EXP-NOSIGNAL-TST",
                BodyText = null   // no OCR body — classifier sees empty LegalReferences
            },
            OverallConfidence = 0.90,
            NextAction = NextAction.AutoProcess,
            ConflictingFields = new List<string>(),
        };

        // Act
        var stagesCompleted = await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Assert: Stage 4 ran but Stage 5 was blocked (1 stage, not 2).
        stagesCompleted.ShouldBe(1,
            "Stage 5 must be blocked when there is no classification signal (confidence 0)");

        // Assert: export was held for review.
        publishedEvents.OfType<ExportHeldForReviewEvent>().ShouldNotBeEmpty(
            "ExportHeldForReviewEvent must be published when confidence is 0 (no signal)");

        // Assert: export did NOT complete.
        publishedEvents.OfType<ExportCompletedEvent>().ShouldBeEmpty(
            "ExportCompletedEvent must NOT be published when the gate blocks");
    }
}

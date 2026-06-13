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
        IAdaptiveExporter exporter, IEventPublisher eventPublisher) CreateSut()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ExxerCube.Prisma.Domain.Enum.ClassificationLevel1.Aseguramiento,
                Confidence = 95
            }));

        var exporter = Substitute.For<IAdaptiveExporter>();
        exporter.ExportAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 0x50, 0x4B, 0x03, 0x04 }));

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
            .Returns(Result<Expediente>.Success(new Expediente { NumeroExpediente = "EXP-1" }));

        var result = await sut.ProcessAsync(CreateEvent(fileId: fileId), Ct);

        result.IsSuccess.ShouldBeTrue();
        await classifier.Received(1).ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>());
        await exporter.Received(1).ExportAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
}

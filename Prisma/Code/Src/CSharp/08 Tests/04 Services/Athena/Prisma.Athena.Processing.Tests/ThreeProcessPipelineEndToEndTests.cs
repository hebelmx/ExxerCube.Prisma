using System.Reactive.Linq;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prisma.Athena.Processing;
using Prisma.Athena.Processing.Ingestion;
using Prisma.Athena.Processing.Reconciliation;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// End-to-end proof of the MVP-PATH 1.4 three-process split (ADR-011, DoD A4): a document flows across all
/// three actors — Downloader (Orion) → Extractor (Athena) → Reconciliator — with the two cross-process edges
/// modelled by their real forwarders/bridges (the real SignalR wire of each edge is proven separately by the
/// two HubWireTests), and the Extractor→Reconciliator handoff carried as a shared-storage <em>reference</em>
/// through a real <see cref="FileSystemExpedienteHandoffStore"/> on a shared volume.
/// </summary>
/// <remarks>
/// Two independent <see cref="EventPublisher"/> instances model the two process boundaries (the Extractor and
/// the Reconciliator subscribe to their own streams). Only the leaf adapters (loader, quality, OCR, fusion,
/// classifier, exporter) are mocked, so the assertion is the real cross-actor data flow: the fused expediente
/// is persisted by the Extractor and loaded by the Reconciliator, which classifies, exports, and emits the
/// terminal completion event.
/// </remarks>
public sealed class ThreeProcessPipelineEndToEndTests : IDisposable
{
    private readonly EventPublisher _extractorPublisher = new(NullLogger<EventPublisher>.Instance);
    private readonly EventPublisher _reconciliatorPublisher = new(NullLogger<EventPublisher>.Instance);
    private readonly string _sharedBaseDir = Path.Combine(Path.GetTempPath(), "prisma-3proc-e2e-" + Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DocumentFlowsDownloaderToExtractorToReconciliator_AcrossAllThreeProcesses()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        // ---- Shared storage volume (one base both processes mount) ----
        var storageResolver = new SharedStoragePathResolver(
            Options.Create(new StorageOptions { BasePath = _sharedBaseDir }),
            NullLogger<SharedStoragePathResolver>.Instance);
        var extractorHandoffStore = new FileSystemExpedienteHandoffStore(
            storageResolver, NullLogger<FileSystemExpedienteHandoffStore>.Instance);
        var reconciliatorHandoffStore = new FileSystemExpedienteHandoffStore(
            storageResolver, NullLogger<FileSystemExpedienteHandoffStore>.Instance);

        // ---- Extractor (Athena) leaf adapters: produce a fused expediente ----
        var fileLoader = Substitute.For<IFileLoader>();
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(new ImageData(new byte[] { 1, 2, 3 }, "doc.pdf")));

        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ImageQualityLevel.Pristine,
                Confidence = Confidence.FromQuality(0.95),
            }));

        var ocrExecutor = Substitute.For<IOcrExecutor>();
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(new OCRResult("Extracted text", 92.5f, 93.0f, new List<float> { 90, 95 }, "spa")));

        var fusionService = Substitute.For<IFusionExpediente>();
        fusionService.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(new FusionResult
            {
                Confidence = Confidence.FromFusion(0.92),
                ConflictingFields = new List<string>(),
                NextAction = NextAction.AutoProcess, // G-C2: AutoProcess so export gate allows Stage 5
                // NumeroOficio is required by SiroXmlExporter.ValidateMetadata(); include it so Stage 5 succeeds.
                FusedExpediente = new Expediente
                {
                    NumeroExpediente = "EXP-3PROC-E2E",
                    NumeroOficio = "OF-3PROC-E2E",
                },
            }));

        var extractionOrchestrator = new ExtractionOrchestrator(
            _extractorPublisher, NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer, ocrExecutor: ocrExecutor,
            fusionService: fusionService, fileLoader: fileLoader);

        // ---- Reconciliator leaf adapters: classify + export ----
        var classifier = Substitute.For<IFileClassifier>();
        classifier.ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClassificationResult>.Success(new ClassificationResult
            {
                Level1 = ClassificationLevel1.Aseguramiento,
                Confidence = Confidence.FromInt(95),
            }));

        // Stage 5 now uses IResponseExporter.ExportSiroXmlAsync (MVP-PATH #8).
        UnifiedMetadataRecord? exportedMetadata = null;
        var exporter = Substitute.For<IResponseExporter>();
        exporter.ExportSiroXmlAsync(
                Arg.Any<UnifiedMetadataRecord>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                exportedMetadata = callInfo.ArgAt<UnifiedMetadataRecord>(0);
                return Task.FromResult(Result.Success());
            });

        var reconciliationOrchestrator = new ReconciliationOrchestrator(
            _reconciliatorPublisher, NullLogger<ReconciliationOrchestrator>.Instance, classifier, exporter);
        var reconciliationPipeline = new ReconciliationPipelineService(
            _reconciliatorPublisher, reconciliationOrchestrator, reconciliatorHandoffStore,
            NullLogger<ReconciliationPipelineService>.Instance);
        await reconciliationPipeline.StartAsync(ct);

        // The reconciliation forwarder republishes the received handoff onto the Reconciliator's local stream.
        var reconciliationTokenService = Substitute.For<IProcessClearanceTokenService>();
        reconciliationTokenService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
            {
                ActorId = "athena-extractor",
                ActorType = SiaraActorType.ServiceAccount,
                Clearance = ProcessClearance.Extract,
                FileId = fileId, // matched against the event's FileId by the forwarder
                Jti = Guid.NewGuid().ToString("D"),
            }));

        var reconciliationForwarder = new ReconciliationEventForwarder(
            _reconciliatorPublisher, reconciliationTokenService, NullLogger<ReconciliationEventForwarder>.Instance,
            new InMemoryClearanceReplayGuard());

        // ---- Edge 2 (Extractor → Reconciliator): a hub stub bridges the broadcast to the Reconciliator ----
        var reconciliationHub = Substitute.For<IExxerHub<ExtractionCompletedEvent>>();
        reconciliationHub.SendToAllAsync(Arg.Any<ExtractionCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var evt = callInfo.Arg<ExtractionCompletedEvent>();
                // Stamp a clearance token so the forwarder's validation passes.
                var stamped = evt with { ClearanceToken = "e2e-extract-token" };
                await reconciliationForwarder.ForwardAsync(stamped, callInfo.ArgAt<CancellationToken>(1))
                    .ConfigureAwait(false);
                return Result.Success();
            });

        var extractionPipeline = new ExtractionPipelineService(
            _extractorPublisher, extractionOrchestrator, extractorHandoffStore, reconciliationHub,
            NullLogger<ExtractionPipelineService>.Instance);
        await extractionPipeline.StartAsync(ct);

        // Terminal signal: the Reconciliator emits DocumentProcessingCompletedEvent at the end.
        var pipelineComplete = new TaskCompletionSource<DocumentProcessingCompletedEvent>();
        using var completionSub = _reconciliatorPublisher.GetEventStream<DocumentProcessingCompletedEvent>()
            .Subscribe(e => pipelineComplete.TrySetResult(e));

        // ---- Edge 1 (Downloader → Extractor): the ingestion forwarder republishes onto the Extractor stream ----
        var ingestionTokenService = Substitute.For<IProcessClearanceTokenService>();
        ingestionTokenService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
            {
                ActorId = "orion-downloader",
                ActorType = SiaraActorType.ServiceAccount,
                Clearance = ProcessClearance.Download,
                FileId = fileId,
                Jti = Guid.NewGuid().ToString("D"),
            }));

        var ingestionForwarder = new IngestionEventForwarder(
            _extractorPublisher, storageResolver, ingestionTokenService, NullLogger<IngestionEventForwarder>.Instance,
            new InMemoryClearanceReplayGuard());

        // Act — a document is downloaded (Orion) and crosses into the Extractor.
        await ingestionForwarder.ForwardAsync(new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            FileId = fileId,
            FileName = "doc.pdf",
            Path = "2026/06/12/doc.pdf",
            Source = "SIARA",
            FileSizeBytes = 2048,
            Format = FileFormat.Pdf,
            ClearanceToken = "e2e-download-token",
        }, ct);

        // Assert — the document flowed across all three actors and the Reconciliator completed it.
        var completed = await Task.WhenAny(pipelineComplete.Task, Task.Delay(TimeSpan.FromSeconds(10), ct));
        completed.ShouldBe(pipelineComplete.Task, "the document should flow across all three processes within 10s");

        var completionEvent = await pipelineComplete.Task;
        completionEvent.FileId.ShouldBe(fileId);
        completionEvent.CorrelationId.ShouldBe(correlationId);

        // The Reconciliator classified + exported (SIRO XML) the expediente the Extractor handed off through shared storage.
        await classifier.Received(1).ClassifyAsync(Arg.Any<ExtractedMetadata>(), Arg.Any<CancellationToken>());
        await exporter.Received(1).ExportSiroXmlAsync(
            Arg.Any<UnifiedMetadataRecord>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        exportedMetadata.ShouldNotBeNull();
        exportedMetadata!.Expediente.ShouldNotBeNull();
        exportedMetadata.Expediente!.NumeroExpediente.ShouldBe("EXP-3PROC-E2E");

        // The handoff artifact really crossed the shared volume (a file was written by the Extractor).
        Directory.Exists(_sharedBaseDir).ShouldBeTrue();
        Directory.GetFiles(_sharedBaseDir, "*.fusion.json", SearchOption.AllDirectories).Length.ShouldBe(1);
    }

    public void Dispose()
    {
        _extractorPublisher.Dispose();
        _reconciliatorPublisher.Dispose();
        try
        {
            if (Directory.Exists(_sharedBaseDir))
            {
                Directory.Delete(_sharedBaseDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}

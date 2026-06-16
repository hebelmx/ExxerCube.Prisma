using System.Reactive.Linq;
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

namespace ExxerCube.Prisma.Athena.Processing.Tests;

file static class TokenServiceHelper
{
    public static IProcessClearanceTokenService AcceptingDownload(Guid fileId)
    {
        var mock = Substitute.For<IProcessClearanceTokenService>();
        mock.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
            {
                ActorId = "orion-downloader",
                ActorType = SiaraActorType.ServiceAccount,
                Clearance = ProcessClearance.Download,
                FileId = fileId,
                Jti = Guid.NewGuid().ToString("D"),
            }));
        return mock;
    }

    public static IProcessClearanceTokenService AcceptingExtract(Guid fileId)
    {
        var mock = Substitute.For<IProcessClearanceTokenService>();
        mock.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
            {
                ActorId = "athena-extractor",
                ActorType = SiaraActorType.ServiceAccount,
                Clearance = ProcessClearance.Extract,
                FileId = fileId,
                Jti = Guid.NewGuid().ToString("D"),
            }));
        return mock;
    }
}

/// <summary>
/// End-to-end proof of the MVP-PATH 1.3 shared-storage edge (ADR-011): a cross-process
/// <see cref="DocumentDownloadedEvent"/> that carries only a storage-<em>relative</em> path must be resolved
/// against the Extractor's own shared-storage base and the pipeline must open the resulting
/// <em>absolute</em> path. Exercises the real <see cref="IngestionEventForwarder"/> +
/// <see cref="SharedStoragePathResolver"/> + <see cref="ProcessingOrchestrator"/> over a real
/// <see cref="EventPublisher"/>; only the leaf adapters (loader, quality) are mocked so the assertion is the
/// path the pipeline actually loads — deterministic and OS-independent.
/// </summary>
public sealed class SharedStorageForwardingEndToEndTests : IDisposable
{
    private readonly EventPublisher _eventPublisher = new(NullLogger<EventPublisher>.Instance);

    [Fact]
    [Trait("Category", "E2E")]
    public async Task RelativePathOnEvent_ResolvedAgainstSharedBase_AndLoadedByPipeline()
    {
        // Arrange — a shared-storage base this (Extractor) process mounts, and the relative path the
        // Downloader put on the wire. The pipeline must open base + relative.
        var fileId = Guid.NewGuid();
        var baseDir = Path.Combine(Path.GetTempPath(), "prisma-shared-e2e-" + Guid.NewGuid().ToString("N"));
        const string relativePath = "2026/06/12/abc.pdf";
        var expectedAbsolute = Path.GetFullPath(Path.Combine(baseDir, "2026", "06", "12", "abc.pdf"));

        string? loadedPath = null;
        var fileLoader = Substitute.For<IFileLoader>();
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                loadedPath = callInfo.ArgAt<string>(0);
                return Result<ImageData>.Success(new ImageData(new byte[] { 1, 2, 3 }, loadedPath!));
            });

        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ImageQualityLevel.Pristine,
                Confidence = 0.95f,
            }));

        var orchestrator = new ProcessingOrchestrator(
            _eventPublisher,
            NullLogger<ProcessingOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            fileLoader: fileLoader);

        await orchestrator.StartAsync(TestContext.Current.CancellationToken);

        var processingComplete = new TaskCompletionSource<bool>();
        using var completionSub = _eventPublisher.GetEventStream<DocumentProcessingCompletedEvent>()
            .Subscribe(_ => processingComplete.TrySetResult(true));

        var resolver = new SharedStoragePathResolver(
            Options.Create(new StorageOptions { BasePath = baseDir }),
            NullLogger<SharedStoragePathResolver>.Instance);
        var tokenService = TokenServiceHelper.AcceptingDownload(fileId);
        var forwarder = new IngestionEventForwarder(
            _eventPublisher, resolver, tokenService, NullLogger<IngestionEventForwarder>.Instance,
            new InMemoryClearanceReplayGuard());

        // Act — the Athena hub client hands the received cross-process event to the forwarder.
        await forwarder.ForwardAsync(new DocumentDownloadedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            FileId = fileId,
            FileName = "abc.pdf",       // bare name from the Downloader
            Path = relativePath,        // storage-relative, mount-path independent
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = FileFormat.Pdf,
            ClearanceToken = "e2e-download-token",
        }, TestContext.Current.CancellationToken);

        var completed = await Task.WhenAny(
            processingComplete.Task,
            Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // Assert — the pipeline ran and opened the resolved absolute path (NOT the bare relative name).
        completed.ShouldBe(processingComplete.Task, "Pipeline should complete within timeout");
        loadedPath.ShouldBe(expectedAbsolute);
    }

    public void Dispose() => _eventPublisher.Dispose();
}

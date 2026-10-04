using System.Security.Cryptography;
using System.Text;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Orion records the case's <see cref="FileMetadata"/> at download time, keyed by the case FileId. The review
/// page reads it (no row means no field annotations) and the SLA row has a foreign key to it, so it must be
/// written before the case is broadcast to the Extractor, and a failure must never block ingestion.
/// </summary>
public sealed class IngestionOrchestratorFileMetadataTests
{
    private const string PdfUrl = "https://siara.local/cases/CASE1/doc.pdf";
    private const string XmlUrl = "https://siara.local/cases/CASE1/doc.xml";

    private static readonly byte[] PdfBytes = Encoding.UTF8.GetBytes("pdf-content");
    private static readonly byte[] XmlBytes = Encoding.UTF8.GetBytes("xml-content");

    private static readonly SiaraCase Case = new()
    {
        CaseId = "CASE1",
        Files = new[]
        {
            new DownloadableFile { Url = XmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
            new DownloadableFile { Url = PdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
        },
    };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_NewCase_RecordsPrimaryFileMetadataBeforeBroadcast()
    {
        var (journal, downloader, eventHub) = NewCaseCollaborators(isDuplicate: false);
        var metadataLogger = Substitute.For<IFileMetadataLogger>();
        metadataLogger.LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var (orchestrator, storagePath) = Build(journal, downloader, eventHub, metadataLogger);

        try
        {
            var result = await orchestrator.IngestCaseAsync(Case, Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors));
            var expectedFileId = result.Value!.FileId.ToString();

            // The PDF is the primary file even though the XML was listed first.
            await metadataLogger.Received(1).LogFileMetadataAsync(
                Arg.Is<FileMetadata>(m =>
                    m.FileId == expectedFileId
                    && m.FileName == "doc.pdf"
                    && m.Url == PdfUrl
                    && m.Format == FileFormat.Pdf
                    && m.FileSize == PdfBytes.LongLength
                    && m.Checksum == Sha256Hex(PdfBytes)
                    && m.Channel == "SIARA"
                    && m.FilePath.EndsWith("doc.pdf", StringComparison.Ordinal)
                    && m.DownloadTimestamp != default),
                Arg.Any<CancellationToken>());

            Received.InOrder(() =>
            {
                metadataLogger.LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<CancellationToken>());
                eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
            });
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_AllFilesDuplicate_DoesNotRecordFileMetadata()
    {
        var (journal, downloader, eventHub) = NewCaseCollaborators(isDuplicate: true);
        var metadataLogger = Substitute.For<IFileMetadataLogger>();
        var (orchestrator, storagePath) = Build(journal, downloader, eventHub, metadataLogger);

        try
        {
            var result = await orchestrator.IngestCaseAsync(Case, Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value!.WasDuplicate.ShouldBeTrue();
            await metadataLogger.DidNotReceive().LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_FileMetadataRejected_StillBroadcastsTheCase()
    {
        // e.g. the case FileId was already recorded by an earlier, partial ingestion.
        var (journal, downloader, eventHub) = NewCaseCollaborators(isDuplicate: false);
        var metadataLogger = Substitute.For<IFileMetadataLogger>();
        metadataLogger.LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("duplicate key"));
        var (orchestrator, storagePath) = Build(journal, downloader, eventHub, metadataLogger);

        try
        {
            var result = await orchestrator.IngestCaseAsync(Case, Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_FileMetadataLoggerThrows_StillBroadcastsTheCase()
    {
        var (journal, downloader, eventHub) = NewCaseCollaborators(isDuplicate: false);
        var metadataLogger = Substitute.For<IFileMetadataLogger>();
        metadataLogger.LogFileMetadataAsync(Arg.Any<FileMetadata>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result>>(_ => throw new InvalidOperationException("database unavailable"));
        var (orchestrator, storagePath) = Build(journal, downloader, eventHub, metadataLogger);

        try
        {
            var result = await orchestrator.IngestCaseAsync(Case, Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static (IIngestionJournal, IDocumentDownloader, IExxerHub<DocumentDownloadedEvent>) NewCaseCollaborators(bool isDuplicate)
    {
        var journal = Substitute.For<IIngestionJournal>();
        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(isDuplicate);
        journal.TryGetStoredPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("2026/01/01/CASE1/doc.pdf");

        var downloader = Substitute.For<IDocumentDownloader>();
        downloader.DownloadAsync(PdfUrl, Arg.Any<CancellationToken>()).Returns(Downloaded(PdfBytes, PdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(XmlUrl, Arg.Any<CancellationToken>()).Returns(Downloaded(XmlBytes, XmlUrl, FileFormat.Xml));

        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        return (journal, downloader, eventHub);
    }

    private static (IngestionOrchestrator Orchestrator, string StoragePath) Build(
        IIngestionJournal journal,
        IDocumentDownloader downloader,
        IExxerHub<DocumentDownloadedEvent> eventHub,
        IFileMetadataLogger metadataLogger)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => metadataLogger);
        services.AddScoped(_ => Substitute.For<IAuditLogger>());
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var storagePath = Path.Combine(Path.GetTempPath(), "iofm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);
        var orchestrator = new IngestionOrchestrator(
            journal,
            downloader,
            eventHub,
            NullLogger<IngestionOrchestrator>.Instance,
            storageBasePath: storagePath,
            scopeFactory: scopeFactory,
            postWriteFlushDelay: TimeSpan.Zero);
        return (orchestrator, storagePath);
    }

    private static Result<DownloadedDocument> Downloaded(byte[] content, string url, FileFormat format) =>
        Result<DownloadedDocument>.Success(new DownloadedDocument
        {
            Content = content,
            DocumentId = url,
            SourceUrl = url,
            Format = format,
            AcquiredBy = new SiaraActor { ActorId = "svc-orion-ingestion", ActorType = SiaraActorType.ServiceAccount },
            SessionId = "sess-abc123",
        });

    private static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}

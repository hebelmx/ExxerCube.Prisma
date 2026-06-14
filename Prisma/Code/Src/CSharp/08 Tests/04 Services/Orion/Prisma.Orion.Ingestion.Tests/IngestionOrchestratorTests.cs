using System.Text;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.Orion.Ingestion.Tests;

/// <summary>
/// Unit tests for <see cref="IngestionOrchestrator.IngestCaseAsync"/>: validates case-level ingestion with
/// Railway-Oriented Programming, one-event-per-case semantics, deterministic FileId, path layout,
/// all-duplicate suppression, and CaseFiles population.
/// </summary>
public sealed class IngestionOrchestratorTests
{
    private static readonly SiaraActor TestActor = new()
    {
        ActorId = "svc-orion-ingestion",
        ActorType = SiaraActorType.ServiceAccount,
        DisplayName = "Orion Ingestion Worker",
    };

    /// <summary>
    /// Builds a successful Railway-Oriented download result carrying the document bytes and trustworthy
    /// provenance (actor + session) the downloader returns (MVP-PATH 1.1 / ADR-010 P2).
    /// </summary>
    private static Result<DownloadedDocument> Downloaded(byte[] content, string url, FileFormat format) =>
        Result<DownloadedDocument>.Success(new DownloadedDocument
        {
            Content = content,
            DocumentId = url,
            SourceUrl = url,
            Format = format,
            AcquiredBy = TestActor,
            SessionId = "sess-abc123",
        });

    /// <summary>
    /// Builds an orchestrator under test with no delay (tests should be fast) and a temp storage path.
    /// </summary>
    private static (IngestionOrchestrator Orchestrator, string StoragePath) BuildOrchestrator(
        IIngestionJournal journal,
        IDocumentDownloader downloader,
        IExxerHub<DocumentDownloadedEvent> eventHub)
    {
        var storagePath = Path.Combine(Path.GetTempPath(), "iot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);
        var orchestrator = new IngestionOrchestrator(
            journal,
            downloader,
            eventHub,
            NullLogger<IngestionOrchestrator>.Instance,
            storageBasePath: storagePath,
            postWriteFlushDelay: TimeSpan.Zero);        // No real delay in unit tests
        return (orchestrator, storagePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_NewThreeFileCase_ReturnsSuccessAndBroadcastsOneEvent()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/CASE1/doc.pdf";
        var xmlUrl = "https://siara.local/cases/CASE1/doc.xml";
        var docxUrl = "https://siara.local/cases/CASE1/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-content"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml-content"), xmlUrl, FileFormat.Xml));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("docx-content"), docxUrl, FileFormat.Docx));

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "CASE1",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var correlationId = Guid.NewGuid();
        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);
        var now = DateTime.UtcNow;

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, correlationId, TestContext.Current.CancellationToken);

            // Assert: success
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.WasDuplicate.ShouldBeFalse();
            result.Value!.CorrelationId.ShouldBe(correlationId);

            // Assert: exactly ONE event broadcast
            await eventHub.Received(1).SendToAllAsync(
                Arg.Any<DocumentDownloadedEvent>(),
                Arg.Any<CancellationToken>());

            // Assert: the event carries all 3 CaseFileReferences
            await eventHub.Received(1).SendToAllAsync(
                Arg.Is<DocumentDownloadedEvent>(e =>
                    e.CaseFiles.Count == 3 &&
                    e.CorrelationId == correlationId &&
                    e.Source == "SIARA"),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_CaseFiles_HaveCorrectRelativePathsAndFormats()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/CASE2/report.pdf";
        var xmlUrl = "https://siara.local/cases/CASE2/data.xml";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml"), xmlUrl, FileFormat.Xml));

        // Set up a capture via Arg.Do BEFORE the Act call so the side-effect fires on the real call.
        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "CASE2",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "report.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "data.xml", Format = FileFormat.Xml },
            },
        };

        var now = DateTime.UtcNow;
        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            captured.ShouldNotBeNull();
            captured!.CaseFiles.Count.ShouldBe(2);

            // Paths must follow YYYY/MM/DD/{caseId}/{fileName} with forward slashes
            var expectedDatePrefix = $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}/CASE2/";
            foreach (var fileRef in captured.CaseFiles)
            {
                fileRef.RelativePath.ShouldStartWith(expectedDatePrefix);
            }

            // Formats match inputs
            captured.CaseFiles.ShouldContain(r => r.Format == FileFormat.Pdf);
            captured.CaseFiles.ShouldContain(r => r.Format == FileFormat.Xml);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_FileId_IsDeterministicFromCaseId()
    {
        // Arrange — two separate orchestrator instances ingesting the same caseId must produce the same FileId
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var url = "https://siara.local/cases/STABLE/doc.pdf";
        downloader.DownloadAsync(url, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("content"), url, FileFormat.Pdf));

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "STABLE",
            Files = new[] { new DownloadableFile { Url = url, FileName = "doc.pdf", Format = FileFormat.Pdf } },
        };

        var (orch1, path1) = BuildOrchestrator(journal, downloader, eventHub);
        var (orch2, path2) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var r1 = await orch1.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);
            // Second orchestrator — journal returns false so it tries to store again (different bytes path)
            // but the FileId must match.
            var r2 = await orch2.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: same deterministic FileId regardless of orchestrator instance
            r1.IsSuccess.ShouldBeTrue();
            r2.IsSuccess.ShouldBeTrue();
            r1.Value!.FileId.ShouldBe(r2.Value!.FileId);
            r1.Value!.FileId.ShouldNotBe(Guid.Empty);
        }
        finally
        {
            Directory.Delete(path1, recursive: true);
            Directory.Delete(path2, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PrimaryFile_IsPdfWhenPresent()
    {
        // Arrange: XML is first, PDF is second — primary should be the PDF
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var xmlUrl = "https://siara.local/cases/CASE3/data.xml";
        var pdfUrl = "https://siara.local/cases/CASE3/doc.pdf";

        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml"), xmlUrl, FileFormat.Xml));
        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf"), pdfUrl, FileFormat.Pdf));

        // Pre-call capture: Arg.Do fires when SendToAllAsync is called during the Act.
        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // XML is listed first; PDF is second
        var siaraCase = new SiaraCase
        {
            CaseId = "CASE3",
            Files = new[]
            {
                new DownloadableFile { Url = xmlUrl, FileName = "data.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: primary fields derive from the PDF file
            result.IsSuccess.ShouldBeTrue();
            captured.ShouldNotBeNull();
            captured!.Format.ShouldBe(FileFormat.Pdf);
            captured!.FileName.ShouldBe("doc.pdf");
            // Primary path ends with the PDF file name
            captured!.Path.ShouldEndWith("doc.pdf");
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_AllDuplicates_ReturnsSuccessWithNoBroadcast()
    {
        // Arrange: all files report as duplicates via the journal
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);   // All files are duplicates

        var pdfUrl = "https://siara.local/cases/CASE4/doc.pdf";
        var xmlUrl = "https://siara.local/cases/CASE4/doc.xml";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml"), xmlUrl, FileFormat.Xml));

        var siaraCase = new SiaraCase
        {
            CaseId = "CASE4",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: idempotent success, no broadcast
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.WasDuplicate.ShouldBeTrue();

            // No event must be sent when all files are duplicates
            await eventHub.DidNotReceive().SendToAllAsync(
                Arg.Any<DocumentDownloadedEvent>(),
                Arg.Any<CancellationToken>());

            // Journal must NOT be written to for already-known files
            await journal.DidNotReceive().RecordAsync(
                Arg.Any<IngestionManifestEntry>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_WhenCancelled_ReturnsCancelledResult()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        var siaraCase = new SiaraCase
        {
            CaseId = "CASE_CT",
            Files = new[] { new DownloadableFile { Url = "https://x/a.pdf", FileName = "a.pdf", Format = FileFormat.Pdf } },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), cts.Token);

            // Assert: cancelled result, no side effects
            result.IsCancelled().ShouldBeTrue();

            await downloader.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
            await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_WhenDownloadFails_ReturnsFailureWithoutBroadcast()
    {
        // Arrange: the downloader fails closed for one file
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DownloadedDocument>.WithFailure("Network error"));

        var siaraCase = new SiaraCase
        {
            CaseId = "CASE_FAIL",
            Files = new[] { new DownloadableFile { Url = "https://x/fail.pdf", FileName = "fail.pdf", Format = FileFormat.Pdf } },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldContain(e => e.Contains("Network error"));

            await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
            await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_StoresFilesUnderCaseFolderPath()
    {
        // Arrange
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/CASESTORE/a.pdf";
        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-bytes"), pdfUrl, FileFormat.Pdf));

        // Pre-call capture: Arg.Do fires when SendToAllAsync is called during the Act.
        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "CASESTORE",
            Files = new[] { new DownloadableFile { Url = pdfUrl, FileName = "a.pdf", Format = FileFormat.Pdf } },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);
        var now = DateTime.UtcNow;

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: the event Path follows YYYY/MM/DD/{caseId}/{fileName}
            result.IsSuccess.ShouldBeTrue();
            captured.ShouldNotBeNull();
            var expectedPathPrefix = $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}/CASESTORE/";
            captured!.Path.ShouldStartWith(expectedPathPrefix);
            captured!.Path.ShouldEndWith("a.pdf");

            // The file should physically exist under the storage root
            var physicalPath = System.IO.Path.Combine(
                storagePath,
                captured.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
            File.Exists(physicalPath).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_EventFileId_MatchesDeterministicCaseGuid()
    {
        // Arrange: verify the event carries the deterministic FileId (not a random Guid)
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var url = "https://siara.local/cases/CASE_DET/doc.pdf";
        downloader.DownloadAsync(url, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf"), url, FileFormat.Pdf));

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var caseId = "CASE_DET";
        var siaraCase = new SiaraCase
        {
            CaseId = caseId,
            Files = new[] { new DownloadableFile { Url = url, FileName = "doc.pdf", Format = FileFormat.Pdf } },
        };

        // Compute the expected deterministic Guid the same way the orchestrator does (MD5 of UTF-8 caseId)
        var expectedFileId = new Guid(
            System.Security.Cryptography.MD5.HashData(
                System.Text.Encoding.UTF8.GetBytes(caseId)));

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value!.FileId.ShouldBe(expectedFileId);

            await eventHub.Received(1).SendToAllAsync(
                Arg.Is<DocumentDownloadedEvent>(e => e.FileId == expectedFileId),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }
}

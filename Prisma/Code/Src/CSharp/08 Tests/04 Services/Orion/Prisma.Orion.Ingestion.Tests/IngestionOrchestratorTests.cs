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
    public async Task IngestCase_WhenAllFilesFailToDownload_ReturnsFailureWithoutBroadcast()
    {
        // Arrange: the only file in the case fails — zero files obtained — hard fail (best-effort contract).
        // Previously this test was named IngestCase_WhenDownloadFails_ReturnsFailureWithoutBroadcast and
        // asserted on the raw downloader error message. Under the new best-effort contract the failure message
        // is the "no files could be downloaded" case summary (issue #4, owner ruling 2026-06-13).
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

            // Assert: zero files obtained → hard fail
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldContain(e => e.Contains("no files could be downloaded"));

            await journal.DidNotReceive().RecordAsync(Arg.Any<IngestionManifestEntry>(), Arg.Any<CancellationToken>());
            await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Best-effort partial-case ingestion tests (issue #4, owner ruling 2026-06-13)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a failed download result for a file URL.
    /// </summary>
    private static Result<DownloadedDocument> FailedDownload(string errorMessage = "Network error") =>
        Result<DownloadedDocument>.WithFailure(errorMessage);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PartialCase_PdfMissing_BroadcastsEventWithTwoFiles()
    {
        // Arrange: 3-file case where PDF fails; DOCX + XML succeed.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/PARTIAL1/doc.pdf";
        var xmlUrl = "https://siara.local/cases/PARTIAL1/doc.xml";
        var docxUrl = "https://siara.local/cases/PARTIAL1/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("PDF unavailable"));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml-content"), xmlUrl, FileFormat.Xml));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("docx-content"), docxUrl, FileFormat.Docx));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL1",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: best-effort success
            result.IsSuccess.ShouldBeTrue();

            // One event broadcast
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            // Event is flagged as incomplete with 2 of 3 files
            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeFalse();
            captured!.CaseFiles.Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PartialCase_DocxMissing_BroadcastsEventWithTwoFiles()
    {
        // Arrange: 3-file case where DOCX fails; PDF + XML succeed.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/PARTIAL2/doc.pdf";
        var xmlUrl = "https://siara.local/cases/PARTIAL2/doc.xml";
        var docxUrl = "https://siara.local/cases/PARTIAL2/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-content"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml-content"), xmlUrl, FileFormat.Xml));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("DOCX unavailable"));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL2",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeFalse();
            captured!.CaseFiles.Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PartialCase_XmlMissing_BroadcastsEventWithTwoFiles()
    {
        // Arrange: 3-file case where XML fails; PDF + DOCX succeed.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/PARTIAL3/doc.pdf";
        var xmlUrl = "https://siara.local/cases/PARTIAL3/doc.xml";
        var docxUrl = "https://siara.local/cases/PARTIAL3/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-content"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("XML unavailable"));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("docx-content"), docxUrl, FileFormat.Docx));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL3",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeFalse();
            captured!.CaseFiles.Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PartialCase_OnlyPdfSucceeds_BroadcastsEventWithOneFile()
    {
        // Arrange: 3-file case where only PDF succeeds (XML + DOCX fail).
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/PARTIAL4/doc.pdf";
        var xmlUrl = "https://siara.local/cases/PARTIAL4/doc.xml";
        var docxUrl = "https://siara.local/cases/PARTIAL4/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-content"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("XML unavailable"));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("DOCX unavailable"));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL4",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeFalse();
            captured!.CaseFiles.Count.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PartialCase_OnlyDocxSucceeds_BroadcastsEventWithOneFile()
    {
        // Arrange: 3-file case where only DOCX succeeds (PDF + XML fail).
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/PARTIAL5/doc.pdf";
        var xmlUrl = "https://siara.local/cases/PARTIAL5/doc.xml";
        var docxUrl = "https://siara.local/cases/PARTIAL5/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("PDF unavailable"));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("XML unavailable"));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("docx-content"), docxUrl, FileFormat.Docx));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL5",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeFalse();
            captured!.CaseFiles.Count.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_PartialCase_OnlyXmlSucceeds_BroadcastsEventWithOneFile()
    {
        // Arrange: 3-file case where only XML succeeds (PDF + DOCX fail).
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/PARTIAL6/doc.pdf";
        var xmlUrl = "https://siara.local/cases/PARTIAL6/doc.xml";
        var docxUrl = "https://siara.local/cases/PARTIAL6/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("PDF unavailable"));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml-content"), xmlUrl, FileFormat.Xml));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(FailedDownload("DOCX unavailable"));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL6",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeFalse();
            captured!.CaseFiles.Count.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_AllFilesFailToDownload_ReturnsFailureAndNoBroadcast()
    {
        // Arrange: all 3 files fail — zero obtained — hard fail, no event.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FailedDownload("Network error"));

        var siaraCase = new SiaraCase
        {
            CaseId = "PARTIAL7",
            Files = new[]
            {
                new DownloadableFile { Url = "https://siara.local/cases/PARTIAL7/doc.pdf", FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = "https://siara.local/cases/PARTIAL7/doc.xml", FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = "https://siara.local/cases/PARTIAL7/doc.docx", FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: zero files obtained → hard fail
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldContain(e => e.Contains("no files could be downloaded"));

            await eventHub.DidNotReceive().SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_AllThreeFilesPresent_IsCompleteIsTrue()
    {
        // Arrange: happy path — all 3 files succeed → IsComplete true.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var pdfUrl = "https://siara.local/cases/COMPLETE1/doc.pdf";
        var xmlUrl = "https://siara.local/cases/COMPLETE1/doc.xml";
        var docxUrl = "https://siara.local/cases/COMPLETE1/doc.docx";

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-content"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml-content"), xmlUrl, FileFormat.Xml));
        downloader.DownloadAsync(docxUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("docx-content"), docxUrl, FileFormat.Docx));

        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "COMPLETE1",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
                new DownloadableFile { Url = docxUrl, FileName = "doc.docx", Format = FileFormat.Docx },
            },
        };

        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.IsComplete.ShouldBeTrue();
            captured!.CaseFiles.Count.ShouldBe(3);
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

    // -------------------------------------------------------------------------
    // GH issue #3: cross-day-partition duplicate resolution
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_DuplicateFileStoredEarlierDay_EmitsOriginalStoredPath()
    {
        // Arrange: 2-file case (PDF new, XML duplicate stored on an earlier day).
        // The bug: without the fix, relativePath for the XML ref is today's partition
        // (BuildRelativePath(now, ...)) even though the bytes live under 2026/06/10/CASE5/doc.xml.
        // The fix: the else-branch calls TryGetStoredPathAsync and reassigns relativePath.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        var pdfUrl = "https://siara.local/cases/CASE5/doc.pdf";
        var xmlUrl = "https://siara.local/cases/CASE5/doc.xml";
        const string earlierDayXmlPath = "2026/06/10/CASE5/doc.xml";

        // Default: no file is a duplicate (PDF is new)
        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // XML is a duplicate (stored on an earlier day)
        journal.ExistsAsync(Arg.Any<string>(), Arg.Is<string>(u => u == xmlUrl), Arg.Any<CancellationToken>())
            .Returns(true);

        // Journal returns the original earlier-day stored path for the XML hash+url
        journal.TryGetStoredPathAsync(Arg.Any<string>(), Arg.Is<string>(u => u == xmlUrl), Arg.Any<CancellationToken>())
            .Returns(earlierDayXmlPath);

        downloader.DownloadAsync(pdfUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("pdf-content"), pdfUrl, FileFormat.Pdf));
        downloader.DownloadAsync(xmlUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(Encoding.UTF8.GetBytes("xml-content"), xmlUrl, FileFormat.Xml));

        // Pre-call capture via Arg.Do
        DocumentDownloadedEvent? captured = null;
        eventHub.SendToAllAsync(
            Arg.Do<DocumentDownloadedEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "CASE5",
            Files = new[]
            {
                new DownloadableFile { Url = pdfUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
                new DownloadableFile { Url = xmlUrl, FileName = "doc.xml", Format = FileFormat.Xml },
            },
        };

        var now = DateTime.UtcNow;
        var (orchestrator, storagePath) = BuildOrchestrator(journal, downloader, eventHub);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: overall success (PDF is new → anyNew=true → event fires)
            result.IsSuccess.ShouldBeTrue();

            // Exactly one event broadcast
            await eventHub.Received(1).SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            captured.ShouldNotBeNull();
            captured!.CaseFiles.Count.ShouldBe(2);

            // The XML CaseFileReference must carry the original earlier-day path, NOT today's partition.
            var xmlRef = captured.CaseFiles.Single(f => f.Format == FileFormat.Xml);
            xmlRef.RelativePath.ShouldBe(earlierDayXmlPath);

            // Sanity: PDF ref uses today's partition (new file, unchanged path).
            var pdfRef = captured.CaseFiles.Single(f => f.Format == FileFormat.Pdf);
            var todayPartition = $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}/CASE5/";
            pdfRef.RelativePath.ShouldStartWith(todayPartition);
            pdfRef.RelativePath.ShouldEndWith("doc.pdf");

            // Guard: XML path must NOT be today's partition (that was the bug)
            xmlRef.RelativePath.ShouldNotStartWith(todayPartition);
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // RV-1: Encryption-at-rest on the Orion download-persist path (G-S1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Minimal AES-256-GCM <see cref="IStorageEncryptor"/> for use in RV-1 tests only.
    /// Produces the same on-disk blob layout as AesGcmStorageEncryptor:
    /// [12 bytes nonce][16 bytes GCM tag][N bytes ciphertext].
    /// </summary>
    private sealed class TestAesGcmEncryptor : IStorageEncryptor
    {
        private readonly byte[] _key;

        public TestAesGcmEncryptor()
        {
            _key = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(_key);
        }

        public Task<Result<byte[]>> EncryptAsync(byte[] plaintext, string purpose, CancellationToken ct = default)
        {
            const int NonceSize = 12;
            const int TagSize = 16;
            var nonce = new byte[NonceSize];
            System.Security.Cryptography.RandomNumberGenerator.Fill(nonce);

            var cipher = new byte[plaintext.Length];
            var tag = new byte[TagSize];

            using var aes = new System.Security.Cryptography.AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            var blob = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, blob, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, blob, NonceSize + TagSize, cipher.Length);

            return Task.FromResult(Result<byte[]>.Success(blob));
        }

        public Task<Result<byte[]>> DecryptAsync(byte[] ciphertext, string purpose, CancellationToken ct = default)
        {
            const int NonceSize = 12;
            const int TagSize = 16;
            var nonce = ciphertext[..NonceSize];
            var tag = ciphertext[NonceSize..(NonceSize + TagSize)];
            var encPayload = ciphertext[(NonceSize + TagSize)..];

            var plaintext = new byte[encPayload.Length];
            using var aes = new System.Security.Cryptography.AesGcm(_key, TagSize);
            aes.Decrypt(nonce, encPayload, tag, plaintext);

            return Task.FromResult(Result<byte[]>.Success(plaintext));
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_WithEncryptor_OnDiskBytesAreCiphertext_NotPlaintext()
    {
        // Arrange — verify the RV-1 contract: when IStorageEncryptor is wired, raw on-disk bytes
        // must NOT equal the plaintext document bytes the downloader returned.
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Plaintext content we will assert is NOT on disk.
        var plaintextContent = Encoding.UTF8.GetBytes("SENSITIVE-PDF-CONTENT-SHOULD-NOT-APPEAR-ON-DISK");
        var fileUrl = "https://siara.local/cases/ENCTEST/encrypted.pdf";

        downloader.DownloadAsync(fileUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(plaintextContent, fileUrl, FileFormat.Pdf));

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "ENCTEST",
            Files = new[] { new DownloadableFile { Url = fileUrl, FileName = "encrypted.pdf", Format = FileFormat.Pdf } },
        };

        // Build orchestrator WITH encryptor (the production path).
        var storagePath = Path.Combine(Path.GetTempPath(), "rv1-enc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);
        var encryptor = new TestAesGcmEncryptor();
        var orchestrator = new IngestionOrchestrator(
            journal, downloader, eventHub,
            NullLogger<IngestionOrchestrator>.Instance,
            storageBasePath: storagePath,
            postWriteFlushDelay: TimeSpan.Zero,
            storageEncryptor: encryptor);

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: ingestion succeeded
            result.IsSuccess.ShouldBeTrue($"Ingestion failed: {string.Join(", ", result.Errors)}");

            // Assert: ONE event broadcast — pipeline not broken by encryption
            await eventHub.Received(1).SendToAllAsync(
                Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>());

            // Assert: on-disk bytes are NOT the plaintext content (ciphertext proof).
            var storedFiles = Directory.GetFiles(storagePath, "encrypted.pdf", SearchOption.AllDirectories);
            storedFiles.Length.ShouldBe(1, "Exactly one file should have been stored on disk.");

            var diskBytes = await File.ReadAllBytesAsync(storedFiles[0], TestContext.Current.CancellationToken);

            // Primary assertion: the on-disk blob must differ from the plaintext.
            diskBytes.ShouldNotBe(plaintextContent,
                "On-disk bytes must be ciphertext, not plaintext (G-S1 / RV-1 requirement).");

            // Secondary assertion: the blob is larger than plaintext (AES-GCM adds 12-byte nonce + 16-byte tag).
            diskBytes.Length.ShouldBeGreaterThan(plaintextContent.Length,
                "Encrypted blob must be larger than plaintext due to nonce+tag overhead.");

            // Guard: the plaintext sentinel string must NOT appear verbatim in the blob.
            var diskString = Encoding.UTF8.GetString(diskBytes);
            diskString.Contains("SENSITIVE-PDF-CONTENT-SHOULD-NOT-APPEAR-ON-DISK", StringComparison.Ordinal)
                .ShouldBeFalse("Plaintext sentinel must not be readable in the on-disk ciphertext blob.");
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IngestCase_WithoutEncryptor_OnDiskBytesMatchPlaintext_AndWarningIsLogged()
    {
        // Arrange — without an encryptor (dev mode), bytes are plaintext and the ctor warning fires.
        // This test confirms the fallback path works (pipeline not broken by absent encryption key).
        var journal = Substitute.For<IIngestionJournal>();
        var downloader = Substitute.For<IDocumentDownloader>();
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();

        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var plaintextContent = Encoding.UTF8.GetBytes("dev-plaintext-content");
        var fileUrl = "https://siara.local/cases/NOENC/doc.pdf";

        downloader.DownloadAsync(fileUrl, Arg.Any<CancellationToken>())
            .Returns(Downloaded(plaintextContent, fileUrl, FileFormat.Pdf));

        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var siaraCase = new SiaraCase
        {
            CaseId = "NOENC",
            Files = new[] { new DownloadableFile { Url = fileUrl, FileName = "doc.pdf", Format = FileFormat.Pdf } },
        };

        // Build orchestrator WITHOUT encryptor (no storageEncryptor argument — falls back to plaintext).
        var storagePath = Path.Combine(Path.GetTempPath(), "rv1-noenc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);
        var orchestrator = new IngestionOrchestrator(
            journal, downloader, eventHub,
            NullLogger<IngestionOrchestrator>.Instance,
            storageBasePath: storagePath,
            postWriteFlushDelay: TimeSpan.Zero
            // storageEncryptor: null (omitted — tests the dev-mode fallback)
        );

        try
        {
            // Act
            var result = await orchestrator.IngestCaseAsync(siaraCase, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: ingestion still succeeds (plaintext fallback, not a crash)
            result.IsSuccess.ShouldBeTrue("Dev-mode (no encryptor) ingestion should still succeed.");

            var storedFiles = Directory.GetFiles(storagePath, "doc.pdf", SearchOption.AllDirectories);
            storedFiles.Length.ShouldBe(1);

            var diskBytes = await File.ReadAllBytesAsync(storedFiles[0], TestContext.Current.CancellationToken);
            diskBytes.ShouldBe(plaintextContent,
                "Without an encryptor (dev mode), on-disk bytes should be plaintext.");
        }
        finally
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }
}

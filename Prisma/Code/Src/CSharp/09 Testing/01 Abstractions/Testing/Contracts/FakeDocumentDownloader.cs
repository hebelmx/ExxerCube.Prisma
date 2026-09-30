using System.Text;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written, in-memory reference fake of <see cref="IDocumentDownloader"/> (ADR-005 §6): honest
/// Railway-Oriented logic, no mocking framework. It produces deterministic content and the per-document
/// provenance (a trustworthy actor + session id) a real downloader returns, without a live browser or a
/// real SIARA — so it unblocks the ingestion-orchestrator and watch-loop tests.
/// </summary>
/// <remarks>
/// Construct with <c>failClosed: true</c> to model the fail-closed path (resolver/provider/auth failure)
/// where the downloader returns a failure rather than a document.
/// </remarks>
public sealed class FakeDocumentDownloader : IDocumentDownloader
{
    private readonly bool _failClosed;
    private int _counter;

    /// <summary>Initializes the fake.</summary>
    /// <param name="failClosed">When <see langword="true"/>, every (non-blank, non-cancelled) call fails closed.</param>
    public FakeDocumentDownloader(bool failClosed = false) => _failClosed = failClosed;

    /// <summary>Gets the number of documents successfully produced by this fake so far.</summary>
    public int DownloadCount => _counter;

    /// <inheritdoc />
    public Task<Result<DownloadedDocument>> DownloadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<DownloadedDocument>());
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.FromResult(Result<DownloadedDocument>.WithFailure("Document id cannot be null or empty."));
        }

        if (_failClosed)
        {
            return Task.FromResult(Result<DownloadedDocument>.WithFailure(
                "Fake downloader configured to fail closed (no authenticated SIARA session)."));
        }

        var n = Interlocked.Increment(ref _counter);
        var document = new DownloadedDocument
        {
            Content = Encoding.UTF8.GetBytes($"fake-document-content-{documentId}"),
            DocumentId = documentId,
            SourceUrl = $"https://siara.fake/documents/{documentId}.pdf",
            Format = FileFormat.Pdf,
            AcquiredBy = new SiaraActor
            {
                ActorId = "fake-downloader-actor",
                ActorType = SiaraActorType.ServiceAccount,
            },
            SessionId = $"fake-download-session-{n}",
        };

        return Task.FromResult(Result<DownloadedDocument>.Success(document));
    }
}

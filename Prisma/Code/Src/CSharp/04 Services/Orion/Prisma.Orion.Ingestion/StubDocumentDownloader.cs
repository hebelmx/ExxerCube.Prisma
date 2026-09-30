using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Stub implementation of IDocumentDownloader for testing and development.
/// Fails closed: it is NOT a real SIARA integration and never produces a document, so it can never be
/// mistaken for a working download. Replaced by SiaraDocumentDownloader in the Orion worker (MVP-PATH 1.1).
/// </summary>
public sealed class StubDocumentDownloader : IDocumentDownloader
{
    private readonly ILogger<StubDocumentDownloader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubDocumentDownloader"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public StubDocumentDownloader(ILogger<StubDocumentDownloader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<Result<DownloadedDocument>> DownloadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<DownloadedDocument>());
        }

        _logger.LogWarning(
            "StubDocumentDownloader: no real SIARA integration is wired for document {DocumentId}; failing closed. Register SiaraDocumentDownloader.",
            documentId);
        return Task.FromResult(Result<DownloadedDocument>.WithFailure(
            "StubDocumentDownloader is not a real SIARA integration. Register SiaraDocumentDownloader (MVP-PATH 1.1)."));
    }
}

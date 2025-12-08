using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Stub implementation of IDocumentDownloader for testing and development.
/// Returns empty byte arrays. Replace with actual SIARA integration in production.
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
    public Task<byte[]> DownloadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("StubDocumentDownloader: Returning empty byte array for document {DocumentId}. Replace with actual implementation.", documentId);
        return Task.FromResult(Array.Empty<byte>());
    }
}

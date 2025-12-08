namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Document downloader abstraction for fetching PDFs from external sources.
/// </summary>
public interface IDocumentDownloader
{
    /// <summary>
    /// Downloads a document from an external source.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document content as bytes.</returns>
    Task<byte[]> DownloadAsync(string documentId, CancellationToken cancellationToken = default);
}

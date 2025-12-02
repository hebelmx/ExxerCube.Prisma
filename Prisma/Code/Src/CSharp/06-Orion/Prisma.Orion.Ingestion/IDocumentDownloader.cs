using System.Threading;
using System.Threading.Tasks;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// Document downloader abstraction for fetching PDFs from SIARA.
/// </summary>
public interface IDocumentDownloader
{
    /// <summary>
    /// Downloads a document from SIARA.
    /// </summary>
    /// <param name="documentId">SIARA document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document content as bytes.</returns>
    Task<byte[]> DownloadAsync(string documentId, CancellationToken cancellationToken = default);
}

namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Document downloader abstraction for fetching documents from external sources (SIARA).
/// </summary>
/// <remarks>
/// Railway-Oriented: returns <see cref="Result{T}"/> and never throws for business outcomes. A
/// pre-cancelled token yields a cancelled result; an authentication or fetch failure fails closed (a
/// failure result, never an empty-but-successful download). The returned <see cref="DownloadedDocument"/>
/// carries the trustworthy actor and session that authorized the pull for per-document non-repudiation
/// (ADR-010 P2).
/// </remarks>
public interface IDocumentDownloader
{
    /// <summary>
    /// Downloads a document from an external source.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A success result wrapping the downloaded content and its provenance, or a failure (including a
    /// cancelled result for a pre-cancelled token).
    /// </returns>
    Task<Result<DownloadedDocument>> DownloadAsync(string documentId, CancellationToken cancellationToken = default);
}

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Discovery port that lists the documents currently available to be pulled from SIARA, so the headless
/// watch loop (MVP-PATH 1.2) knows what to ingest each cycle.
/// </summary>
/// <remarks>
/// <para>
/// This is the "list" half of ingestion, kept separate from the "download" half
/// (<see cref="IDocumentDownloader"/>): the watch loop discovers ids here, then pulls each one through the
/// downloader inside its own DI scope. An implementation rides the same credential-free SIARA auth seam
/// (<see cref="ISiaraSessionProvider"/>, ADR-010) and is expected to keep its session warm across cycles
/// via <see cref="ISiaraSessionProvider.EnsureValidAsync"/> so a long-running loop runs off a single
/// acquisition.
/// </para>
/// <para>
/// <strong>Contract:</strong> Railway-Oriented — returns <see cref="Result{T}"/> and never throws for
/// business outcomes; a pre-cancelled token yields a cancelled result; an authentication or navigation
/// failure fails closed (a failure result, never a silently-empty success). A successful result wraps a
/// non-null list of non-blank document ids (the list may be empty when SIARA presents nothing).
/// </para>
/// </remarks>
public interface ISiaraDocumentSource
{
    /// <summary>
    /// Lists the document ids SIARA currently presents. Each returned id is something
    /// <see cref="IDocumentDownloader.DownloadAsync"/> can resolve (a SIARA document URL or file name).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping the (possibly empty) list of available document ids, or a failure
    /// (including a cancelled result for a pre-cancelled token).
    /// </returns>
    Task<Result<IReadOnlyList<string>>> DiscoverDocumentIdsAsync(CancellationToken cancellationToken = default);
}

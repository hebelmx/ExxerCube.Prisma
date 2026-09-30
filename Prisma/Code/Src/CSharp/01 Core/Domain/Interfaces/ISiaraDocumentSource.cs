namespace ExxerCube.Prisma.Domain.Interfaces;

using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Discovery port that lists the SIARA cases currently available to be pulled, so the headless
/// watch loop (MVP-PATH 1.2 / 2.1) knows what to ingest each cycle.
/// </summary>
/// <remarks>
/// <para>
/// This is the "list" half of ingestion, kept separate from the "download" half
/// (<see cref="IDocumentDownloader"/>): the watch loop discovers cases here, then pulls all companion files
/// for each case through the downloader inside its own DI scope. An implementation rides the same
/// credential-free SIARA auth seam (<see cref="ISiaraSessionProvider"/>, ADR-010) and is expected to keep
/// its session warm across cycles via <see cref="ISiaraSessionProvider.EnsureValidAsync"/> so a long-running
/// loop runs off a single acquisition.
/// </para>
/// <para>
/// <strong>Contract:</strong> Railway-Oriented — returns <see cref="Result{T}"/> and never throws for
/// business outcomes; a pre-cancelled token yields a cancelled result; an authentication or navigation
/// failure fails closed (a failure result, never a silently-empty success). A successful result wraps a
/// non-null list; the list may be empty when SIARA presents nothing.
/// </para>
/// </remarks>
public interface ISiaraDocumentSource
{
    /// <summary>
    /// Lists the SIARA cases currently presented, each bundling its companion files (XML, PDF, DOCX).
    /// Used by the case-aware watch loop (MVP-PATH 2.1) to emit one event per case instead of one per file.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success result wrapping the (possibly empty) list of <see cref="SiaraCase"/> bundles, or a
    /// failure (including a cancelled result for a pre-cancelled token). Authentication or navigation
    /// failures fail closed — never a silently-empty success.
    /// </returns>
    Task<Result<IReadOnlyList<SiaraCase>>> DiscoverCasesAsync(CancellationToken cancellationToken = default);
}

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Port that turns a storage-<em>relative</em> document path (as carried on a cross-process
/// <see cref="ExxerCube.Prisma.Domain.Events.DocumentDownloadedEvent"/>) into a locally-loadable
/// <em>absolute</em> path, resolved against the storage base this process mounts (MVP-PATH 1.3 shared
/// storage, ADR-011).
/// </summary>
/// <remarks>
/// <para>
/// The security-mandated 3-process split runs the Downloader (Orion) and Extractor (Athena) as separate
/// processes that share a storage volume which may be mounted at <em>different</em> absolute paths in each
/// process. The Downloader emits only the relative path (<c>YYYY/MM/DD/{fileId}.pdf</c>); each consumer
/// resolves it against its own configured base, so the wire contract stays mount-path independent.
/// </para>
/// <para>
/// <strong>Contract:</strong> resolution is <em>deterministic</em> pure path math (no disk access — file
/// existence is the loader's concern) and <em>tolerant of configuration faults</em>: a blank input, an
/// unconfigured base, or a path that would escape the base all fail closed as a <see cref="Result{T}"/>
/// failure rather than throwing, so the caller can log-and-continue. A successful result is a rooted,
/// normalized absolute path that stays within the configured base.
/// </para>
/// </remarks>
public interface IStoragePathResolver
{
    /// <summary>
    /// Resolves a storage-relative path to an absolute, base-confined path.
    /// </summary>
    /// <param name="relativeStoragePath">
    /// The storage-relative path to resolve (for example <c>2026/06/12/abc.pdf</c>; either path separator
    /// is accepted).
    /// </param>
    /// <returns>
    /// A success result wrapping the rooted absolute path, or a failure when the input is blank, the storage
    /// base is not configured, or the resolved path would escape the configured base.
    /// </returns>
    Result<string> Resolve(string relativeStoragePath);
}

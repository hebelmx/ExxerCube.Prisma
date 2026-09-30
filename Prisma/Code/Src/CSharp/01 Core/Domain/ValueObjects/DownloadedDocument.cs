namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// A document pulled from SIARA together with the provenance that authorized the pull.
/// </summary>
/// <remarks>
/// <para>
/// This is what <see cref="Interfaces.IDocumentDownloader"/> returns (MVP-PATH 1.1). It carries the raw
/// bytes <em>and</em> the trustworthy <see cref="SiaraActor"/> plus session correlation id that the SIARA
/// auth seam (ADR-010 P2) resolved for the acquisition, so per-document non-repudiation is realized end to
/// end: the actor that authorized the download is recorded against the specific document, not just against
/// the session.
/// </para>
/// <para>
/// <strong>Security invariant:</strong> the provenance members are trustworthy identity tokens (an actor id
/// and our own session correlation id), never raw credentials and never the opaque storage-state secret.
/// </para>
/// </remarks>
public sealed record DownloadedDocument
{
    /// <summary>Gets the raw document content.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Gets the SIARA document identifier this content was pulled for.</summary>
    public required string DocumentId { get; init; }

    /// <summary>Gets the URL the file was actually downloaded from (for the audit/manifest trail).</summary>
    public required string SourceUrl { get; init; }

    /// <summary>Gets the file format as presented by SIARA.</summary>
    public FileFormat Format { get; init; } = FileFormat.Unknown;

    /// <summary>
    /// Gets the trustworthy actor whose acquired session authorized this download, recorded for
    /// per-document non-repudiation (ADR-010 P2). Resolved by ISiaraActorIdentityProvider, never
    /// supplied by the caller.
    /// </summary>
    public required SiaraActor AcquiredBy { get; init; }

    /// <summary>Gets the correlation id of the SIARA session used to pull this document (for the audit trail).</summary>
    public required string SessionId { get; init; }
}

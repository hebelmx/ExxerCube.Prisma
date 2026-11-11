namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// The storage-relative path of one companion file within a SIARA case, together with its detected format.
/// </summary>
/// <remarks>
/// This is what the <c>DocumentDownloadedEvent</c> carries for each companion file after a full-case
/// download (MVP-PATH 2.1). The relative path is resolved against the per-process configured storage base
/// by the consuming process (Athena Extractor), following the same convention as
/// <c>DocumentDownloadedEvent.Path</c> (ADR-011 shared-storage handoff).
/// </remarks>
public sealed record CaseFileReference
{
    /// <summary>
    /// Gets the storage-relative path of the case file (e.g. <c>2026/06/13/{fileId}.xml</c>).
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Gets the detected file format of the case file.
    /// </summary>
    public required FileFormat Format { get; init; }
}

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// A SIARA case bundling its companion files (XML, PDF, DOCX) that belong to the same case folder.
/// </summary>
/// <remarks>
/// SIARA serves documents in per-case folders shaped <c>/document_store/{caseId}/{fileName}</c>.
/// A single case may contain up to three companion files. The downloader emits ONE event per
/// <see cref="SiaraCase"/> rather than one event per file, so the extraction pipeline always sees
/// a full set of sources for a case (MVP-PATH 2.1).
/// </remarks>
public sealed record SiaraCase
{
    /// <summary>
    /// Gets the case identifier derived from the SIARA URL path segment immediately before the file name
    /// (e.g. <c>CASE1</c> for <c>/document_store/CASE1/a.pdf</c>).
    /// </summary>
    public required string CaseId { get; init; }

    /// <summary>
    /// Gets the companion files that belong to this case.
    /// </summary>
    public required IReadOnlyList<DownloadableFile> Files { get; init; }
}

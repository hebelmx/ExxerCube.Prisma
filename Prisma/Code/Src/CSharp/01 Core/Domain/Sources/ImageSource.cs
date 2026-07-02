namespace ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Represents a document that has been rasterised to page images suitable for vision-LLM extraction.
/// Each element in <see cref="PagePngs"/> is a PNG byte array for one page, in order.
/// </summary>
/// <param name="DocumentId">
/// Logical identifier for the originating document (e.g. a storage key or case-file ID).
/// Used for logging and correlation; not used in extraction.
/// </param>
/// <param name="PagePngs">
/// Ordered list of raw PNG bytes, one per page. Must not be empty when passed to an extractor.
/// </param>
public sealed record ImageSource(string DocumentId, IReadOnlyList<byte[]> PagePngs);

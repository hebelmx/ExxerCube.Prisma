namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Structured representation of a client name extracted from a VEC statement header.
/// </summary>
/// <remarks>
/// The PDF renders the client name as a single run of words with no delimiter between
/// given names and family names.  The extractor captures the full name verbatim;
/// splitting into first/last names is best-effort and may be <see langword="null"/>
/// when no reliable heuristic applies.
/// </remarks>
/// <param name="FullName">
/// The complete name exactly as it appears in the statement (all-caps, space-separated tokens),
/// e.g. <c>"GUADALUPE PEPITA PEPITA"</c>.
/// </param>
/// <param name="FirstNames">
/// Best-effort first (given) name(s), or <see langword="null"/> if the split cannot be determined.
/// </param>
/// <param name="LastNames">
/// Best-effort last (family) name(s), or <see langword="null"/> if the split cannot be determined.
/// </param>
public sealed record ExtractedClientName(
    string FullName,
    string? FirstNames,
    string? LastNames);

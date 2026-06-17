namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Structured representation of a mailing address extracted from a VEC statement header.
/// </summary>
/// <remarks>
/// The PDF renders the address across several lines in the right-hand column of the header.
/// The extractor captures each address component that it can reliably identify via
/// heuristics (postal-code pattern, known state abbreviations, etc.).
/// Components that cannot be reliably identified are left <see langword="null"/>.
/// </remarks>
/// <param name="RawAddress">
/// The address lines concatenated (space-separated), exactly as read from the PDF,
/// e.g. <c>"PARQUE LIRA S/N SAN MIGUEL CHAPULTEPEC 11850 CDMX, CDMX C.R.00001"</c>.
/// </param>
/// <param name="StreetAndNumber">
/// Street name and number if identifiable, e.g. <c>"PARQUE LIRA S/N"</c>.
/// </param>
/// <param name="Neighborhood">
/// Neighborhood / colonia if identifiable, e.g. <c>"SAN MIGUEL CHAPULTEPEC"</c>.
/// </param>
/// <param name="PostalCode">
/// 5-digit postal code if identifiable, e.g. <c>"11850"</c>.
/// </param>
/// <param name="State">
/// State abbreviation or name if identifiable, e.g. <c>"CDMX"</c>.
/// </param>
public sealed record ExtractedAddress(
    string RawAddress,
    string? StreetAndNumber,
    string? Neighborhood,
    string? PostalCode,
    string? State);

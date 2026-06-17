using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Structured model extracted from a VEC (Estado de Cuenta) statement PDF.
/// Produced by the extraction pipeline (Story 3.1 header fields; Story 3.2 period/summary fields).
/// </summary>
/// <remarks>
/// <para>
/// Every header field is wrapped in <see cref="ExtractedField{T}"/> so that callers can inspect
/// <c>Confidence</c>, <c>Locator</c>, and <c>Status</c> independently of the value itself.
/// A field that was not found in the PDF is represented as
/// <see cref="ExtractionStatus.NotExtracted"/> with a best-effort page-level locator hint —
/// it is never silently absent.
/// </para>
/// <para>
/// The <see cref="PeriodSummary"/> property is reserved for Story 3.2 and is always
/// <see langword="null"/> in Story 3.1 output; checklist checks must guard on this.
/// </para>
/// </remarks>
public sealed class StatementModel
{
    /// <summary>
    /// Initializes a <see cref="StatementModel"/> with all extracted header identity fields.
    /// </summary>
    /// <param name="clientName">Extracted client name (full + best-effort split).</param>
    /// <param name="address">Extracted mailing address (raw + best-effort decomposition).</param>
    /// <param name="branchNumber">Branch number (<c>Número de sucursal</c>).</param>
    /// <param name="cardNumber">Card number normalized to 16 digits (<c>Número de Tarjeta</c>).</param>
    /// <param name="clabe">CLABE interbank number normalized to 18 digits (<c>CLABE Interbancaria</c>).</param>
    /// <param name="clientNumber">Client number (<c>Número de cliente</c>).</param>
    /// <param name="rfc">RFC tax registration number.</param>
    public StatementModel(
        ExtractedField<ExtractedClientName> clientName,
        ExtractedField<ExtractedAddress> address,
        ExtractedField<string> branchNumber,
        ExtractedField<string> cardNumber,
        ExtractedField<string> clabe,
        ExtractedField<string> clientNumber,
        ExtractedField<string> rfc)
    {
        ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));
        Address = address ?? throw new ArgumentNullException(nameof(address));
        BranchNumber = branchNumber ?? throw new ArgumentNullException(nameof(branchNumber));
        CardNumber = cardNumber ?? throw new ArgumentNullException(nameof(cardNumber));
        Clabe = clabe ?? throw new ArgumentNullException(nameof(clabe));
        ClientNumber = clientNumber ?? throw new ArgumentNullException(nameof(clientNumber));
        Rfc = rfc ?? throw new ArgumentNullException(nameof(rfc));
    }

    // -----------------------------------------------------------------------
    // Header — identity fields (Story 3.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Client name as it appears in the statement header.
    /// The <see cref="ExtractedClientName.FullName"/> is verbatim; first/last name split is best-effort.
    /// </summary>
    public ExtractedField<ExtractedClientName> ClientName { get; }

    /// <summary>
    /// Mailing address as it appears across the address lines in the statement header.
    /// <see cref="ExtractedAddress.RawAddress"/> is the full verbatim text;
    /// the decomposed components are best-effort.
    /// </summary>
    public ExtractedField<ExtractedAddress> Address { get; }

    /// <summary>
    /// Branch number (<c>Número de sucursal</c>).
    /// Value is the numeric string as printed (e.g. <c>"910"</c>).
    /// </summary>
    public ExtractedField<string> BranchNumber { get; }

    /// <summary>
    /// Card number (<c>Número de Tarjeta</c>) normalized to 16 digits (digits only, spaces removed).
    /// <see cref="ExtractionStatus.ExtractedInvalidFormat"/> is used when the normalized digit count ≠ 16.
    /// </summary>
    public ExtractedField<string> CardNumber { get; }

    /// <summary>
    /// CLABE interbank account number (<c>CLABE Interbancaria</c>) normalized to digits only.
    /// <see cref="ExtractionStatus.ExtractedInvalidFormat"/> is used when the digit count ≠ 18.
    /// </summary>
    public ExtractedField<string> Clabe { get; }

    /// <summary>
    /// Client number (<c>Número de cliente</c>) as printed (e.g. <c>"99887766"</c>).
    /// </summary>
    public ExtractedField<string> ClientNumber { get; }

    /// <summary>
    /// RFC tax registration number.
    /// Must match the pattern <c>^[A-ZÑ&amp;]{3,4}\d{6}[A-Z0-9]{3}$</c>.
    /// <see cref="ExtractionStatus.ExtractedInvalidFormat"/> is used when the pattern is not matched.
    /// </summary>
    public ExtractedField<string> Rfc { get; }

    // -----------------------------------------------------------------------
    // Period / summary section (Story 3.2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Period and summary fields extracted in Story 3.2.
    /// <see langword="null"/> when only the header stage has run (Story 3.1 output).
    /// Checklist checks that depend on period/summary data must guard on this.
    /// </summary>
    /// <remarks>
    /// Populated by the full extraction method (Story 3.2 — <c>ExtractFullAsync</c>),
    /// which extracts both header and period/summary fields in a single pass.
    /// </remarks>
    public PeriodSummary? PeriodSummary { get; init; }

    // -----------------------------------------------------------------------
    // DESGLOSE DE MOVIMIENTOS DEL PERIODO — transaction table (Story 4.4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parsed transaction rows from the "DESGLOSE DE MOVIMIENTOS DEL PERIODO" table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Populated by <c>ExtractFullAsync</c> (Story 4.4).  An empty list combined with
    /// <see cref="MovementsStatus"/> == <see cref="MovementsExtractionStatus.SectionNotFound"/>
    /// indicates the DESGLOSE section was absent from the PDF (e.g. minimal test PDFs).
    /// </para>
    /// <para>
    /// The list is ordered as encountered top-to-bottom across all DESGLOSE pages.
    /// Each row carries a <see cref="StatementMovement.Locator"/> with page number and
    /// bounding-box coordinates.
    /// </para>
    /// </remarks>
    public IReadOnlyList<StatementMovement> Movements { get; init; } = [];

    /// <summary>
    /// Indicates the outcome of extracting the DESGLOSE transaction table (Story 4.4).
    /// </summary>
    /// <remarks>
    /// <see cref="MovementsExtractionStatus.SectionNotFound"/> when the "DESGLOSE DE MOVIMIENTOS"
    /// section header was not found in any page of the PDF.
    /// <see cref="MovementsExtractionStatus.Extracted"/> when at least one row was parsed.
    /// <see cref="MovementsExtractionStatus.NoRowsParsed"/> when the section was found but
    /// no valid data rows could be reconstructed.
    /// </remarks>
    public MovementsExtractionStatus MovementsStatus { get; init; } = MovementsExtractionStatus.SectionNotFound;

    // -----------------------------------------------------------------------
    // Font runs (Story 5.1 — CL-35 embedded-font compliance)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Distinct (normalized-family, page) font runs collected from the PDF text layer.
    /// One entry per unique combination; the <see cref="FontUsage.Locator"/> captures the
    /// first occurrence of that font on that page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Populated by <c>ExtractFullAsync</c> (Story 5.1).  Always non-null; an empty list
    /// combined with <see cref="FontExtractionStatus"/> ==
    /// <see cref="FontExtractionStatus.NotFound"/> indicates a scanned-image PDF with no
    /// text layer.
    /// </para>
    /// </remarks>
    public IReadOnlyList<FontUsage> FontRuns { get; init; } = Array.Empty<FontUsage>();

    /// <summary>
    /// Indicates the outcome of the font-run extraction pass (Story 5.1 — CL-35).
    /// </summary>
    /// <remarks>
    /// <see cref="FontExtractionStatus.Extracted"/> when the PDF text layer contained at least
    /// one letter glyph.  <see cref="FontExtractionStatus.NotFound"/> when no letters were
    /// found (scanned PDF or empty document).
    /// </remarks>
    public FontExtractionStatus FontExtractionStatus { get; init; } = FontExtractionStatus.NotFound;
}

using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

// Normalization contract (Story 6.1 — CL-32/46):
//
// NormalizedFullText is the concatenated text of all PDF pages, transformed to a
// canonical form for legend-presence checks:
//   1. All characters are upper-cased (InvariantCulture).
//   2. Unicode diacritics (accents) are stripped via NFD decomposition followed by
//      removal of non-spacing marks (UnicodeCategory.NonSpacingMark).
//   3. Whitespace runs (spaces, tabs, newlines) are collapsed to a single ASCII space.
//   4. Leading/trailing whitespace is trimmed.
//
// Consequence: "Compara tu Tarjeta" → "COMPARA TU TARJETA"; "Adeúdo" → "ADEUDO".
// Rules must apply the same NormalizeText() transformation to any search string
// before calling String.Contains so that comparisons are consistent.

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
    // Text-overlap incidents (Story 5.2 — CL-28)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Text-overlap incidents detected during extraction (Story 5.2 — CL-28).
    /// Each entry represents a pair of words on the same horizontal band whose
    /// X-extents intersect by more than 2.0 PDF points (the extraction epsilon).
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty list indicates no overlapping words were found across any page.
    /// Normal kerning (glyphs touching but not crossing) does NOT produce incidents.
    /// </para>
    /// <para>
    /// Populated by <c>ExtractFullAsync</c> (Story 5.2).  Always non-null.
    /// </para>
    /// </remarks>
    public IReadOnlyList<TextOverlapIncident> TextOverlapIncidents { get; init; } = [];

    // -----------------------------------------------------------------------
    // Section-header styles (Story 5.2 — CL-29)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Typographic style of each detected section header (Story 5.2 — CL-29).
    /// Headers are identified by matching known Spanish section-title strings
    /// (case-insensitive) against page text on every page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty list indicates none of the known section titles were found in the PDF.
    /// Rules that depend on this list must treat an empty result as
    /// <c>InsufficientData</c> rather than Pass.
    /// </para>
    /// <para>
    /// Populated by <c>ExtractFullAsync</c> (Story 5.2).  Always non-null.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SectionHeaderStyle> SectionHeaderStyles { get; init; } = [];

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

    // -----------------------------------------------------------------------
    // Normalized full text (Story 6.1 — CL-32/46 legend presence checks)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Normalized concatenation of all page text in the statement PDF, suitable for
    /// legend-presence substring checks (CL-32, CL-46).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Normalization (applied by the extraction stage — see comment block at top of file):
    /// upper-cased, accent-stripped (NFD + remove non-spacing marks), whitespace collapsed to
    /// a single ASCII space. Never <see langword="null"/>; empty string when the PDF has no
    /// text layer or contains no words.
    /// </para>
    /// <para>
    /// Rules that search this property must apply the same normalization to any search string
    /// before calling <see cref="string.Contains(string, System.StringComparison)"/>
    /// with <see cref="System.StringComparison.Ordinal"/>.
    /// </para>
    /// </remarks>
    public string NormalizedFullText { get; init; } = string.Empty;

    // -----------------------------------------------------------------------
    // Fiscal block (Story 6.2 — CL-50..53)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fiscal block facts extracted from the CFDI legend page (Story 6.2 — CL-50..53).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> when the full extraction pass has not run (<c>ExtractHeaderAsync</c> only).
    /// When populated by <c>ExtractFullAsync</c>, <see cref="FiscalBlock.BlockPresent"/> is
    /// <see langword="false"/> when no CFDI legend page is found in the document —
    /// it is never <see langword="null"/> after the full extraction pass.
    /// </para>
    /// </remarks>
    public FiscalBlock? FiscalBlock { get; init; }

    // -----------------------------------------------------------------------
    // Per-page inspection facts (Story 5.3, extended Story 10.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Total number of pages in the statement PDF.
    /// Zero when the extraction stage has not run.
    /// </summary>
    /// <remarks>
    /// Populated by <c>ExtractFullAsync</c> (Story 5.3). Set to <c>doc.NumberOfPages</c>.
    /// </remarks>
    public int PageCount { get; init; }

    /// <summary>
    /// Per-page structural metadata collected during the full extraction pass (Story 5.3).
    /// Each entry corresponds to one page of the PDF (ordered by page number, 1-based).
    /// <see cref="PageInspectionFacts.Width"/> and <see cref="PageInspectionFacts.Height"/>
    /// (PDF points) are populated from Story 10.1 onward; they are zero in earlier extractions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty list indicates the extraction stage did not run the per-page pass.
    /// </para>
    /// <para>
    /// Populated by <c>ExtractFullAsync</c> (Story 5.3). Always non-null.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PageInspectionFacts> Pages { get; init; } = [];

    // -----------------------------------------------------------------------
    // Mandatory CONDUSEF section map (Story 10.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Detection results for the 28 mandatory CONDUSEF <i>Acuerdo</i> sections (Story 10.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Populated by <c>ExtractFullAsync</c> (Story 10.1).  An empty list indicates either:
    /// <list type="bullet">
    ///   <item>The extraction stage predates Story 10.1 and did not run the section-detection pass.</item>
    ///   <item>The text layer was unreadable (no words on any page).</item>
    /// </list>
    /// </para>
    /// <para>
    /// When populated, the list always contains exactly 28 entries — one per Acuerdo section
    /// (§1–§28) — ordered by <see cref="DetectedSection.SectionNumber"/>.
    /// Rules that consume this list must guard on <c>Sections.Count == 0</c> and return
    /// <c>InsufficientData</c> rather than Fail.
    /// </para>
    /// <para>
    /// Always non-null; empty list (not null) when the pass did not run.
    /// </para>
    /// </remarks>
    public IReadOnlyList<DetectedSection> Sections { get; init; } = [];
}

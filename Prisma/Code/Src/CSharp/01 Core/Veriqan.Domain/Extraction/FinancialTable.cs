namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// A reconstructed regulatory financial table from a VEC credit-card statement section
/// (§8, §19, §20, or §16).
/// </summary>
/// <remarks>
/// <para>
/// Column structure and row label semantics are section-specific.  Consumers should
/// check <see cref="SectionNumber"/> to interpret <see cref="TableRow.Values"/> in order.
/// </para>
/// <para>
/// When <see cref="Status"/> is not <see cref="TableExtractionStatus.Extracted"/>,
/// <see cref="Rows"/> may be empty. Validation rules MUST abstain (InsufficientData)
/// for any status other than Extracted — a wrong cell can cause a false bank-non-compliant
/// verdict.
/// </para>
/// </remarks>
public sealed class FinancialTable
{
    /// <summary>
    /// Initializes a <see cref="FinancialTable"/>.
    /// </summary>
    public FinancialTable(
        int sectionNumber,
        string sectionName,
        TableExtractionStatus status,
        IReadOnlyList<TableRow> rows,
        FieldLocator locator)
    {
        ArgumentNullException.ThrowIfNull(sectionName);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(locator);

        SectionNumber = sectionNumber;
        SectionName = sectionName;
        Status = status;
        Rows = rows;
        Locator = locator;
    }

    /// <summary>CONDUSEF Acuerdo section numeral (8, 16, 19, or 20).</summary>
    public int SectionNumber { get; }

    /// <summary>Canonical Spanish section title.</summary>
    public string SectionName { get; }

    /// <summary>Outcome of the table reconstruction attempt.</summary>
    public TableExtractionStatus Status { get; }

    /// <summary>
    /// Reconstructed rows in document reading order.
    /// Empty when <see cref="Status"/> is not <see cref="TableExtractionStatus.Extracted"/>.
    /// </summary>
    public IReadOnlyList<TableRow> Rows { get; }

    /// <summary>Source locator of the section heading in the PDF.</summary>
    public FieldLocator Locator { get; }

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    /// <summary>Returns a not-found table (section heading absent from the document).</summary>
    public static FinancialTable NotFound(int sectionNumber, string sectionName) =>
        new(sectionNumber, sectionName, TableExtractionStatus.SectionNotFound, [], FieldLocator.NoPage());

    /// <summary>Returns an indeterminate table (section found but reconstruction not reliable).</summary>
    public static FinancialTable Indeterminate(int sectionNumber, string sectionName, FieldLocator locator) =>
        new(sectionNumber, sectionName, TableExtractionStatus.Indeterminate, [], locator);

    /// <summary>Returns a no-rows-parsed table (section heading found but no rows reconstructed).</summary>
    public static FinancialTable NoRows(int sectionNumber, string sectionName, FieldLocator locator) =>
        new(sectionNumber, sectionName, TableExtractionStatus.NoRowsParsed, [], locator);
}

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// A single cell in a <see cref="TableRow"/>, carrying raw text, a parsed decimal
/// value (when applicable), cell kind, extraction confidence, and source locator.
/// </summary>
/// <remarks>
/// Confidence conventions (aligned with <see cref="ExtractedField{T}"/>):
/// <list type="bullet">
///   <item>1.0 — text found, value parsed successfully or kind is Label/NotApplicable/Empty.</item>
///   <item>0.7 — text found but numeric parse failed (raw text preserved).</item>
///   <item>0.0 — cell region is structurally absent.</item>
/// </list>
/// </remarks>
/// <param name="RawText">Raw text as extracted from the PDF (trimmed). Empty string when absent.</param>
/// <param name="ParsedValue">Parsed decimal value. Non-null only when <see cref="Kind"/> is Amount, Rate, or Days.</param>
/// <param name="Kind">Semantic kind of this cell.</param>
/// <param name="Confidence">Extraction confidence [0.0, 1.0].</param>
/// <param name="Locator">Source location in the PDF.</param>
public sealed record TableCell(
    string RawText,
    decimal? ParsedValue,
    CellKind Kind,
    double Confidence,
    FieldLocator Locator)
{
    /// <summary>
    /// Creates a cell whose amount was parsed successfully.
    /// </summary>
    public static TableCell Amount(decimal value, string rawText, FieldLocator locator) =>
        new(rawText, value, CellKind.Amount, 1.0, locator);

    /// <summary>Creates a cell whose rate was parsed successfully (value stored as decimal fraction, e.g. 0.2736 for 27.36%).</summary>
    public static TableCell Rate(decimal value, string rawText, FieldLocator locator) =>
        new(rawText, value, CellKind.Rate, 1.0, locator);

    /// <summary>Creates a days-count cell.</summary>
    public static TableCell Days(decimal value, string rawText, FieldLocator locator) =>
        new(rawText, value, CellKind.Days, 1.0, locator);

    /// <summary>Creates a label cell (column or row header text).</summary>
    public static TableCell LabelCell(string text, FieldLocator locator) =>
        new(text, null, CellKind.Label, 1.0, locator);

    /// <summary>Creates a "NA" cell (bank says not applicable).</summary>
    public static TableCell NotApplicableCell(string rawText, FieldLocator locator) =>
        new(rawText, null, CellKind.NotApplicable, 1.0, locator);

    /// <summary>Creates an empty cell (column present, no text found).</summary>
    public static TableCell EmptyCell(FieldLocator locator) =>
        new(string.Empty, null, CellKind.Empty, 1.0, locator);

    /// <summary>Creates a cell where text was found but numeric parse failed.</summary>
    public static TableCell ParseFailure(string rawText, FieldLocator locator) =>
        new(rawText, null, CellKind.Amount, 0.7, locator);

    /// <summary>Creates a missing cell (column not populated at all).</summary>
    public static TableCell Missing(FieldLocator locator) =>
        new(string.Empty, null, CellKind.Empty, 0.0, locator);
}

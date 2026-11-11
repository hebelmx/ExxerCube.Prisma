namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the structure of tables within a DOCX document.
/// </summary>
public sealed class DocxTableStructure
{
    /// <summary>
    /// Gets or sets the number of tables in the document.
    /// </summary>
    public int TableCount { get; set; }

    /// <summary>
    /// Gets or sets the number of rows in the primary table.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Gets or sets the number of columns in the primary table.
    /// </summary>
    public int ColumnCount { get; set; }

    /// <summary>
    /// Gets or sets whether the first row appears to be headers.
    /// Determined by styling (bold, different background) or position.
    /// </summary>
    public bool HasHeaderRow { get; set; }

    /// <summary>
    /// Gets or sets the detected column headers.
    /// Null if no header row or unable to detect.
    /// </summary>
    public string[]? ColumnHeaders { get; set; }
}
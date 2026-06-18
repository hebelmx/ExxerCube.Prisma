namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>The semantic kind of a table cell in a financial regulatory grid.</summary>
public enum CellKind
{
    /// <summary>Row label text (never a numeric value).</summary>
    Label,
    /// <summary>A parsed currency amount (may be positive or negative).</summary>
    Amount,
    /// <summary>An annual interest rate (e.g. 27.36%).</summary>
    Rate,
    /// <summary>A count of days.</summary>
    Days,
    /// <summary>The cell text is literally "NA" (not applicable per bank).</summary>
    NotApplicable,
    /// <summary>Cell is structurally present but blank/empty in the PDF.</summary>
    Empty,
}

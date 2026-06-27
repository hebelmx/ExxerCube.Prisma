namespace ExxerCube.Prisma.Veriqan.Web.UI.Models;

/// <summary>
/// View-model for the header/balance fields extracted from a bank statement during the
/// Veriqan ingestion and extraction stages (demo capture 1).
/// </summary>
public sealed class DemoExtractedFields
{
    /// <summary>Gets or sets the institution name printed on the statement.</summary>
    public string BankName { get; init; } = string.Empty;

    /// <summary>Gets or sets the masked card/account number (e.g. "****9879").</summary>
    public string MaskedAccount { get; init; } = string.Empty;

    /// <summary>Gets or sets the product type (e.g. "Tarjeta de Crédito Visa").</summary>
    public string ProductType { get; init; } = string.Empty;

    /// <summary>Gets or sets the billing period covered (e.g. "2026-03-15 – 2026-04-14").</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Gets or sets the cut date of the statement.</summary>
    public DateOnly CutDate { get; init; }

    /// <summary>Gets or sets the opening balance in MXN.</summary>
    public decimal OpeningBalance { get; init; }

    /// <summary>Gets or sets the closing balance in MXN.</summary>
    public decimal ClosingBalance { get; init; }

    /// <summary>Gets or sets the total number of movement rows on the statement.</summary>
    public int MovementCount { get; init; }

    /// <summary>Gets or sets the SHA-256 content hash of the submitted PDF.</summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>Gets or sets the page count of the PDF.</summary>
    public int PageCount { get; init; }
}

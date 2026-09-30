namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a financial account/product reference tied to a measure.
/// </summary>
public sealed class Cuenta
{
    /// <summary>
    /// Gets or sets the account number.
    /// </summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bank name.
    /// </summary>
    public string Banco { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the branch/sucursal identifier.
    /// </summary>
    public string Sucursal { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product type/name.
    /// </summary>
    public string Producto { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the currency code.
    /// </summary>
    public string Moneda { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the monetary amount if specified.
    /// </summary>
    public decimal? Monto { get; set; }

    /// <summary>
    /// Gets or sets the amount qualifier (e.g., Garantia).
    /// </summary>
    public string TipoMonto { get; set; } = string.Empty;
}

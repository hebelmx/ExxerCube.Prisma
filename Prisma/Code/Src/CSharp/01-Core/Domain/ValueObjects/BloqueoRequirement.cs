namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Asset freeze (bloqueo/aseguramiento) requirement details.
/// </summary>
public class BloqueoRequirement
{
    /// <summary>
    /// Whether asset freeze is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Whether freeze is partial (specific amount) or total.
    /// </summary>
    public bool EsParcial { get; set; }

    /// <summary>
    /// Amount to freeze (if partial).
    /// </summary>
    public decimal? Monto { get; set; }

    /// <summary>
    /// Currency of amount.
    /// </summary>
    public string? Moneda { get; set; }

    /// <summary>
    /// Specific accounts to freeze (if specified).
    /// </summary>
    public List<string> CuentasEspecificas { get; set; } = new();

    /// <summary>
    /// Specific products to freeze (if specified).
    /// </summary>
    public List<string> ProductosEspecificos { get; set; } = new();

    /// <summary>
    /// Confidence score of the classification (0.0 to 1.0).
    /// Represents how confident the classifier is that this requirement exists.
    /// </summary>
    public double Confidence { get; set; }
}
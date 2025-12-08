namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Fund transfer requirement details.
/// </summary>
public class TransferenciaRequirement
{
    /// <summary>
    /// Whether transfer is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Destination account for transfer (if specified).
    /// </summary>
    public string? CuentaDestino { get; set; }

    /// <summary>
    /// Amount to transfer (if specified).
    /// </summary>
    public decimal? Monto { get; set; }

    /// <summary>
    /// Confidence score of the classification (0.0 to 1.0).
    /// Represents how confident the classifier is that this requirement exists.
    /// </summary>
    public double Confidence { get; set; }
}
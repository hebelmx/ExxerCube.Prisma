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
}
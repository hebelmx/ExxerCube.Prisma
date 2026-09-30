namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Specific document requirement.
/// </summary>
public class DocumentoRequerido
{
    /// <summary>
    /// Type of document (e.g., "Estado de cuenta", "ID del cliente").
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Period start date (for statements, transactions, etc.).
    /// </summary>
    public DateTime? PeriodoInicio { get; set; }

    /// <summary>
    /// Period end date (for statements, transactions, etc.).
    /// </summary>
    public DateTime? PeriodoFin { get; set; }
}
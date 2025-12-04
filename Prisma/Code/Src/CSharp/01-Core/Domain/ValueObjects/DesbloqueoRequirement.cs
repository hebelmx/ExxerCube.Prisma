namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Asset unfreeze (desbloqueo) requirement details.
/// </summary>
public class DesbloqueoRequirement
{
    /// <summary>
    /// Whether asset unfreeze is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Reference to original freeze case (expediente).
    /// </summary>
    public string? ExpedienteBloqueoOriginal { get; set; }
}
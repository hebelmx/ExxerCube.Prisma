namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// General information request requirement details.
/// </summary>
public class InformacionGeneralRequirement
{
    /// <summary>
    /// Whether general information is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Description of information requested.
    /// </summary>
    public string? InformacionSolicitada { get; set; }
}
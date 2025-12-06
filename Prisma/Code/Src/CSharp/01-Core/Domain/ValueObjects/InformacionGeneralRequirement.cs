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

    /// <summary>
    /// Confidence score of the classification (0.0 to 1.0).
    /// Represents how confident the classifier is that this requirement exists.
    /// </summary>
    public double Confidence { get; set; }
}
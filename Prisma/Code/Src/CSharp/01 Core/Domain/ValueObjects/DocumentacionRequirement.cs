namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Documentation submission requirement details.
/// </summary>
public class DocumentacionRequirement
{
    /// <summary>
    /// Whether documentation is required.
    /// </summary>
    public bool EsRequerido { get; set; }

    /// <summary>
    /// Types of documents requested.
    /// </summary>
    public List<DocumentoRequerido> TiposDocumento { get; set; } = new();

    /// <summary>
    /// Confidence score of the classification (0.0 to 1.0).
    /// Represents how confident the classifier is that this requirement exists.
    /// </summary>
    public double Confidence { get; set; }
}
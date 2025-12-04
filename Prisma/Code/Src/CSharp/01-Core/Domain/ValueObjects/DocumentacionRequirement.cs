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
}
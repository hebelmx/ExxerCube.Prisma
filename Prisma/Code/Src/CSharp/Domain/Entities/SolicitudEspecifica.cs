namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a specific request (solicitud especifica) in a regulatory case.
/// </summary>
public class SolicitudEspecifica
{
    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string RequerimientoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request description.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request type.
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SolicitudEspecifica"/> class.
    /// </summary>
    public SolicitudEspecifica()
    {
    }
}


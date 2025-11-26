namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a specific request (solicitud especifica) in a regulatory case.
/// </summary>
/// <remarks>
/// Updated to match real PRP1 XML structure from CNBV fixtures.
/// Contains the specific solicitud ID, instructions for unknown accounts (inmovilización),
/// and the list of persons about whom information is requested.
/// </remarks>
public class SolicitudEspecifica
{
    /// <summary>
    /// Gets or sets the specific request identifier.
    /// </summary>
    /// <remarks>
    /// XML element: &lt;SolicitudEspecificaId&gt; (int, not "RequerimientoId").
    /// </remarks>
    public int SolicitudEspecificaId { get; set; }

    /// <summary>
    /// Gets or sets the instructions for unknown accounts (inmovilización instructions).
    /// </summary>
    /// <remarks>
    /// XML element: &lt;InstruccionesCuentasPorConocer&gt;
    /// Can be very long (500+ characters) containing legal instructions for account freezing.
    /// Example: "For efectos de la inmunización o aseguramiento precautorio..."
    /// </remarks>
    public string InstruccionesCuentasPorConocer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of persons about whom information is requested.
    /// </summary>
    /// <remarks>
    /// XML element: &lt;PersonasSolicitud&gt; (nested collection).
    /// Each person has similar structure to SolicitudParte but in this specific context.
    /// </remarks>
    public List<PersonaSolicitud> PersonasSolicitud { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SolicitudEspecifica"/> class.
    /// </summary>
    public SolicitudEspecifica()
    {
    }
}


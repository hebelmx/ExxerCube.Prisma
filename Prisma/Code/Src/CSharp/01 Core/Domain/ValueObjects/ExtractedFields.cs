namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents structured data extracted from OCR text.
/// </summary>
public class ExtractedFields
{
    /// <summary>
    /// Gets or sets the expediente (file number) extracted from the document.
    /// </summary>
    public string? Expediente { get; set; }

    /// <summary>
    /// Gets or sets the causa (cause) extracted from the document.
    /// </summary>
    public string? Causa { get; set; }

    /// <summary>
    /// Gets or sets the accion solicitada (requested action) extracted from the document.
    /// </summary>
    public string? AccionSolicitada { get; set; }

    /// <summary>
    /// Gets or sets the list of dates extracted from the document.
    /// </summary>
    public List<string> Fechas { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of monetary amounts extracted from the document.
    /// </summary>
    public List<AmountData> Montos { get; set; } = new();

    /// <summary>
    /// Gets or sets additional extracted fields (keyed by field name) beyond the core properties.
    /// </summary>
    public Dictionary<string, string?> AdditionalFields { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractedFields"/> class.
    /// </summary>
    public ExtractedFields()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractedFields"/> class with specified values.
    /// </summary>
    /// <param name="expediente">The expediente value.</param>
    /// <param name="causa">The causa value.</param>
    /// <param name="accionSolicitada">The accion solicitada value.</param>
    /// <param name="fechas">The list of dates.</param>
    /// <param name="montos">The list of monetary amounts.</param>
    /// <param name="additionalFields">Additional extracted fields keyed by name.</param>
    public ExtractedFields(string? expediente, string? causa, string? accionSolicitada, List<string> fechas, List<AmountData> montos, Dictionary<string, string?>? additionalFields = null)
    {
        Expediente = expediente;
        Causa = causa;
        AccionSolicitada = accionSolicitada;
        Fechas = fechas;
        Montos = montos;
        AdditionalFields = additionalFields ?? new Dictionary<string, string?>();
    }
}

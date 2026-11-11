using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a requested document item in an oficio.
/// </summary>
public sealed class DocumentItem
{
    /// <summary>
    /// Gets or sets the requested document type.
    /// </summary>
    public DocumentItemKind Tipo { get; set; } = DocumentItemKind.Unknown;

    /// <summary>
    /// Gets or sets the start date of the requested period, if any.
    /// </summary>
    public DateOnly? PeriodoInicio { get; set; }

    /// <summary>
    /// Gets or sets the end date of the requested period, if any.
    /// </summary>
    public DateOnly? PeriodoFin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a certified copy is required.
    /// </summary>
    public bool? Certificada { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the request.
    /// </summary>
    public string Notas { get; set; } = string.Empty;
}

namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the authority category issuing the directive.
/// </summary>
public enum AuthorityType
{
    /// <summary>Authority not determined.</summary>
    Unknown = 0,
    /// <summary>Comisión Nacional Bancaria y de Valores.</summary>
    CNBV,
    /// <summary>Unidad de Inteligencia Financiera.</summary>
    UIF,
    /// <summary>Court or judicial authority.</summary>
    Juzgado,
    /// <summary>Hacienda/SAT or fiscal authority.</summary>
    Hacienda,
    /// <summary>Authority outside the known list.</summary>
    Other
}

namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Controlled list of CNBV subdivision codes for oficio classification.
/// </summary>
public enum LegalSubdivision
{
    /// <summary>Subdivision not determined.</summary>
    Unknown = 0,
    /// <summary>A/AS Especial Aseguramiento.</summary>
    A_AS,
    /// <summary>A/DE Especial Desembargo.</summary>
    A_DE,
    /// <summary>A/TF Especial Transferencia.</summary>
    A_TF,
    /// <summary>A/IN Especial Informativo - documentación.</summary>
    A_IN,
    /// <summary>J/AS Judicial Aseguramiento.</summary>
    J_AS,
    /// <summary>J/DE Judicial Desembargo.</summary>
    J_DE,
    /// <summary>J/IN Judicial Informativo - documentación.</summary>
    J_IN,
    /// <summary>H/IN Hacendario Informativo - documentación.</summary>
    H_IN,
    /// <summary>E/AS Operaciones ilícitas Aseguramiento.</summary>
    E_AS,
    /// <summary>E/DE Operaciones ilícitas Desembargo.</summary>
    E_DE,
    /// <summary>E/IN Operaciones ilícitas Informativo - documentación.</summary>
    E_IN,
    /// <summary>Subdivision outside the known list.</summary>
    Other
}

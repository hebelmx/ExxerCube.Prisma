namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the Level 1 classification categories for regulatory documents.
/// </summary>
public enum ClassificationLevel1
{
    /// <summary>
    /// Aseguramiento (Asset seizure).
    /// </summary>
    Aseguramiento,

    /// <summary>
    /// Desembargo (Asset release).
    /// </summary>
    Desembargo,

    /// <summary>
    /// Documentacion (Documentation request).
    /// </summary>
    Documentacion,

    /// <summary>
    /// Informacion (Information request).
    /// </summary>
    Informacion,

    /// <summary>
    /// Transferencia (Transfer).
    /// </summary>
    Transferencia,

    /// <summary>
    /// OperacionesIlicitas (Illicit operations).
    /// </summary>
    OperacionesIlicitas
}


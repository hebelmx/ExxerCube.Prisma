namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the intent of a legal measure requested by the authority.
/// </summary>
public enum MeasureType
{
    /// <summary>Measure intent not determined.</summary>
    Unknown = 0,
    /// <summary>Block/aseguramiento of assets or accounts.</summary>
    Block,               // Bloqueo / Aseguramiento
    /// <summary>Unblock/desembargo of assets or accounts.</summary>
    Unblock,             // Desbloqueo / Desembargo
    /// <summary>Transfer of funds between accounts.</summary>
    TransferFunds,       // Transferencia de fondos
    /// <summary>Documentation request.</summary>
    Documentation,       // Documentación solicitada
    /// <summary>General information request.</summary>
    Information,         // Información general
    /// <summary>Future or unmatched measure type.</summary>
    Other                // Future or unmatched measure type
}

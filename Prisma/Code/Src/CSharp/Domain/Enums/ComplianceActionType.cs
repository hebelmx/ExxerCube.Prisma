namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the type of compliance action to be taken based on legal directive classification.
/// </summary>
public enum ComplianceActionType
{
    /// <summary>
    /// Block action (bloqueo).
    /// </summary>
    Block,

    /// <summary>
    /// Unblock action (desbloqueo).
    /// </summary>
    Unblock,

    /// <summary>
    /// Document action (documentación).
    /// </summary>
    Document,

    /// <summary>
    /// Transfer action (transferencia).
    /// </summary>
    Transfer,

    /// <summary>
    /// Information action (información).
    /// </summary>
    Information,

    /// <summary>
    /// Ignore action (ignorar).
    /// </summary>
    Ignore
}


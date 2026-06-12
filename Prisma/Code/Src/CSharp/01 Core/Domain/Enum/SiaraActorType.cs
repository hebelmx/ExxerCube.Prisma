namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Classifies the kind of actor that acquired a SIARA session, for audit and non-repudiation (ADR-010 P2).
/// </summary>
/// <remarks>
/// Used by SiaraActor to distinguish a human operator riding an interactive session from an unattended
/// service account running AutomatedLogin. The distinction drives alerting and compliance trail shape.
/// </remarks>
public enum SiaraActorType
{
    /// <summary>
    /// A human operator who interacted with the browser (for example, the interactive-login mode where a
    /// person types their credentials into SIARA's own page).
    /// </summary>
    User = 0,

    /// <summary>
    /// An unattended service account, typically sourced from a secret vault and used by the AutomatedLogin
    /// or SessionPassthrough modes running in a scheduled or watch-loop context.
    /// </summary>
    ServiceAccount = 1,
}

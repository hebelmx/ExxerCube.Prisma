namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Outcome of a single compliance check within a <see cref="Entities.Finding"/>.
/// </summary>
public enum FindingVerdict
{
    /// <summary>The check passed; observed value matches the expected value.</summary>
    Pass = 0,

    /// <summary>The check failed; observed value violates the rule.</summary>
    Fail = 1,

    /// <summary>
    /// The check could not be evaluated due to insufficient data in the submitted content.
    /// </summary>
    InsufficientData = 2,
}

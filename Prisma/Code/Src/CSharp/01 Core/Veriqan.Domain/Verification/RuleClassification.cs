namespace ExxerCube.Prisma.Veriqan.Domain.Verification;

/// <summary>
/// Classifies the override-flexibility of a validation rule with respect to
/// tenant customisation. Governs how the tenant-profile overlay (Story 9.3b) may adjust the
/// rule's effective parameters.
/// </summary>
public enum RuleClassification
{
    /// <summary>
    /// The law fixes this check. A tenant may NOT loosen, relax, or alter it in any way.
    /// Override attempts are rejected and the legal-baseline value is always applied.
    /// </summary>
    BaselineLocked = 0,

    /// <summary>
    /// A tenant may make this rule STRICTER (e.g. tighten a tolerance toward zero) but
    /// never looser than the legal floor set by the CONDUSEF Acuerdo.
    /// Any override that would loosen beyond the legal floor is rejected.
    /// </summary>
    TenantTightenableOnly = 1,

    /// <summary>
    /// A tenant may adjust this rule's parameters within the legal bounds in either direction
    /// (both tightening and loosening are permitted, subject to the [Min, Max] range defined
    /// in the <see cref="ExxerCube.Prisma.Veriqan.Domain.Tolerances.Tolerance"/> specification).
    /// </summary>
    TenantOverridable = 2,
}

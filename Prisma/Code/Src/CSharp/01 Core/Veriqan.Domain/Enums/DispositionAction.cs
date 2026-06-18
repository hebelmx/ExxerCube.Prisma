namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// The human-reviewer decision recorded in a disposition audit row.
/// </summary>
/// <remarks>
/// <para>
/// A disposition is <b>always</b> a deliberate human act — VEC never auto-accepts or
/// auto-rejects. The <c>Disposition</c> entity enforces a non-empty <c>Actor</c> field
/// to guarantee this invariant at the domain level.
/// </para>
/// </remarks>
public enum DispositionAction
{
    /// <summary>
    /// The reviewer has accepted the finding or statement verdict as-is.
    /// This does not clear the underlying finding result but records that a human
    /// has reviewed and approved it.
    /// </summary>
    Accept = 0,

    /// <summary>
    /// The reviewer has rejected (overridden) the finding or statement verdict.
    /// The rejection is advisory — downstream handling of the override is the
    /// responsibility of the calling workflow.
    /// </summary>
    Reject = 1,
}

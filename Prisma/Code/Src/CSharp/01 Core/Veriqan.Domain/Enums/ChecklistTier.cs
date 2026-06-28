namespace ExxerCube.Prisma.Veriqan.Domain.Enums;

/// <summary>
/// Classifies a compliance check into its regulatory or contractual tier.
/// </summary>
/// <remarks>
/// <para>
/// Tier membership is DATA in the per-tenant reference bundle — it is not hard-coded on
/// rules. The canonical source is <c>checklist-tiers.csv</c> in the bundle directory.
/// </para>
/// <para>
/// Callers that look up a CheckId not present in the tier map must treat the missing key
/// as <see cref="Condusef"/> (conservative default: an unmapped rule counts toward the
/// regulatory floor so it is never silently dropped from a RED outcome).
/// </para>
/// </remarks>
public enum ChecklistTier
{
    /// <summary>
    /// The check belongs exclusively to the bank's own improvement-opportunity ruleset.
    /// A failure escalates the bank-tier verdict to Yellow (non-blocking, improvement opportunity).
    /// The CONDUSEF regulatory tier is not affected.
    /// </summary>
    Bank = 0,

    /// <summary>
    /// The check is mandated by CONDUSEF regulation (the legal compliance floor).
    /// A failure escalates the CONDUSEF-tier verdict to Red.
    /// </summary>
    Condusef = 1,

    /// <summary>
    /// The check belongs to both tiers simultaneously.
    /// A failure escalates the CONDUSEF-tier verdict to Red
    /// <em>and</em> the bank-tier verdict to Yellow.
    /// </summary>
    Both = 2,
}

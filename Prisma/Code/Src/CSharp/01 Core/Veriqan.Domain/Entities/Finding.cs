using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Domain.Entities;

/// <summary>
/// A single compliance check result produced by the verification engine for a <see cref="VerificationJob"/>.
/// </summary>
public sealed class Finding
{
    /// <summary>
    /// Initializes a new instance of <see cref="Finding"/> for EF Core materialization.
    /// </summary>
    private Finding() { }

    /// <summary>
    /// Initializes a new <see cref="Finding"/>.
    /// </summary>
    /// <param name="id">Unique finding identifier.</param>
    /// <param name="verificationJobId">Parent job identifier.</param>
    /// <param name="checkId">Check rule identifier (e.g. "CL-21").</param>
    /// <param name="verdict">Outcome of the check.</param>
    /// <param name="engineVersion">Version of the verification engine that produced this finding.</param>
    /// <param name="expected">Expected value, if applicable.</param>
    /// <param name="observed">Observed value, if applicable.</param>
    /// <param name="tier">
    /// Regulatory tier this check belongs to (Story 1.4).
    /// Defaults to <c>ChecklistTier.Condusef</c> — the conservative fallback when no tier map
    /// is available, ensuring unmapped checks are never silently dropped from a RED outcome.
    /// </param>
    public Finding(
        Guid id,
        Guid verificationJobId,
        string checkId,
        FindingVerdict verdict,
        string engineVersion,
        string? expected = null,
        string? observed = null,
        ChecklistTier tier = ChecklistTier.Condusef)
    {
        ArgumentNullException.ThrowIfNull(checkId);
        ArgumentNullException.ThrowIfNull(engineVersion);
        Id = id;
        VerificationJobId = verificationJobId;
        CheckId = checkId;
        Verdict = verdict;
        EngineVersion = engineVersion;
        Expected = expected;
        Observed = observed;
        Tier = tier;
    }

    /// <summary>Gets the unique identifier for this finding.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the identifier of the parent <see cref="VerificationJob"/>.</summary>
    public Guid VerificationJobId { get; private set; }

    /// <summary>
    /// Gets the compliance check rule identifier (e.g. "CL-21", "AML-07").
    /// </summary>
    public string CheckId { get; private set; } = string.Empty;

    /// <summary>Gets the verdict for this individual check.</summary>
    public FindingVerdict Verdict { get; private set; }

    /// <summary>Gets the expected value declared by the check rule, if applicable.</summary>
    public string? Expected { get; private set; }

    /// <summary>Gets the value actually observed in the submitted content, if applicable.</summary>
    public string? Observed { get; private set; }

    /// <summary>Gets the semantic version of the verification engine that ran this check.</summary>
    public string EngineVersion { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the regulatory tier this check belongs to (Story 1.4).
    /// </summary>
    /// <remarks>
    /// Populated from the per-tenant checklist-tier map when available; defaults to
    /// <c>ChecklistTier.Condusef</c> when the map is absent or the check ID is not mapped
    /// (conservative default — unmapped checks count toward the regulatory floor).
    /// Pre-existing rows (before migration <c>AddTwoTierVerdictColumns</c>) carry the DB
    /// default of <c>Condusef</c> (integer value 1).
    /// </remarks>
    public ChecklistTier Tier { get; private set; }
}

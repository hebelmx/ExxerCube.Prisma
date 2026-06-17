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
    public Finding(
        Guid id,
        Guid verificationJobId,
        string checkId,
        FindingVerdict verdict,
        string engineVersion,
        string? expected = null,
        string? observed = null)
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
}

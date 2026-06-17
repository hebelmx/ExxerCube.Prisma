namespace ExxerCube.Prisma.Veriqan.Domain.Entities;

/// <summary>
/// Aggregate root representing a single content-verification job.
/// A job is created when content is submitted for compliance verification.
/// </summary>
public sealed class VerificationJob
{
    /// <summary>
    /// Initializes a new instance of <see cref="VerificationJob"/> for EF Core materialization.
    /// </summary>
    private VerificationJob() { }

    /// <summary>
    /// Initializes a new <see cref="VerificationJob"/>.
    /// </summary>
    /// <param name="id">Unique job identifier.</param>
    /// <param name="contentHash">SHA-256 hash of the submitted content (unique per job).</param>
    /// <param name="receivedAtUtc">UTC timestamp when the job was received.</param>
    /// <param name="status">Initial processing status.</param>
    public VerificationJob(Guid id, string contentHash, DateTimeOffset receivedAtUtc, VerificationJobStatus status)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        Id = id;
        ContentHash = contentHash;
        ReceivedAtUtc = receivedAtUtc;
        Status = status;
    }

    /// <summary>Gets the unique identifier for this verification job.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the SHA-256 hash of the submitted content.
    /// This is unique across all jobs — duplicate content is deduplicated by the application layer.
    /// </summary>
    public string ContentHash { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when this job was received by the system.</summary>
    public DateTimeOffset ReceivedAtUtc { get; private set; }

    /// <summary>Gets the current processing status of this job.</summary>
    public VerificationJobStatus Status { get; private set; }

    /// <summary>Gets the collection of compliance findings produced for this job.</summary>
    public IReadOnlyCollection<Finding> Findings => _findings.AsReadOnly();

    private readonly List<Finding> _findings = [];

    /// <summary>Gets the rollup verdict for this job, if scoring has completed.</summary>
    public JobVerdict? Verdict { get; private set; }

    /// <summary>
    /// Transitions the job status to the specified value.
    /// </summary>
    /// <param name="newStatus">Target status.</param>
    public void SetStatus(VerificationJobStatus newStatus) => Status = newStatus;
}

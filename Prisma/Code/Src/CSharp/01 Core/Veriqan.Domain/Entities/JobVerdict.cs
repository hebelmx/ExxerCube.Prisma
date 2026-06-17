namespace ExxerCube.Prisma.Veriqan.Domain.Entities;

/// <summary>
/// Rollup verdict record that summarises all <see cref="Finding"/> items for a <see cref="VerificationJob"/>
/// into a single traffic-light signal used by downstream consumers.
/// </summary>
public sealed class JobVerdict
{
    /// <summary>
    /// Initializes a new instance of <see cref="JobVerdict"/> for EF Core materialization.
    /// </summary>
    private JobVerdict() { }

    /// <summary>
    /// Initializes a new <see cref="JobVerdict"/>.
    /// </summary>
    /// <param name="id">Unique verdict identifier.</param>
    /// <param name="verificationJobId">Parent job identifier.</param>
    /// <param name="signal">Traffic-light signal summarising all findings.</param>
    public JobVerdict(Guid id, Guid verificationJobId, VerdictSignal signal)
    {
        Id = id;
        VerificationJobId = verificationJobId;
        Signal = signal;
    }

    /// <summary>Gets the unique identifier for this verdict record.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the identifier of the parent <see cref="VerificationJob"/>.</summary>
    public Guid VerificationJobId { get; private set; }

    /// <summary>Gets the traffic-light signal summarising all check findings.</summary>
    public VerdictSignal Signal { get; private set; }
}

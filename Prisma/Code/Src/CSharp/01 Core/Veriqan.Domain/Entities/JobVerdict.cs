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

    /// <summary>
    /// Gets the UTC timestamp at which a RED-alert email was sent for this verdict,
    /// or <see langword="null"/> when no alert has been dispatched yet.
    /// </summary>
    /// <remarks>
    /// Used as an idempotency flag: <c>VecAlertService</c> checks this value before
    /// dispatching an email.  When already set it skips the send entirely, preventing duplicate
    /// alerts on reprocess or retry.  The column is configured as an EF Core concurrency token
    /// so two concurrent retries racing to set it produce an optimistic-concurrency exception
    /// and only one wins.
    /// </remarks>
    public DateTimeOffset? AlertSentAt { get; private set; }

    /// <summary>
    /// Records that a RED-alert email has been dispatched at <paramref name="sentAt"/>.
    /// </summary>
    /// <param name="sentAt">The UTC timestamp at which the alert was sent.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="AlertSentAt"/> is already set — callers must check
    /// <see cref="AlertSentAt"/> before calling this method.
    /// </exception>
    public void RecordAlertSent(DateTimeOffset sentAt)
    {
        if (AlertSentAt.HasValue)
            throw new InvalidOperationException(
                $"Alert already recorded at {AlertSentAt.Value:O} for verdict {Id}.");

        AlertSentAt = sentAt;
    }
}

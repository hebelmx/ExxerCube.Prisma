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
    /// <param name="bankTierVerdict">
    /// Bank-tier verdict (Story 1.4). Defaults to <c>VerdictSignal.Green</c> for
    /// pre-migration rows and early-exit blocked paths that carry no tier data.
    /// </param>
    /// <param name="condusefTierVerdict">
    /// CONDUSEF-tier verdict (Story 1.4). Defaults to <c>VerdictSignal.Green</c> for
    /// pre-migration rows; see <c>VerdictSummary.ToJobVerdict</c> for the live mapping.
    /// </param>
    /// <param name="confidence">
    /// Minimum extraction confidence across all <see cref="Finding"/> items for this verdict
    /// (Story 4.1). Ranges from <c>0.0</c> (fully uncertain) to <c>1.0</c> (fully confident).
    /// Defaults to <c>1.0</c> for pre-migration rows and blocked verdicts where no rules ran.
    /// </param>
    /// <param name="engineVersion">
    /// Semantic version of the verification engine that produced this verdict (VERIQAN-E3-S4),
    /// for provenance/audit linkage — mirrors <see cref="Finding.EngineVersion"/>. Defaults to
    /// <c>"unknown"</c> for pre-migration rows and callers that do not supply a real engine
    /// version (e.g. unit-test fixtures).
    /// </param>
    /// <param name="referenceBundleVersion">
    /// Version of the reference-data bundle (<c>BundleMetadata.SchemaVersion</c>) active when
    /// this verdict was computed (VERIQAN-E3-S4), or <see langword="null"/> when no bundle was
    /// resolved for the run (graceful-degradation path — see
    /// <c>VerificationPipeline</c>'s catalog pre-resolve stage) or for pre-migration rows.
    /// </param>
    public JobVerdict(
        Guid id,
        Guid verificationJobId,
        VerdictSignal signal,
        VerdictSignal bankTierVerdict = VerdictSignal.Green,
        VerdictSignal condusefTierVerdict = VerdictSignal.Green,
        double confidence = 1.0,
        string engineVersion = "unknown",
        string? referenceBundleVersion = null)
    {
        ArgumentNullException.ThrowIfNull(engineVersion);

        Id = id;
        VerificationJobId = verificationJobId;
        Signal = signal;
        BankTierVerdict = bankTierVerdict;
        CondusefTierVerdict = condusefTierVerdict;
        Confidence = confidence;
        EngineVersion = engineVersion;
        ReferenceBundleVersion = referenceBundleVersion;
    }

    /// <summary>Gets the unique identifier for this verdict record.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the identifier of the parent <see cref="VerificationJob"/>.</summary>
    public Guid VerificationJobId { get; private set; }

    /// <summary>Gets the traffic-light signal summarising all check findings.</summary>
    public VerdictSignal Signal { get; private set; }

    /// <summary>
    /// Gets the verdict from the bank's own ruleset tier (Story 1.4).
    /// </summary>
    /// <remarks>
    /// <c>Green</c> — no bank-tier failures.
    /// <c>Yellow</c> — bank-tier improvement opportunities present.
    /// <c>Blocked</c> — set on early-exit blocked verdicts.
    /// Pre-existing rows (before migration <c>AddTwoTierVerdictColumns</c>) carry the DB
    /// default of <c>Green</c> (integer value 0).
    /// </remarks>
    public VerdictSignal BankTierVerdict { get; private set; }

    /// <summary>
    /// Gets the verdict from the CONDUSEF regulatory tier (Story 1.4).
    /// </summary>
    /// <remarks>
    /// <c>Green</c> — no CONDUSEF-mandated failures.
    /// <c>Red</c> — at least one CONDUSEF-mandated rule failed.
    /// <c>Blocked</c> — set on early-exit blocked verdicts.
    /// Pre-existing rows (before migration <c>AddTwoTierVerdictColumns</c>) carry the DB
    /// default of <c>Green</c> (integer value 0).
    /// </remarks>
    public VerdictSignal CondusefTierVerdict { get; private set; }

    /// <summary>
    /// Gets the minimum extraction confidence across all <see cref="Finding"/> items for this
    /// verdict (Story 4.1). Ranges from <c>0.0</c> (fully uncertain) to <c>1.0</c> (fully
    /// confident).
    /// </summary>
    /// <remarks>
    /// Stamped at persist time as <c>findings.Min(f =&gt; f.Confidence)</c>.
    /// <c>1.0</c> for blocked verdicts (no rules ran) and pre-existing rows (before migration
    /// <c>AddConfidenceColumns</c>).
    /// </remarks>
    public double Confidence { get; private set; } = 1.0;

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

    /// <summary>
    /// Gets the semantic version of the verification engine that produced this verdict
    /// (VERIQAN-E3-S4), for provenance/audit linkage.
    /// </summary>
    /// <remarks>
    /// Stamped from <c>VerificationPipeline</c>'s assembly version at persist time.
    /// <c>"unknown"</c> for pre-migration rows (before migration <c>AddJobVerdictProvenance</c>)
    /// and for verdicts constructed without a real engine version (e.g. unit-test fixtures).
    /// </remarks>
    public string EngineVersion { get; private set; } = "unknown";

    /// <summary>
    /// Gets the version of the reference-data bundle (<c>BundleMetadata.SchemaVersion</c>) that
    /// was active when this verdict was computed, or <see langword="null"/> when no bundle was
    /// resolved for the run.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> for pre-migration rows and for pipeline runs where the reference-data
    /// catalog pre-resolve stage degraded gracefully (no matching bundle for the submission's
    /// context key) — the pipeline continues with a <c>null</c> catalog bundle in that case.
    /// </remarks>
    public string? ReferenceBundleVersion { get; private set; }
}

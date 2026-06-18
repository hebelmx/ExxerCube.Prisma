using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Domain.Entities;

/// <summary>
/// An immutable, append-only audit record capturing a single human-reviewer decision
/// (accept or reject) applied to a <see cref="Finding"/> or to the overall statement verdict
/// of a <see cref="VerificationJob"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only contract (AR-9, FR-18):</b> Disposition rows are <b>never updated or deleted</b>
/// after they are written. The underlying database table is insert-only; no UPDATE or DELETE
/// statement should ever target it. To correct a prior decision, a new <see cref="Disposition"/>
/// row is appended — the audit trail therefore shows the full history of reviewer decisions.
/// </para>
/// <para>
/// <b>Human-actor invariant:</b> <see cref="Actor"/> is always a non-empty string identifying
/// the human reviewer who issued the decision. VEC never auto-dispositions — the application
/// layer enforces this by validating <see cref="Actor"/> before construction.
/// </para>
/// <para>
/// <b>Finding vs statement disposition:</b>
/// When <see cref="FindingId"/> is non-null the row dispositions a specific compliance check
/// result. When <see cref="FindingId"/> is null the row dispositions the aggregate statement
/// verdict for the entire job.
/// </para>
/// </remarks>
public sealed class Disposition
{
    /// <summary>
    /// Private constructor reserved for EF Core materialization.
    /// </summary>
    private Disposition() { }

    /// <summary>
    /// Initializes a new <see cref="Disposition"/> audit row.
    /// </summary>
    /// <param name="id">Unique audit-row identifier (caller allocates a new <see cref="Guid"/>).</param>
    /// <param name="verificationJobId">Identifier of the parent <see cref="VerificationJob"/>.</param>
    /// <param name="findingId">
    /// Identifier of the specific <see cref="Finding"/> being dispositioned, or <c>null</c>
    /// when the disposition applies to the overall statement verdict.
    /// </param>
    /// <param name="action">The human-reviewer decision (<see cref="DispositionAction.Accept"/> or
    /// <see cref="DispositionAction.Reject"/>).</param>
    /// <param name="actor">
    /// Non-empty string identifying the human reviewer who issued this decision.
    /// Must not be null, empty, or whitespace — the application service enforces this
    /// invariant before construction.
    /// </param>
    /// <param name="dispositionedAtUtc">UTC timestamp when the decision was recorded.</param>
    /// <param name="beforeState">
    /// Optional snapshot of the finding/verdict state <i>before</i> the decision was applied
    /// (e.g. serialised signal or verdict string).
    /// </param>
    /// <param name="afterState">
    /// Optional snapshot of the finding/verdict state <i>after</i> the decision was applied.
    /// </param>
    /// <param name="notes">Optional free-text reviewer notes explaining the decision.</param>
    /// <param name="engineVersion">
    /// Optional semantic version of the verification engine that produced the underlying finding.
    /// </param>
    /// <param name="referenceBundleVersion">
    /// Optional version of the reference-data bundle active when the job was verified.
    /// Provides provenance linkage for audit queries.
    /// </param>
    public Disposition(
        Guid id,
        Guid verificationJobId,
        Guid? findingId,
        DispositionAction action,
        string actor,
        DateTimeOffset dispositionedAtUtc,
        string? beforeState = null,
        string? afterState = null,
        string? notes = null,
        string? engineVersion = null,
        string? referenceBundleVersion = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("Actor must be a non-empty, non-whitespace string.", nameof(actor));

        Id = id;
        VerificationJobId = verificationJobId;
        FindingId = findingId;
        Action = action;
        Actor = actor;
        DispositionedAtUtc = dispositionedAtUtc;
        BeforeState = beforeState;
        AfterState = afterState;
        Notes = notes;
        EngineVersion = engineVersion;
        ReferenceBundleVersion = referenceBundleVersion;
    }

    // -----------------------------------------------------------------------
    // Identity
    // -----------------------------------------------------------------------

    /// <summary>Gets the unique identifier for this audit row.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the identifier of the parent <see cref="VerificationJob"/>.</summary>
    public Guid VerificationJobId { get; private set; }

    /// <summary>
    /// Gets the identifier of the specific <see cref="Finding"/> being dispositioned,
    /// or <c>null</c> when the disposition applies to the aggregate statement verdict.
    /// </summary>
    public Guid? FindingId { get; private set; }

    // -----------------------------------------------------------------------
    // Decision
    // -----------------------------------------------------------------------

    /// <summary>Gets the human-reviewer decision recorded in this audit row.</summary>
    public DispositionAction Action { get; private set; }

    /// <summary>
    /// Gets the identifier of the human reviewer who issued this decision.
    /// This is always a non-empty string — VEC never auto-dispositions.
    /// </summary>
    public string Actor { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when this decision was recorded.</summary>
    public DateTimeOffset DispositionedAtUtc { get; private set; }

    // -----------------------------------------------------------------------
    // Audit / provenance
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the serialised snapshot of the finding or verdict state <i>before</i> the
    /// reviewer decision was applied, or <c>null</c> when not captured.
    /// </summary>
    public string? BeforeState { get; private set; }

    /// <summary>
    /// Gets the serialised snapshot of the finding or verdict state <i>after</i> the
    /// reviewer decision was applied, or <c>null</c> when not captured.
    /// </summary>
    public string? AfterState { get; private set; }

    /// <summary>Gets optional free-text reviewer notes explaining the decision.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Gets the semantic version of the verification engine that produced the underlying
    /// finding, for provenance linkage.
    /// </summary>
    public string? EngineVersion { get; private set; }

    /// <summary>
    /// Gets the version of the reference-data bundle that was active when the job was
    /// verified, for provenance linkage.
    /// </summary>
    public string? ReferenceBundleVersion { get; private set; }
}

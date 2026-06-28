namespace ExxerCube.Prisma.Veriqan.Web.UI.Models;

/// <summary>
/// Aggregate view-model for one demo verification case, shaped after
/// <c>Veriqan.Orchestration.Pipeline.VerificationOutcome</c>.
/// The demo data service returns three instances: GREEN, RED, and BLOCKED.
/// </summary>
public sealed class DemoStatementCase
{
    /// <summary>Gets or sets the unique job identifier (mirrors <c>VerificationJob.Id</c>).</summary>
    public Guid JobId { get; init; } = Guid.NewGuid();

    /// <summary>Gets or sets the display name of this demo case (e.g. "Caso GREEN — Conforme").</summary>
    public string CaseName { get; init; } = string.Empty;

    /// <summary>Gets or sets the file name of the submitted statement.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Gets or sets the traffic-light verdict signal.</summary>
    public VerdictSignal Signal { get; init; }

    /// <summary>
    /// Gets or sets the CONDUSEF-tier verdict (checks whose tier is
    /// <see cref="ChecklistTier.Condusef"/> or <see cref="ChecklistTier.Both"/>).
    /// Red when at least one such check fails; Green otherwise.
    /// </summary>
    public VerdictSignal CondusefTierVerdict { get; init; }

    /// <summary>
    /// Gets or sets the bank-tier verdict (checks whose tier is <see cref="ChecklistTier.Bank"/>).
    /// Yellow when at least one such check fails; Green otherwise.
    /// </summary>
    public VerdictSignal BankTierVerdict { get; init; }

    /// <summary>Gets or sets the total number of findings evaluated.</summary>
    public int TotalChecks { get; init; }

    /// <summary>Gets or sets the count of Pass findings.</summary>
    public int PassCount { get; init; }

    /// <summary>Gets or sets the count of Fail findings.</summary>
    public int FailCount { get; init; }

    /// <summary>Gets or sets the count of InsufficientData findings.</summary>
    public int InsufficientDataCount { get; init; }

    /// <summary>Gets or sets the check IDs that returned Fail, mirroring <c>VerdictSummary.FailCheckIds</c>.</summary>
    public IReadOnlyList<string> FailCheckIds { get; init; } = Array.Empty<string>();

    /// <summary>Gets or sets the block reason when <see cref="Signal"/> is <see cref="VerdictSignal.Blocked"/>.</summary>
    public BlockReason? BlockReason { get; init; }

    /// <summary>Gets or sets the block detail message when blocked.</summary>
    public string? BlockDetail { get; init; }

    /// <summary>Gets or sets the full ordered list of per-check findings.</summary>
    public IReadOnlyList<DemoFinding> Findings { get; init; } = Array.Empty<DemoFinding>();

    /// <summary>Gets or sets the fields extracted from the statement header.</summary>
    public DemoExtractedFields ExtractedFields { get; init; } = new();

    /// <summary>Gets or sets the simulated processing duration.</summary>
    public TimeSpan ProcessingDuration { get; init; }

    /// <summary>Gets or sets the UTC timestamp when the job was received.</summary>
    public DateTimeOffset ReceivedAtUtc { get; init; }

    /// <summary>Gets or sets the path (relative to wwwroot) of the marked PDF preview, or null.</summary>
    public string? MarkedPdfPath { get; init; }

    /// <summary>Gets or sets the audit trail rows for this case.</summary>
    public IReadOnlyList<DemoAuditRow> AuditRows { get; init; } = Array.Empty<DemoAuditRow>();

    /// <summary>Gets or sets recorded dispositions for this case.</summary>
    public List<DemoDisposition> Dispositions { get; init; } = [];
}

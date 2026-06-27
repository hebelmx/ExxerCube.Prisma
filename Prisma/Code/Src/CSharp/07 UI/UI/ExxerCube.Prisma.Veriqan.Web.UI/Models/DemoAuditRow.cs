namespace ExxerCube.Prisma.Veriqan.Web.UI.Models;

/// <summary>
/// View-model for a single row in the Veriqan audit trail (demo capture 5).
/// Mirrors the append-only audit ledger written by the pipeline.
/// </summary>
public sealed class DemoAuditRow
{
    /// <summary>Gets or sets the unique audit entry identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets or sets the verification job this entry is attached to.</summary>
    public Guid JobId { get; init; }

    /// <summary>Gets or sets the UTC timestamp of the event.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Gets or sets the pipeline stage or event type (e.g. "Ingestion", "Verdict", "Disposition").</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>Gets or sets a human-readable description of what happened.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets or sets the actor that triggered the event (pipeline user or system component).</summary>
    public string Actor { get; init; } = string.Empty;

    /// <summary>Gets or sets supplementary data in JSON form, or empty string.</summary>
    public string Payload { get; init; } = string.Empty;
}

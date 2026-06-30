namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// EF persistence entity for a single row in the reprocess audit log,
/// tracking each explicit reprocessing event for a verified document.
/// </summary>
public sealed class ReprocessAuditLogEntity
{
    /// <summary>Gets or sets the unique identifier for this audit entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the content hash of the document that was reprocessed.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor (user or system identity) that triggered the reprocess.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional human-readable reason for the reprocess.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the integer representation of the <c>VerdictSignal</c> before reprocessing.
    /// <see langword="null"/> when no prior outcome existed.
    /// </summary>
    public int? BeforeSignal { get; set; }

    /// <summary>Gets or sets the integer representation of the <c>VerdictSignal</c> after reprocessing.</summary>
    public int AfterSignal { get; set; }

    /// <summary>Gets or sets the UTC timestamp at which the reprocessing occurred.</summary>
    public DateTimeOffset ReprocessedAtUtc { get; set; }
}

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// EF persistence entity for a single row in the reprocess audit log,
/// tracking each explicit reprocessing event for a verified document.
/// </summary>
public sealed class ReprocessAuditLogEntity
{
    /// <summary>Gets the unique identifier for this audit entry.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the content hash of the document that was reprocessed.</summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>Gets the actor (user or system identity) that triggered the reprocess.</summary>
    public string Actor { get; init; } = string.Empty;

    /// <summary>Gets an optional human-readable reason for the reprocess.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the integer representation of the <c>VerdictSignal</c> before reprocessing.
    /// <see langword="null"/> when no prior outcome existed.
    /// </summary>
    public int? BeforeSignal { get; init; }

    /// <summary>Gets the integer representation of the <c>VerdictSignal</c> after reprocessing.</summary>
    public int AfterSignal { get; init; }

    /// <summary>Gets the UTC timestamp at which the reprocessing occurred.</summary>
    public DateTimeOffset ReprocessedAtUtc { get; init; }
}

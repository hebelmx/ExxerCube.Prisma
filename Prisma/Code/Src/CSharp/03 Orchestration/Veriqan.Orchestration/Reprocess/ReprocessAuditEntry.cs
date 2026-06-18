using System;
using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;

/// <summary>
/// Immutable append-only audit record written each time a statement is explicitly reprocessed
/// by a human operator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only invariant:</b> Reprocess audit entries are never modified or deleted after
/// creation.  Auditing a subsequent reprocess of the same statement appends a second entry;
/// the full history of reprocess operations is preserved.
/// </para>
/// <para>
/// <b>Human-actor invariant:</b> The <paramref name="Actor"/> must be a non-empty string
/// supplied by the caller.  Automated paths are not permitted to create reprocess audit entries.
/// </para>
/// </remarks>
/// <param name="Id">Unique identifier of this audit entry.</param>
/// <param name="ContentHash">SHA-256 hex digest of the statement that was reprocessed.</param>
/// <param name="Actor">Non-empty identifier of the human operator who initiated reprocessing.</param>
/// <param name="Reason">Optional free-text reason supplied by the operator.</param>
/// <param name="BeforeSignal">
/// The <see cref="VerdictSignal"/> of the prior <c>VerificationOutcome</c> before reprocessing,
/// or <see langword="null"/> when no prior outcome existed.
/// </param>
/// <param name="AfterSignal">
/// The <see cref="VerdictSignal"/> of the new outcome produced by reprocessing.
/// </param>
/// <param name="ReprocessedAtUtc">UTC timestamp when the reprocess completed.</param>
public sealed record ReprocessAuditEntry(
    Guid Id,
    string ContentHash,
    string Actor,
    string? Reason,
    VerdictSignal? BeforeSignal,
    VerdictSignal AfterSignal,
    DateTimeOffset ReprocessedAtUtc);

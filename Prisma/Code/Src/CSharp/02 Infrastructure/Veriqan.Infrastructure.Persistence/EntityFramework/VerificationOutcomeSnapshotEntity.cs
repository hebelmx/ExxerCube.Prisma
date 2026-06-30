namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// EF persistence entity for a JSON snapshot of a <c>VerificationOutcome</c>.
/// One row per content hash; updated in place on reprocess.
/// </summary>
public sealed class VerificationOutcomeSnapshotEntity
{
    /// <summary>Gets or sets the content hash that uniquely identifies the verified document.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the full JSON-serialised <c>VerificationOutcome</c> payload.</summary>
    public string OutcomeJson { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp at which the snapshot was first written.</summary>
    public DateTimeOffset SavedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp at which the snapshot was last replaced via <c>ReplaceOutcomeAsync</c>.
    /// <see langword="null"/> when the snapshot has never been replaced.
    /// </summary>
    public DateTimeOffset? ReplacedAtUtc { get; set; }
}

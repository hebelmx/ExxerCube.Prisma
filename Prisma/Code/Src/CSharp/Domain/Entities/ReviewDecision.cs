namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a decision made during manual review of a case.
/// </summary>
public class ReviewDecision
{
    /// <summary>
    /// Gets or sets the unique identifier for the review decision.
    /// </summary>
    public string DecisionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the case identifier this decision is associated with.
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of decision made.
    /// </summary>
    public DecisionType DecisionType { get; set; } = DecisionType.Unknown;

    /// <summary>
    /// Gets or sets the user ID of the reviewer who made this decision.
    /// </summary>
    public string ReviewerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the decision was made.
    /// </summary>
    public DateTime ReviewedAt { get; set; }

    /// <summary>
    /// Gets or sets the review notes explaining the decision (required for overrides).
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dictionary of fields that were overridden during review, with field names as keys.
    /// </summary>
    public Dictionary<string, object> OverriddenFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the classification override if the reviewer changed the classification (nullable).
    /// </summary>
    public ClassificationResult? OverriddenClassification { get; set; }

    /// <summary>
    /// Gets or sets the     file identifier associated with this decision (nullable).
    /// </summary>
    public string? FileId { get; set; }

    /// <summary>
    /// Gets or sets the reason for the review decision.
    /// </summary>
    public ReviewReason ReviewReason { get; set; } = ReviewReason.Unknown;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewDecision"/> class.
    /// </summary>
    public ReviewDecision()
    {
    }
}

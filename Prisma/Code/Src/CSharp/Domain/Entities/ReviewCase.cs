namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents a case that requires manual review by a compliance analyst.
/// </summary>
public class ReviewCase
{
    /// <summary>
    /// Gets or sets the unique identifier for the review case.
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file identifier this review case is associated with.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason why this case requires manual review.
    /// </summary>
    public ReviewReason RequiresReviewReason { get; set; } = ReviewReason.Unknown;

    /// <summary>
    /// Gets or sets the overall confidence level for the classification or extraction (0-100).
    /// </summary>
    public int ConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the classification is ambiguous.
    /// </summary>
    public bool ClassificationAmbiguity { get; set; }

    /// <summary>
    /// Gets or sets the current status of the review case.
    /// </summary>
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>
    /// Gets or sets the user ID of the reviewer assigned to this case (nullable if unassigned).
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the review case was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewCase"/> class.
    /// </summary>
    public ReviewCase()
    {
    }
}


namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents filters for querying review cases.
/// </summary>
public class ReviewFilters
{
    /// <summary>
    /// Gets or sets the minimum confidence level filter (0-100, nullable).
    /// </summary>
    public int? MinConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum confidence level filter (0-100, nullable).
    /// </summary>
    public int? MaxConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to filter by classification ambiguity (nullable).
    /// </summary>
    public bool? ClassificationAmbiguity { get; set; }

    /// <summary>
    /// Gets or sets the review reason filter (nullable).
    /// </summary>
    public ReviewReason? ReviewReason { get; set; }

    /// <summary>
    /// Gets or sets the review status filter (nullable).
    /// </summary>
    public ReviewStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the assigned reviewer user ID filter (nullable).
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewFilters"/> class.
    /// </summary>
    public ReviewFilters()
    {
    }
}


namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Represents the status of a review case.
/// </summary>
public enum ReviewStatus
{
    /// <summary>
    /// Pending - case is waiting for review assignment.
    /// </summary>
    Pending,

    /// <summary>
    /// InProgress - case is currently being reviewed.
    /// </summary>
    InProgress,

    /// <summary>
    /// Completed - case has been reviewed and decision made.
    /// </summary>
    Completed,

    /// <summary>
    /// Rejected - case was rejected during review.
    /// </summary>
    Rejected
}


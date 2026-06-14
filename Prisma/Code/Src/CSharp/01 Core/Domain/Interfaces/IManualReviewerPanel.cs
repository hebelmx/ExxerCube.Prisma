namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the manual reviewer panel service for managing review cases and decisions.
/// </summary>
public interface IManualReviewerPanel
{
    /// <summary>
    /// Retrieves review cases filtered by the specified criteria.
    /// </summary>
    /// <param name="filters">Optional filters to apply when querying review cases.</param>
    /// <param name="pageNumber">Page number for pagination (1-based, default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 1000).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of review cases matching the filters or an error.</returns>
    Task<Result<List<ReviewCase>>> GetReviewCasesAsync(
        ReviewFilters? filters,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a review decision for a specific case.
    /// </summary>
    /// <param name="caseId">The unique identifier of the review case.</param>
    /// <param name="decision">The review decision to submit.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result> SubmitReviewDecisionAsync(
        string caseId,
        ReviewDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves field-level annotations for a specific review case.
    /// </summary>
    /// <param name="caseId">The unique identifier of the review case.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the field annotations for the case or an error.</returns>
    Task<Result<FieldAnnotations>> GetFieldAnnotationsAsync(
        string caseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies cases requiring manual review based on metadata and classification results.
    /// When <paramref name="isComplete"/> is <see langword="false"/> a new
    /// <c>IncompleteCase</c> review case is created (idempotent: only one pending row per file).
    /// When <paramref name="isComplete"/> is <see langword="true"/> any existing pending
    /// <c>IncompleteCase</c> rows for the file are healed (status → Completed).
    /// </summary>
    /// <param name="fileId">The file identifier this metadata is associated with.</param>
    /// <param name="metadata">The unified metadata record to analyze.</param>
    /// <param name="classification">The classification result to analyze.</param>
    /// <param name="isComplete">
    /// Whether the source case package was complete (all companion files present).
    /// Defaults to <see langword="true"/> so existing callers compile unchanged.
    /// </param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of identified review cases or an error.</returns>
    Task<Result<List<ReviewCase>>> IdentifyReviewCasesAsync(
        string fileId,
        UnifiedMetadataRecord metadata,
        ClassificationResult classification,
        bool isComplete = true,
        CancellationToken cancellationToken = default);
}


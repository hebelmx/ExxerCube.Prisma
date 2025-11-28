using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// Service for managing manual review cases and decisions.
/// </summary>
public class ManualReviewerService : IManualReviewerPanel
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<ManualReviewerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualReviewerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ManualReviewerService(
        PrismaDbContext dbContext,
        ILogger<ManualReviewerService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<List<ReviewCase>>> GetReviewCasesAsync(
        ReviewFilters? filters,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetReviewCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<ReviewCase>>();
        }

        // Validate pagination parameters
        if (pageNumber < 1)
        {
            _logger.LogWarning("GetReviewCasesAsync called with invalid pageNumber: {PageNumber}", pageNumber);
            return Result<List<ReviewCase>>.WithFailure("Page number must be greater than 0");
        }

        if (pageSize < 1 || pageSize > 1000)
        {
            _logger.LogWarning("GetReviewCasesAsync called with invalid pageSize: {PageSize}", pageSize);
            return Result<List<ReviewCase>>.WithFailure("Page size must be between 1 and 1000");
        }

        try
        {
            _logger.LogInformation("Retrieving review cases with filters, page {PageNumber}, page size {PageSize}", pageNumber, pageSize);

            var query = _dbContext.ReviewCases.AsQueryable();

            // Apply filters
            if (filters != null)
            {
                if (filters.MinConfidenceLevel.HasValue)
                {
                    query = query.Where(c => c.ConfidenceLevel >= filters.MinConfidenceLevel.Value);
                }

                if (filters.MaxConfidenceLevel.HasValue)
                {
                    query = query.Where(c => c.ConfidenceLevel <= filters.MaxConfidenceLevel.Value);
                }

                if (filters.ClassificationAmbiguity.HasValue)
                {
                    query = query.Where(c => c.ClassificationAmbiguity == filters.ClassificationAmbiguity.Value);
                }

                if (filters.ReviewReason is not null)
                {
                    query = query.Where(c => c.RequiresReviewReason == filters.ReviewReason);
                }

                if (filters.Status is not null)
                {
                    query = query.Where(c => c.Status == filters.Status);
                }

                if (!string.IsNullOrWhiteSpace(filters.AssignedTo))
                {
                    query = query.Where(c => c.AssignedTo == filters.AssignedTo);
                }
            }

            // Apply pagination
            var skip = (pageNumber - 1) * pageSize;
            var cases = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Retrieved {Count} review cases (page {PageNumber}, page size {PageSize})", cases.Count, pageNumber, pageSize);

            return Result<List<ReviewCase>>.Success(cases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetReviewCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<ReviewCase>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review cases");
            return Result<List<ReviewCase>>.WithFailure($"Error retrieving review cases: {ex.Message}", default(List<ReviewCase>), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> SubmitReviewDecisionAsync(
        string caseId,
        ReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SubmitReviewDecisionAsync cancelled before starting for case: {CaseId}", caseId);
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(caseId))
        {
            _logger.LogWarning("SubmitReviewDecisionAsync called with null or empty caseId");
            return Result.WithFailure("CaseId cannot be null or empty");
        }

        if (decision == null)
        {
            _logger.LogWarning("SubmitReviewDecisionAsync called with null decision");
            return Result.WithFailure("Decision cannot be null");
        }

        try
        {
            _logger.LogInformation("Submitting review decision for case: {CaseId}, decision type: {DecisionType}", caseId, decision.DecisionType);

            // Validate notes are required for overrides
            if ((decision.OverriddenFields?.Count > 0 || decision.OverriddenClassification != null)
                && string.IsNullOrWhiteSpace(decision.Notes))
            {
                _logger.LogWarning("Review decision requires notes when overrides are present for case: {CaseId}", caseId);
                return Result.WithFailure("Notes are required when overriding fields or classification");
            }

            // Use transaction for atomicity and concurrency control (if supported)
            // In-memory database doesn't support transactions, so we handle both cases
            IDbContextTransaction? transaction = null;

            try
            {
                if (_dbContext.Database.IsRelational())
                {
                    transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("TransactionIgnoredWarning", StringComparison.OrdinalIgnoreCase))
            {
                // In-memory database doesn't support transactions, continue without transaction
                _logger.LogDebug("Transactions not supported by database provider, continuing without transaction");
                transaction = null;
            }

            try
            {
                // Verify case exists and check for existing decision (concurrency control)
                var reviewCase = await _dbContext.ReviewCases
                    .FirstOrDefaultAsync(c => c.CaseId == caseId, cancellationToken).ConfigureAwait(false);

                if (reviewCase == null)
                {
                    _logger.LogWarning("Review case not found: {CaseId}", caseId);
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    }
                    return Result.WithFailure($"Review case not found: {caseId}");
                }

                // Check if decision already exists (prevent duplicates)
                var existingDecision = await _dbContext.ReviewDecisions
                    .AnyAsync(d => d.CaseId == caseId, cancellationToken).ConfigureAwait(false);

                if (existingDecision)
                {
                    _logger.LogWarning("A decision has already been submitted for case: {CaseId}", caseId);
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    }
                    return Result.WithFailure("A decision has already been submitted for this case");
                }

                // Set decision case ID if not set
                if (string.IsNullOrWhiteSpace(decision.CaseId))
                {
                    decision.CaseId = caseId;
                }

                // Set reviewed timestamp if not set
                if (decision.ReviewedAt == default)
                {
                    decision.ReviewedAt = DateTime.UtcNow;
                }

                // Generate decision ID if not set
                if (string.IsNullOrWhiteSpace(decision.DecisionId))
                {
                    decision.DecisionId = $"DEC-{Guid.NewGuid():N}";
                }

                // Save decision
                await _dbContext.ReviewDecisions.AddAsync(decision, cancellationToken).ConfigureAwait(false);

                // Update case status
                reviewCase.Status = decision.DecisionType.Value switch
                {
                    0 => ReviewStatus.Completed,
                    1 => ReviewStatus.Rejected,
                    2 => ReviewStatus.Pending,
                    _ => reviewCase.Status,
                };

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("Review decision submitted successfully for case: {CaseId}, decision ID: {DecisionId}", caseId, decision.DecisionId);

                return Result.Success();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                _logger.LogWarning(ex, "Concurrency conflict updating review case: {CaseId}", caseId);
                return Result.WithFailure("Case was modified by another user. Please refresh and try again.");
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SubmitReviewDecisionAsync cancelled for case: {CaseId}", caseId);
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting review decision for case: {CaseId}", caseId);
            return Result.WithFailure($"Error submitting review decision: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FieldAnnotations>> GetFieldAnnotationsAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetFieldAnnotationsAsync cancelled before starting for case: {CaseId}", caseId);
            return ResultExtensions.Cancelled<FieldAnnotations>();
        }

        if (string.IsNullOrWhiteSpace(caseId))
        {
            _logger.LogWarning("GetFieldAnnotationsAsync called with null or empty caseId");
            return Result<FieldAnnotations>.WithFailure("CaseId cannot be null or empty");
        }

        try
        {
            _logger.LogInformation("Retrieving field annotations for case: {CaseId}", caseId);

            // Verify case exists
            var reviewCase = await _dbContext.ReviewCases
                .FirstOrDefaultAsync(c => c.CaseId == caseId, cancellationToken).ConfigureAwait(false);

            if (reviewCase == null)
            {
                _logger.LogWarning("Review case not found: {CaseId}", caseId);
                return Result<FieldAnnotations>.WithFailure($"Review case not found: {caseId}");
            }

            // Get file metadata to retrieve unified metadata record
            var fileMetadata = await _dbContext.FileMetadata
                .FirstOrDefaultAsync(f => f.FileId == reviewCase.FileId, cancellationToken).ConfigureAwait(false);

            if (fileMetadata == null)
            {
                _logger.LogWarning("File metadata not found for case: {CaseId}, fileId: {FileId}", caseId, reviewCase.FileId);
                return Result<FieldAnnotations>.WithFailure($"File metadata not found for file: {reviewCase.FileId}");
            }

            // Build field annotations from review case and file metadata
            // Note: This is a simplified implementation - in a real scenario, we would need to
            // retrieve the UnifiedMetadataRecord from a separate service or storage
            var annotations = new FieldAnnotations
            {
                CaseId = caseId,
                FieldAnnotationsDict = new Dictionary<string, FieldAnnotation>()
            };

            // Add basic annotation for confidence level
            annotations.FieldAnnotationsDict["ConfidenceLevel"] = new FieldAnnotation
            {
                FieldName = "ConfidenceLevel",
                Value = reviewCase.ConfidenceLevel,
                Confidence = reviewCase.ConfidenceLevel,
                Source = "Classification",
                HasConflict = reviewCase.ClassificationAmbiguity,
                AgreementLevel = reviewCase.ClassificationAmbiguity ? 0.5f : 1.0f,
                OriginTrace = $"Case {caseId} - Classification"
            };

            _logger.LogInformation("Retrieved field annotations for case: {CaseId}", caseId);

            return Result<FieldAnnotations>.Success(annotations);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetFieldAnnotationsAsync cancelled for case: {CaseId}", caseId);
            return ResultExtensions.Cancelled<FieldAnnotations>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving field annotations for case: {CaseId}", caseId);
            return Result<FieldAnnotations>.WithFailure($"Error retrieving field annotations: {ex.Message}", default(FieldAnnotations), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<ReviewCase>>> IdentifyReviewCasesAsync(
        string fileId,
        UnifiedMetadataRecord metadata,
        ClassificationResult classification,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("IdentifyReviewCasesAsync cancelled before starting");
            return ResultExtensions.Cancelled<List<ReviewCase>>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            _logger.LogWarning("IdentifyReviewCasesAsync called with null or empty fileId");
            return Result<List<ReviewCase>>.WithFailure("FileId cannot be null or empty");
        }

        if (metadata == null)
        {
            _logger.LogWarning("IdentifyReviewCasesAsync called with null metadata");
            return Result<List<ReviewCase>>.WithFailure("Metadata cannot be null");
        }

        if (classification == null)
        {
            _logger.LogWarning("IdentifyReviewCasesAsync called with null classification");
            return Result<List<ReviewCase>>.WithFailure("Classification cannot be null");
        }

        try
        {
            _logger.LogInformation("Identifying review cases for file: {FileId}, classification confidence: {Confidence}", fileId, classification.Confidence);

            // Check if cases already exist for this file (prevent duplicates)
            var existingCases = await _dbContext.ReviewCases
                .Where(c => c.FileId == fileId && c.Status != ReviewStatus.Completed)
                .AnyAsync(cancellationToken).ConfigureAwait(false);

            if (existingCases)
            {
                _logger.LogInformation("Review cases already exist for file: {FileId}, skipping duplicate creation", fileId);
                var existingReviewCases = await _dbContext.ReviewCases
                    .Where(c => c.FileId == fileId && c.Status != ReviewStatus.Completed)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                return Result<List<ReviewCase>>.Success(existingReviewCases);
            }

            var reviewCases = new List<ReviewCase>();

            // Check for low confidence (< 80%)
            if (classification.Confidence < 80)
            {
                var lowConfidenceCase = new ReviewCase
                {
                    CaseId = $"CASE-{Guid.NewGuid():N}",
                    FileId = fileId,
                    RequiresReviewReason = ReviewReason.LowConfidence,
                    ConfidenceLevel = classification.Confidence,
                    ClassificationAmbiguity = false,
                    Status = ReviewStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                reviewCases.Add(lowConfidenceCase);
                _logger.LogInformation("Identified low confidence case: {CaseId}, confidence: {Confidence}", lowConfidenceCase.CaseId, classification.Confidence);
            }

            // Check for ambiguous classification
            bool isAmbiguous = classification.Level2 == null ||
                              (metadata.MatchedFields?.ConflictingFields?.Count > 0);

            if (isAmbiguous)
            {
                var ambiguousCase = new ReviewCase
                {
                    CaseId = $"CASE-{Guid.NewGuid():N}",
                    FileId = fileId,
                    RequiresReviewReason = ReviewReason.AmbiguousClassification,
                    ConfidenceLevel = classification.Confidence,
                    ClassificationAmbiguity = true,
                    Status = ReviewStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                reviewCases.Add(ambiguousCase);
                _logger.LogInformation("Identified ambiguous classification case: {CaseId}", ambiguousCase.CaseId);
            }

            // Check for extraction errors (conflicting fields or missing fields)
            if (metadata.MatchedFields != null)
            {
                bool hasExtractionErrors = (metadata.MatchedFields.ConflictingFields?.Count > 0) ||
                                          (metadata.MatchedFields.MissingFields?.Count > 0);

                if (hasExtractionErrors)
                {
                    var extractionErrorCase = new ReviewCase
                    {
                        CaseId = $"CASE-{Guid.NewGuid():N}",
                        FileId = fileId,
                        RequiresReviewReason = ReviewReason.ExtractionError,
                        ConfidenceLevel = classification.Confidence,
                        ClassificationAmbiguity = false,
                        Status = ReviewStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    reviewCases.Add(extractionErrorCase);
                    _logger.LogInformation("Identified extraction error case: {CaseId}, conflicts: {Conflicts}, missing: {Missing}",
                        extractionErrorCase.CaseId,
                        metadata.MatchedFields.ConflictingFields?.Count ?? 0,
                        metadata.MatchedFields.MissingFields?.Count ?? 0);
                }
            }

            // Save identified cases to database
            if (reviewCases.Count > 0)
            {
                await _dbContext.ReviewCases.AddRangeAsync(reviewCases, cancellationToken).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Identified and saved {Count} review cases", reviewCases.Count);
            }
            else
            {
                _logger.LogInformation("No review cases identified - classification and extraction are acceptable");
            }

            return Result<List<ReviewCase>>.Success(reviewCases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("IdentifyReviewCasesAsync cancelled");
            return ResultExtensions.Cancelled<List<ReviewCase>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying review cases");
            return Result<List<ReviewCase>>.WithFailure($"Error identifying review cases: {ex.Message}", default(List<ReviewCase>), ex);
        }
    }
}

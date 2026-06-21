using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
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
    private readonly IUnifiedMetadataStore? _unifiedMetadataStore;
    private readonly IEventPublisher? _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualReviewerService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="unifiedMetadataStore">
    /// Optional store used to hydrate real field annotations from the persisted
    /// <see cref="UnifiedMetadataRecord"/> (C3).  When <c>null</c> the service degrades to the
    /// previous stub behaviour (only a <c>ConfidenceLevel</c> annotation is emitted).
    /// </param>
    /// <param name="eventPublisher">
    /// Optional event publisher.  When provided and a reviewer submits an <c>Approve</c> decision,
    /// a <see cref="ReviewDecisionApprovedEvent"/> is published so the
    /// <c>ReviewApprovalExportHandler</c> can re-run Stage-5 export for the held case (G-C2b).
    /// When <c>null</c> no event is published (graceful degradation for callers that do not wire
    /// the event bus, e.g. in-process tests using the old two-parameter constructor).
    /// </param>
    public ManualReviewerService(
        PrismaDbContext dbContext,
        ILogger<ManualReviewerService> logger,
        IUnifiedMetadataStore? unifiedMetadataStore = null,
        IEventPublisher? eventPublisher = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unifiedMetadataStore = unifiedMetadataStore;
        _eventPublisher = eventPublisher;
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

                // G-C2b: publish ReviewDecisionApprovedEvent exactly once on Approve so the
                // ReviewApprovalExportHandler can re-run Stage-5 export for the held case.
                // Only Approve triggers a re-export; Reject / RequestMoreInfo are no-ops here.
                // Guard on _eventPublisher so callers that omit the optional publisher degrade
                // gracefully (InMemory tests, contract tests) without blowing up.
                if (_eventPublisher is not null && decision.DecisionType == DecisionType.Approve)
                {
                    if (!Guid.TryParse(reviewCase.FileId, out var approvedFileId))
                    {
                        _logger.LogWarning(
                            "Cannot publish ReviewDecisionApprovedEvent for case {CaseId}: FileId '{FileId}' is not a valid Guid",
                            caseId, reviewCase.FileId);
                    }
                    else
                    {
                        var approvedEvent = new ReviewDecisionApprovedEvent
                        {
                            EventId = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow,
                            FileId = approvedFileId,
                            CaseId = caseId,
                            DecisionId = decision.DecisionId,
                            ReviewerId = decision.ReviewerId,
                            HandoffPath = reviewCase.HandoffPath,
                        };
                        _eventPublisher.Publish(approvedEvent);
                        _logger.LogInformation(
                            "ReviewDecisionApprovedEvent published for case {CaseId}, FileId {FileId}, DecisionId {DecisionId}",
                            caseId, approvedFileId, decision.DecisionId);
                    }
                }

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
            var annotations = new FieldAnnotations
            {
                CaseId = caseId,
                FieldAnnotationsDict = new Dictionary<string, FieldAnnotation>()
            };

            // C3: Hydrate real per-field annotations from the persisted UnifiedMetadataRecord when available.
            // Degrade gracefully (only the ConfidenceLevel stub annotation) when the store is absent or the
            // record has not yet been saved for this file.
            if (_unifiedMetadataStore is not null && !string.IsNullOrWhiteSpace(reviewCase.FileId))
            {
                var recordResult = await _unifiedMetadataStore.GetByFileIdAsync(reviewCase.FileId, cancellationToken).ConfigureAwait(false);
                if (recordResult.IsSuccess && recordResult.Value is not null)
                {
                    var record = recordResult.Value;

                    // Hydrate from AdditionalFields (merged XML/OCR fields from FusionExpedienteService)
                    foreach (var kv in record.AdditionalFields)
                    {
                        annotations.FieldAnnotationsDict[kv.Key] = new FieldAnnotation
                        {
                            FieldName = kv.Key,
                            Value = kv.Value,
                            Source = "Fusion",
                            Confidence = reviewCase.ConfidenceLevel,
                            HasConflict = record.AdditionalFieldConflicts.Contains(kv.Key),
                            AgreementLevel = record.AdditionalFieldConflicts.Contains(kv.Key) ? 0.5f : 1.0f,
                            OriginTrace = $"File {reviewCase.FileId} - UnifiedMetadataRecord.AdditionalFields",
                        };
                    }

                    // Hydrate from MatchedFields.FieldMatches when available (best value per field across sources)
                    if (record.MatchedFields?.FieldMatches is { Count: > 0 } fieldMatches)
                    {
                        foreach (var kv in fieldMatches)
                        {
                            // Don't overwrite an entry already hydrated from AdditionalFields
                            if (!annotations.FieldAnnotationsDict.ContainsKey(kv.Key))
                            {
                                annotations.FieldAnnotationsDict[kv.Key] = new FieldAnnotation
                                {
                                    FieldName = kv.Key,
                                    Value = kv.Value.MatchedValue,
                                    Source = "Reconciliation",
                                    Confidence = (int)(kv.Value.Confidence * 100),
                                    HasConflict = kv.Value.HasConflict,
                                    AgreementLevel = kv.Value.AgreementLevel,
                                    OriginTrace = $"File {reviewCase.FileId} - UnifiedMetadataRecord.MatchedFields ({kv.Value.SourceType})",
                                };
                            }
                        }
                    }

                    _logger.LogInformation(
                        "Hydrated {Count} field annotations from UnifiedMetadataRecord for case {CaseId}",
                        annotations.FieldAnnotationsDict.Count, caseId);
                }
                else if (recordResult.IsFailure)
                {
                    _logger.LogDebug(
                        "No UnifiedMetadataRecord found for file {FileId} (case {CaseId}); falling back to stub annotation: {Error}",
                        reviewCase.FileId, caseId, recordResult.Error);
                }
            }

            // Always emit the ConfidenceLevel annotation as a baseline (even when real fields are present)
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
        bool isComplete = true,
        string? handoffPath = null,
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
            _logger.LogInformation(
                "Identifying review cases for file: {FileId}, classification confidence: {Confidence}, isComplete: {IsComplete}",
                fileId, classification.Confidence, isComplete);

            // Query all existing non-Completed cases for this file once.
            var existingNonCompleted = await _dbContext.ReviewCases
                .Where(c => c.FileId == fileId && c.Status != ReviewStatus.Completed)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var reviewCases = new List<ReviewCase>();
            var anyChanges = false;

            // ----------------------------------------------------------------
            // INCOMPLETE DIMENSION — orthogonal to confidence/ambiguity/extraction.
            // ----------------------------------------------------------------
            if (!isComplete)
            {
                // Flag: create an IncompleteCase row if no pending one already exists.
                var alreadyFlagged = existingNonCompleted
                    .Any(c => c.RequiresReviewReason == ReviewReason.IncompleteCase);

                if (!alreadyFlagged)
                {
                    var incompleteCase = new ReviewCase
                    {
                        CaseId = $"CASE-{Guid.NewGuid():N}",
                        FileId = fileId,
                        RequiresReviewReason = ReviewReason.IncompleteCase,
                        ConfidenceLevel = classification.Confidence,
                        ClassificationAmbiguity = false,
                        Status = ReviewStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        HandoffPath = handoffPath,
                    };

                    reviewCases.Add(incompleteCase);
                    await _dbContext.ReviewCases.AddAsync(incompleteCase, cancellationToken).ConfigureAwait(false);
                    anyChanges = true;
                    _logger.LogInformation(
                        "Incomplete case flagged: {CaseId} for file {FileId}", incompleteCase.CaseId, fileId);
                }
                else
                {
                    _logger.LogInformation(
                        "Incomplete case already pending for file {FileId} — skipping duplicate", fileId);
                }
            }
            else
            {
                // Heal: if the case package is now complete, close any pending IncompleteCase rows.
                var pendingIncomplete = existingNonCompleted
                    .Where(c => c.RequiresReviewReason == ReviewReason.IncompleteCase
                                && (c.Status == ReviewStatus.Pending || c.Status == ReviewStatus.InProgress))
                    .ToList();

                if (pendingIncomplete.Count > 0)
                {
                    foreach (var row in pendingIncomplete)
                    {
                        row.Status = ReviewStatus.Completed;
                    }

                    anyChanges = true;
                    _logger.LogInformation(
                        "Healed {Count} incomplete-case row(s) for file {FileId} (case is now complete)",
                        pendingIncomplete.Count, fileId);
                }
            }

            // ----------------------------------------------------------------
            // FIELD-MISMATCH DIMENSION (alertamiento — Item C, #9)
            // Orthogonal to confidence/ambiguity/extraction — mirrors the IncompleteCase pattern:
            // idempotent flag when alerts are present, heal (close) when alerts are cleared.
            // ----------------------------------------------------------------
            var hasFieldMismatchAlerts = metadata.FieldConflictAlerts?.Count > 0;

            if (hasFieldMismatchAlerts)
            {
                var alreadyFlaggedMismatch = existingNonCompleted
                    .Any(c => c.RequiresReviewReason == ReviewReason.FieldMismatch);

                if (!alreadyFlaggedMismatch)
                {
                    var mismatchCase = new ReviewCase
                    {
                        CaseId = $"CASE-{Guid.NewGuid():N}",
                        FileId = fileId,
                        RequiresReviewReason = ReviewReason.FieldMismatch,
                        ConfidenceLevel = classification.Confidence,
                        ClassificationAmbiguity = false,
                        Status = ReviewStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        HandoffPath = handoffPath,
                    };

                    reviewCases.Add(mismatchCase);
                    await _dbContext.ReviewCases.AddAsync(mismatchCase, cancellationToken).ConfigureAwait(false);
                    anyChanges = true;
                    _logger.LogInformation(
                        "Field-mismatch alertamiento flagged: {CaseId} for file {FileId} ({AlertCount} conflicting field(s))",
                        mismatchCase.CaseId, fileId, metadata.FieldConflictAlerts!.Count);
                }
                else
                {
                    _logger.LogInformation(
                        "Field-mismatch alertamiento already pending for file {FileId} — skipping duplicate", fileId);
                }
            }
            else
            {
                // Heal: if alerts are now empty, close any pending FieldMismatch rows.
                var pendingMismatch = existingNonCompleted
                    .Where(c => c.RequiresReviewReason == ReviewReason.FieldMismatch
                                && (c.Status == ReviewStatus.Pending || c.Status == ReviewStatus.InProgress))
                    .ToList();

                if (pendingMismatch.Count > 0)
                {
                    foreach (var row in pendingMismatch)
                    {
                        row.Status = ReviewStatus.Completed;
                    }

                    anyChanges = true;
                    _logger.LogInformation(
                        "Healed {Count} field-mismatch alertamiento row(s) for file {FileId} (no alerts present)",
                        pendingMismatch.Count, fileId);
                }
            }

            // ----------------------------------------------------------------
            // CONFIDENCE / AMBIGUITY / EXTRACTION DIMENSION
            // Dedup: only add these when NO non-Completed case of ANY kind exists yet
            // (existing guard, unchanged — only skips creation, not the incomplete heal above).
            // ----------------------------------------------------------------
            var hasExistingNonCompleted = existingNonCompleted.Count > 0;

            if (!hasExistingNonCompleted)
            {
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
                        CreatedAt = DateTime.UtcNow,
                        HandoffPath = handoffPath,
                    };

                    reviewCases.Add(lowConfidenceCase);
                    _logger.LogInformation(
                        "Identified low confidence case: {CaseId}, confidence: {Confidence}",
                        lowConfidenceCase.CaseId, classification.Confidence);
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
                        CreatedAt = DateTime.UtcNow,
                        HandoffPath = handoffPath,
                    };

                    reviewCases.Add(ambiguousCase);
                    _logger.LogInformation(
                        "Identified ambiguous classification case: {CaseId}", ambiguousCase.CaseId);
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
                            CreatedAt = DateTime.UtcNow,
                            HandoffPath = handoffPath,
                        };

                        reviewCases.Add(extractionErrorCase);
                        _logger.LogInformation(
                            "Identified extraction error case: {CaseId}, conflicts: {Conflicts}, missing: {Missing}",
                            extractionErrorCase.CaseId,
                            metadata.MatchedFields.ConflictingFields?.Count ?? 0,
                            metadata.MatchedFields.MissingFields?.Count ?? 0);
                    }
                }

                if (reviewCases.Count > 0)
                {
                    // Only the newly-added confidence/ambiguity/extraction cases need AddRange;
                    // the IncompleteCase row was already Add'd individually above (when applicable).
                    var newConfidenceCases = reviewCases
                        .Where(c => c.RequiresReviewReason != ReviewReason.IncompleteCase)
                        .ToList();
                    if (newConfidenceCases.Count > 0)
                    {
                        await _dbContext.ReviewCases.AddRangeAsync(newConfidenceCases, cancellationToken).ConfigureAwait(false);
                        anyChanges = true;
                    }
                }
            }
            else
            {
                _logger.LogInformation(
                    "Non-completed review cases already exist for file: {FileId}, skipping duplicate confidence/ambiguity/extraction case creation",
                    fileId);
            }

            // Persist once if anything was added or healed.
            if (anyChanges)
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Saved review case changes for file {FileId}: {NewCount} new case(s)", fileId, reviewCases.Count);
            }
            else if (reviewCases.Count == 0)
            {
                _logger.LogInformation(
                    "No review cases identified or changed for file {FileId}", fileId);
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

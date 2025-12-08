namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Orchestrates Stage 3 workflow: identity resolution, legal directive classification, and compliance action mapping.
/// </summary>
public class DecisionLogicService
{
    private readonly IPersonIdentityResolver _personIdentityResolver;
    private readonly ILegalDirectiveClassifier _legalDirectiveClassifier;
    private readonly IManualReviewerPanel _manualReviewerPanel;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DecisionLogicService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionLogicService"/> class.
    /// </summary>
    /// <param name="personIdentityResolver">The person identity resolver service.</param>
    /// <param name="legalDirectiveClassifier">The legal directive classifier service.</param>
    /// <param name="manualReviewerPanel">The manual reviewer panel service.</param>
    /// <param name="auditLogger">The audit logger service.</param>
    /// <param name="logger">The logger instance.</param>
    public DecisionLogicService(
        IPersonIdentityResolver personIdentityResolver,
        ILegalDirectiveClassifier legalDirectiveClassifier,
        IManualReviewerPanel manualReviewerPanel,
        IAuditLogger auditLogger,
        ILogger<DecisionLogicService> logger)
    {
        _personIdentityResolver = personIdentityResolver;
        _legalDirectiveClassifier = legalDirectiveClassifier;
        _manualReviewerPanel = manualReviewerPanel;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Processes persons through identity resolution and deduplication workflow.
    /// </summary>
    /// <param name="persons">The list of persons to process.</param>
    /// <param name="fileId">The file identifier (optional, for audit logging).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages (optional, generates new if not provided).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the resolved and deduplicated list of persons or an error.</returns>
    public async Task<Result<List<Persona>>> ResolvePersonIdentitiesAsync(
        List<Persona> persons,
        string? fileId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Identity resolution cancelled before starting");
            return ResultExtensions.Cancelled<List<Persona>>();
        }

        if (persons == null || persons.Count == 0)
        {
            _logger.LogWarning("Empty or null persons list provided for identity resolution");
            return Result<List<Persona>>.Success(new List<Persona>());
        }

        // Generate correlation ID if not provided
        var actualCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Starting identity resolution for {Count} persons (CorrelationId: {CorrelationId})", persons.Count, actualCorrelationId);

            // Resolve identity for each person
            var resolvedPersons = new List<Persona>();
            var totalRequested = persons.Count;
            foreach (var person in persons)
            {
                // Check for cancellation between iterations
                if (cancellationToken.IsCancellationRequested)
                {
                    // Preserve partial results if work has been completed
                    if (resolvedPersons.Count > 0)
                    {
                        var completed = resolvedPersons.Count;
                        var confidence = (double)completed / totalRequested;
                        var missingDataRatio = (double)(totalRequested - completed) / totalRequested;

                        _logger.LogWarning(
                            "Identity resolution cancelled during processing. Returning {CompletedCount} of {TotalCount} resolved persons.",
                            completed, totalRequested);

                        // Deduplicate partial results before returning (if possible)
                        var partialDeduplicateResult = await _personIdentityResolver.DeduplicatePersonsAsync(resolvedPersons, cancellationToken).ConfigureAwait(false);
                        if (partialDeduplicateResult.IsSuccess && partialDeduplicateResult.Value != null)
                        {
                            return Result<List<Persona>>.WithWarnings(
                                warnings: new[] { $"Operation was cancelled. Resolved {completed} of {totalRequested} persons." },
                                value: partialDeduplicateResult.Value,
                                confidence: confidence,
                                missingDataRatio: missingDataRatio
                            );
                        }
                        else
                        {
                            // Deduplication failed or was cancelled, but return partial results anyway
                            var deduplicationWarning = partialDeduplicateResult.IsCancelled()
                                ? " (deduplication cancelled)"
                                : " (deduplication failed)";

                            return Result<List<Persona>>.WithWarnings(
                                warnings: new[] { $"Operation was cancelled. Resolved {completed} of {totalRequested} persons{deduplicationWarning}." },
                                value: resolvedPersons,
                                confidence: confidence,
                                missingDataRatio: missingDataRatio
                            );
                        }
                    }

                    // No work completed - return cancelled
                    _logger.LogWarning("Identity resolution cancelled during processing with no completed work");
                    return ResultExtensions.Cancelled<List<Persona>>();
                }

                var resolveResult = await _personIdentityResolver.ResolveIdentityAsync(person, cancellationToken).ConfigureAwait(false);

                // Propagate cancellation from dependencies FIRST
                if (resolveResult.IsCancelled())
                {
                    // Log audit for cancelled resolution
                    await _auditLogger.LogAuditAsync(
                        AuditActionType.Extraction,
                        ProcessingStage.DecisionLogic,
                        fileId,
                        actualCorrelationId,
                        null,
                        $"{{\"PersonName\":\"{person.Nombre}\",\"Rfc\":\"{person.Rfc ?? "N/A"}\",\"PersonaTipo\":\"{person.PersonaTipo}\"}}",
                        false,
                        "Operation cancelled",
                        cancellationToken).ConfigureAwait(false);

                    // Preserve partial results if work has been completed
                    if (resolvedPersons.Count > 0)
                    {
                        var completed = resolvedPersons.Count;
                        var confidence = (double)completed / totalRequested;
                        var missingDataRatio = (double)(totalRequested - completed) / totalRequested;

                        _logger.LogWarning(
                            "Identity resolution cancelled by resolver. Returning {CompletedCount} of {TotalCount} resolved persons.",
                            completed, totalRequested);

                        // Deduplicate partial results before returning (if possible)
                        var partialDeduplicateResult = await _personIdentityResolver.DeduplicatePersonsAsync(resolvedPersons, cancellationToken).ConfigureAwait(false);
                        if (partialDeduplicateResult.IsSuccess && partialDeduplicateResult.Value != null)
                        {
                            return Result<List<Persona>>.WithWarnings(
                                warnings: new[] { $"Operation was cancelled by resolver. Resolved {completed} of {totalRequested} persons." },
                                value: partialDeduplicateResult.Value,
                                confidence: confidence,
                                missingDataRatio: missingDataRatio
                            );
                        }
                        else
                        {
                            // Deduplication failed or was cancelled, but return partial results anyway
                            var deduplicationWarning = partialDeduplicateResult.IsCancelled()
                                ? " (deduplication cancelled)"
                                : " (deduplication failed)";

                            return Result<List<Persona>>.WithWarnings(
                                warnings: new[] { $"Operation was cancelled by resolver. Resolved {completed} of {totalRequested} persons{deduplicationWarning}." },
                                value: resolvedPersons,
                                confidence: confidence,
                                missingDataRatio: missingDataRatio
                            );
                        }
                    }

                    // No work completed - return cancelled
                    _logger.LogWarning("Identity resolution cancelled by resolver with no completed work");
                    return ResultExtensions.Cancelled<List<Persona>>();
                }

                if (resolveResult.IsFailure)
                {
                    _logger.LogWarning("Failed to resolve identity for person: {Error}", resolveResult.Error);

                    // Log audit for failed resolution
                    await _auditLogger.LogAuditAsync(
                        AuditActionType.Extraction,
                        ProcessingStage.DecisionLogic,
                        fileId,
                        actualCorrelationId,
                        null,
                        $"{{\"PersonName\":\"{person.Nombre}\",\"Rfc\":\"{person.Rfc ?? "N/A"}\",\"PersonaTipo\":\"{person.PersonaTipo}\"}}",
                        false,
                        resolveResult.Error,
                        cancellationToken).ConfigureAwait(false);

                    continue;
                }

                // Log audit for successful resolution
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Extraction,
                    ProcessingStage.DecisionLogic,
                    fileId,
                    actualCorrelationId,
                    null,
                    $"{{\"PersonName\":\"{person.Nombre}\",\"Rfc\":\"{person.Rfc ?? "N/A"}\",\"PersonaTipo\":\"{person.PersonaTipo}\"}}",
                    true,
                    null,
                    cancellationToken).ConfigureAwait(false);

                if (resolveResult.Value != null)
                {
                    resolvedPersons.Add(resolveResult.Value);
                }
            }

            // Deduplicate persons
            var deduplicateResult = await _personIdentityResolver.DeduplicatePersonsAsync(resolvedPersons, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from deduplication FIRST
            if (deduplicateResult.IsCancelled())
            {
                // Log audit for cancelled deduplication
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Extraction,
                    ProcessingStage.DecisionLogic,
                    fileId,
                    actualCorrelationId,
                    null,
                    $"{{\"InputCount\":{persons.Count},\"ResolvedCount\":{resolvedPersons.Count}}}",
                    false,
                    "Operation cancelled",
                    cancellationToken).ConfigureAwait(false);

                // Preserve partial results if we have resolved persons (deduplication was cancelled, but resolution completed)
                if (resolvedPersons.Count > 0)
                {
                    var completed = resolvedPersons.Count;
                    var confidence = (double)completed / totalRequested;
                    var missingDataRatio = (double)(totalRequested - completed) / totalRequested;

                    _logger.LogWarning(
                        "Identity resolution cancelled during deduplication. Returning {CompletedCount} of {TotalCount} resolved persons (not deduplicated).",
                        completed, totalRequested);

                    return Result<List<Persona>>.WithWarnings(
                        warnings: new[] { $"Operation was cancelled during deduplication. Resolved {completed} of {totalRequested} persons (deduplication incomplete)." },
                        value: resolvedPersons,
                        confidence: confidence,
                        missingDataRatio: missingDataRatio
                    );
                }

                // No work completed - return cancelled
                _logger.LogWarning("Identity resolution cancelled during deduplication with no completed work");
                return ResultExtensions.Cancelled<List<Persona>>();
            }

            if (deduplicateResult.IsFailure)
            {
                _logger.LogError("Failed to deduplicate persons: {Error}", deduplicateResult.Error);

                // Log audit for failed deduplication
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Extraction,
                    ProcessingStage.DecisionLogic,
                    fileId,
                    actualCorrelationId,
                    null,
                    $"{{\"InputCount\":{persons.Count},\"ResolvedCount\":{resolvedPersons.Count}}}",
                    false,
                    deduplicateResult.Error,
                    cancellationToken).ConfigureAwait(false);

                return Result<List<Persona>>.WithFailure($"Failed to deduplicate persons: {deduplicateResult.Error}");
            }

            // Log audit for successful deduplication
            await _auditLogger.LogAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.DecisionLogic,
                fileId,
                actualCorrelationId,
                null,
                $"{{\"InputCount\":{persons.Count},\"ResolvedCount\":{resolvedPersons.Count},\"DeduplicatedCount\":{deduplicateResult.Value?.Count ?? 0}}}",
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Identity resolution completed: {OriginalCount} → {ResolvedCount} persons",
                persons.Count, deduplicateResult.Value?.Count ?? 0);

            return Result<List<Persona>>.Success(deduplicateResult.Value ?? new List<Persona>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Identity resolution cancelled");
            return ResultExtensions.Cancelled<List<Persona>>();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing person identities");
            return Result<List<Persona>>.WithFailure($"Error processing person identities: {ex.Message}", default(List<Persona>), ex);
        }
    }

    /// <summary>
    /// Classifies legal directives from document text and maps them to compliance actions.
    /// </summary>
    /// <param name="documentText">The document text to classify.</param>
    /// <param name="expediente">The expediente information for context (optional).</param>
    /// <param name="fileId">The file identifier (optional, for audit logging).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages (optional, generates new if not provided).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of compliance actions with confidence scores or an error.</returns>
    public async Task<Result<List<ComplianceAction>>> ClassifyLegalDirectivesAsync(
        string documentText,
        Expediente? expediente = null,
        string? fileId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Legal directive classification cancelled before starting");
            return ResultExtensions.Cancelled<List<ComplianceAction>>();
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Empty document text provided for legal directive classification");
            return Result<List<ComplianceAction>>.Success(new List<ComplianceAction>());
        }

        // Generate correlation ID if not provided
        var actualCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Starting legal directive classification for document (length: {Length}, CorrelationId: {CorrelationId})", documentText.Length, actualCorrelationId);

            // Detect legal instruments (non-blocking - continue even if this fails, but not if cancelled)
            var instrumentsResult = await _legalDirectiveClassifier.DetectLegalInstrumentsAsync(documentText, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from instrument detection
            if (instrumentsResult.IsCancelled())
            {
                _logger.LogWarning("Legal directive classification cancelled during instrument detection");
                return ResultExtensions.Cancelled<List<ComplianceAction>>();
            }

            if (instrumentsResult.IsFailure)
            {
                _logger.LogWarning("Failed to detect legal instruments: {Error} - continuing with classification", instrumentsResult.Error);
            }
            else if (instrumentsResult.Value != null && instrumentsResult.Value.Count > 0)
            {
                _logger.LogInformation("Detected {Count} legal instruments: {Instruments}",
                    instrumentsResult.Value.Count, string.Join(", ", instrumentsResult.Value));
            }

            // Classify directives (this is the critical operation)
            var classifyResult = await _legalDirectiveClassifier.ClassifyDirectivesAsync(documentText, expediente, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from classification FIRST
            if (classifyResult.IsCancelled())
            {
                _logger.LogWarning("Legal directive classification cancelled during directive classification");
                return ResultExtensions.Cancelled<List<ComplianceAction>>();
            }

            if (classifyResult.IsFailure)
            {
                _logger.LogError("Failed to classify legal directives: {Error}", classifyResult.Error);

                // Log audit for failed classification
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Extraction,
                    ProcessingStage.DecisionLogic,
                    fileId,
                    actualCorrelationId,
                    null,
                    null,
                    false,
                    classifyResult.Error,
                    cancellationToken).ConfigureAwait(false);

                return Result<List<ComplianceAction>>.WithFailure($"Failed to classify legal directives: {classifyResult.Error}");
            }

            // Now safe to access Value after checking cancellation and failure
            var actions = classifyResult.Value ?? new List<ComplianceAction>();
            var actionsCount = actions.Count;
            var actionsDetails = actionsCount > 0
                ? $"{{\"ActionsCount\":{actionsCount},\"Actions\":[{string.Join(",", actions.Select(a => $"{{\"Type\":\"{a.ActionType}\",\"AccountNumber\":\"{a.AccountNumber ?? "N/A"}\"}}"))}]}}"
                : $"{{\"ActionsCount\":0}}";

            // Log audit for successful classification
            await _auditLogger.LogAuditAsync(
                AuditActionType.Extraction,
                ProcessingStage.DecisionLogic,
                fileId,
                actualCorrelationId,
                null,
                actionsDetails,
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Legal directive classification completed: {Count} compliance actions identified",
                actionsCount);

            return Result<List<ComplianceAction>>.Success(classifyResult.Value ?? new List<ComplianceAction>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Legal directive classification cancelled");
            return ResultExtensions.Cancelled<List<ComplianceAction>>();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error classifying legal directives");
            return Result<List<ComplianceAction>>.WithFailure($"Error classifying legal directives: {ex.Message}", default(List<ComplianceAction>), ex);
        }
    }

    /// <summary>
    /// Processes complete Stage 3 workflow: identity resolution → legal classification → compliance action mapping.
    /// </summary>
    /// <param name="persons">The list of persons to resolve.</param>
    /// <param name="documentText">The document text to classify.</param>
    /// <param name="expediente">The expediente information for context (optional).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the decision logic result with resolved persons and compliance actions, or an error.</returns>
    public async Task<Result<DecisionLogicResult>> ProcessDecisionLogicAsync(
        List<Persona> persons,
        string documentText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Decision logic workflow cancelled before starting");
            return ResultExtensions.Cancelled<DecisionLogicResult>();
        }

        // Validate null parameters at method entry (ROP pattern)
        if (persons == null)
        {
            _logger.LogWarning("ProcessDecisionLogicAsync called with null persons list");
            return Result<DecisionLogicResult>.WithFailure("Persons list cannot be null");
        }

        if (documentText == null)
        {
            _logger.LogWarning("ProcessDecisionLogicAsync called with null document text");
            return Result<DecisionLogicResult>.WithFailure("Document text cannot be null");
        }

        try
        {
            _logger.LogInformation("Starting complete decision logic workflow");

            // Step 1: Resolve person identities
            var resolveResult = await ResolvePersonIdentitiesAsync(persons, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from identity resolution
            if (resolveResult.IsCancelled())
            {
                _logger.LogWarning("Decision logic workflow cancelled during identity resolution");
                return ResultExtensions.Cancelled<DecisionLogicResult>();
            }

            if (resolveResult.IsFailure)
            {
                return Result<DecisionLogicResult>.WithFailure($"Identity resolution failed: {resolveResult.Error}");
            }

            // Handle partial results with warnings (cancellation occurred but work was completed)
            var hasPartialResults = resolveResult.HasWarnings;
            var warnings = hasPartialResults ? resolveResult.Warnings : null;

            // Step 2: Classify legal directives
            var classifyResult = await ClassifyLegalDirectivesAsync(documentText, expediente, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from legal classification
            if (classifyResult.IsCancelled())
            {
                // If we have partial results from identity resolution, preserve them
                if (hasPartialResults && resolveResult.Value != null && resolveResult.Value.Count > 0)
                {
                    _logger.LogWarning(
                        "Decision logic workflow cancelled during legal classification. Returning partial results: {PersonCount} persons resolved.",
                        resolveResult.Value.Count);

                    var combinedWarnings = new List<string>();
                    if (warnings != null)
                    {
                        combinedWarnings.AddRange(warnings);
                    }
                    combinedWarnings.Add("Legal classification was cancelled.");

                    return Result<DecisionLogicResult>.WithWarnings(
                        warnings: combinedWarnings.ToArray(),
                        value: new DecisionLogicResult
                        {
                            ResolvedPersons = resolveResult.Value,
                            ComplianceActions = new List<ComplianceAction>()
                        },
                        confidence: resolveResult.Confidence,
                        missingDataRatio: resolveResult.MissingDataRatio
                    );
                }

                _logger.LogWarning("Decision logic workflow cancelled during legal classification");
                return ResultExtensions.Cancelled<DecisionLogicResult>();
            }

            if (classifyResult.IsFailure)
            {
                // If we have partial results from identity resolution, preserve them
                if (hasPartialResults && resolveResult.Value != null && resolveResult.Value.Count > 0)
                {
                    _logger.LogWarning(
                        "Legal classification failed but returning partial results: {PersonCount} persons resolved. Error: {Error}",
                        resolveResult.Value.Count, classifyResult.Error);

                    var combinedWarnings = new List<string>();
                    if (warnings != null)
                    {
                        combinedWarnings.AddRange(warnings);
                    }
                    combinedWarnings.Add($"Legal classification failed: {classifyResult.Error}");

                    return Result<DecisionLogicResult>.WithWarnings(
                        warnings: combinedWarnings.ToArray(),
                        value: new DecisionLogicResult
                        {
                            ResolvedPersons = resolveResult.Value,
                            ComplianceActions = new List<ComplianceAction>()
                        },
                        confidence: resolveResult.Confidence,
                        missingDataRatio: resolveResult.MissingDataRatio
                    );
                }

                return Result<DecisionLogicResult>.WithFailure($"Legal classification failed: {classifyResult.Error}");
            }

            var result = new DecisionLogicResult
            {
                ResolvedPersons = resolveResult.Value ?? new List<Persona>(),
                ComplianceActions = classifyResult.Value ?? new List<ComplianceAction>()
            };

            // If we have warnings from identity resolution, propagate them
            if (hasPartialResults)
            {
                _logger.LogInformation(
                    "Decision logic workflow completed with partial results: {PersonCount} persons, {ActionCount} actions. Warnings: {Warnings}",
                    result.ResolvedPersons.Count, result.ComplianceActions.Count, string.Join("; ", warnings ?? Array.Empty<string>()));

                return Result<DecisionLogicResult>.WithWarnings(
                    warnings: warnings ?? Array.Empty<string>(),
                    value: result,
                    confidence: resolveResult.Confidence > 0 ? resolveResult.Confidence : 1.0,
                    missingDataRatio: resolveResult.MissingDataRatio
                );
            }

            _logger.LogInformation("Decision logic workflow completed: {PersonCount} persons, {ActionCount} actions",
                result.ResolvedPersons.Count, result.ComplianceActions.Count);

            return Result<DecisionLogicResult>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Decision logic workflow cancelled");
            return ResultExtensions.Cancelled<DecisionLogicResult>();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing decision logic");
            return Result<DecisionLogicResult>.WithFailure($"Error processing decision logic: {ex.Message}", default(DecisionLogicResult), ex);
        }
    }

    /// <summary>
    /// Identifies and queues review cases based on metadata and classification results.
    /// </summary>
    /// <param name="fileId">The file identifier this metadata is associated with.</param>
    /// <param name="metadata">The unified metadata record to analyze.</param>
    /// <param name="classification">The classification result to analyze.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of identified review cases or an error.</returns>
    public async Task<Result<List<ReviewCase>>> IdentifyAndQueueReviewCasesAsync(
        string fileId,
        UnifiedMetadataRecord metadata,
        ClassificationResult classification,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("IdentifyAndQueueReviewCasesAsync cancelled before starting for file: {FileId}", fileId);
            return ResultExtensions.Cancelled<List<ReviewCase>>();
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            _logger.LogWarning("IdentifyAndQueueReviewCasesAsync called with null or empty fileId");
            return Result<List<ReviewCase>>.WithFailure("FileId cannot be null or empty");
        }

        if (metadata == null)
        {
            _logger.LogWarning("IdentifyAndQueueReviewCasesAsync called with null metadata");
            return Result<List<ReviewCase>>.WithFailure("Metadata cannot be null");
        }

        if (classification == null)
        {
            _logger.LogWarning("IdentifyAndQueueReviewCasesAsync called with null classification");
            return Result<List<ReviewCase>>.WithFailure("Classification cannot be null");
        }

        try
        {
            _logger.LogInformation("Identifying review cases for file: {FileId}, classification confidence: {Confidence}", fileId, classification.Confidence);

            var identifyResult = await _manualReviewerPanel.IdentifyReviewCasesAsync(fileId, metadata, classification, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from manual reviewer panel
            if (identifyResult.IsCancelled())
            {
                _logger.LogWarning("Review case identification cancelled for file: {FileId}", fileId);
                return ResultExtensions.Cancelled<List<ReviewCase>>();
            }

            if (identifyResult.IsFailure)
            {
                _logger.LogError("Failed to identify review cases for file: {FileId}, error: {Error}", fileId, identifyResult.Error);
                return Result<List<ReviewCase>>.WithFailure($"Failed to identify review cases: {identifyResult.Error}");
            }

            var reviewCases = identifyResult.Value ?? new List<ReviewCase>();

            _logger.LogInformation("Identified {Count} review cases for file: {FileId}", reviewCases.Count, fileId);

            return Result<List<ReviewCase>>.Success(reviewCases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("IdentifyAndQueueReviewCasesAsync cancelled for file: {FileId}", fileId);
            return ResultExtensions.Cancelled<List<ReviewCase>>();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error identifying review cases for file: {FileId}", fileId);
            return Result<List<ReviewCase>>.WithFailure($"Error identifying review cases: {ex.Message}", default(List<ReviewCase>), ex);
        }
    }

    /// <summary>
    /// Processes a review decision and updates the unified metadata record accordingly.
    /// </summary>
    /// <param name="caseId">The unique identifier of the review case.</param>
    /// <param name="decision">The review decision to process.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    public async Task<Result> ProcessReviewDecisionAsync(
        string caseId,
        ReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ProcessReviewDecisionAsync cancelled before starting for case: {CaseId}", caseId);
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(caseId))
        {
            _logger.LogWarning("ProcessReviewDecisionAsync called with null or empty caseId");
            return Result.WithFailure("CaseId cannot be null or empty");
        }

        if (decision == null)
        {
            _logger.LogWarning("ProcessReviewDecisionAsync called with null decision");
            return Result.WithFailure("Decision cannot be null");
        }

        try
        {
            _logger.LogInformation("Processing review decision for case: {CaseId}, decision type: {DecisionType}", caseId, decision.DecisionType);

            var submitResult = await _manualReviewerPanel.SubmitReviewDecisionAsync(caseId, decision, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from manual reviewer panel FIRST
            if (submitResult.IsCancelled())
            {
                _logger.LogWarning("Review decision processing cancelled for case: {CaseId}", caseId);

                // Log audit for cancelled review decision
                var reviewDetails = $"{{\"CaseId\":\"{caseId}\",\"DecisionType\":\"{decision.DecisionType}\",\"DecisionId\":\"{decision.DecisionId}\",\"ReviewReason\":\"{decision.ReviewReason}\"}}";
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Review,
                    ProcessingStage.DecisionLogic,
                    decision.FileId,
                    Guid.NewGuid().ToString(), // Generate new correlation ID for review decision
                    decision.ReviewerId,
                    reviewDetails,
                    false,
                    "Operation cancelled",
                    cancellationToken).ConfigureAwait(false);

                return ResultExtensions.Cancelled();
            }

            if (submitResult.IsFailure)
            {
                _logger.LogError("Failed to process review decision for case: {CaseId}, error: {Error}", caseId, submitResult.Error);

                // Log audit for failed review decision
                var reviewDetails = $"{{\"CaseId\":\"{caseId}\",\"DecisionType\":\"{decision.DecisionType}\",\"DecisionId\":\"{decision.DecisionId}\",\"ReviewReason\":\"{decision.ReviewReason}\"}}";
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Review,
                    ProcessingStage.DecisionLogic,
                    decision.FileId,
                    Guid.NewGuid().ToString(), // Generate new correlation ID for review decision
                    decision.ReviewerId,
                    reviewDetails,
                    false,
                    submitResult.Error,
                    cancellationToken).ConfigureAwait(false);

                return Result.WithFailure($"Failed to process review decision: {submitResult.Error}");
            }

            // Log audit for successful review decision
            var successReviewDetails = $"{{\"CaseId\":\"{caseId}\",\"DecisionType\":\"{decision.DecisionType}\",\"DecisionId\":\"{decision.DecisionId}\",\"ReviewReason\":\"{decision.ReviewReason}\"}}";
            await _auditLogger.LogAuditAsync(
                AuditActionType.Review,
                ProcessingStage.DecisionLogic,
                decision.FileId,
                Guid.NewGuid().ToString(), // Generate new correlation ID for review decision
                decision.ReviewerId,
                successReviewDetails,
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Review decision processed successfully for case: {CaseId}, decision ID: {DecisionId}", caseId, decision.DecisionId);

            // Note: In a full implementation, we would update the unified metadata record here based on the decision
            // For now, the decision is saved and the case status is updated by ManualReviewerService

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("ProcessReviewDecisionAsync cancelled for case: {CaseId}", caseId);
            return ResultExtensions.Cancelled();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing review decision for case: {CaseId}", caseId);
            return Result.WithFailure($"Error processing review decision: {ex.Message}", ex);
        }
    }
}
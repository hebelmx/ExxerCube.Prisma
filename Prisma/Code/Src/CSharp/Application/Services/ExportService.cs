using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Orchestrates Stage 4 workflow: SIRO XML/PDF export generation, schema validation, and Excel layout generation.
/// </summary>
public class ExportService
{
    private readonly IResponseExporter _responseExporter;
    private readonly ILayoutGenerator _layoutGenerator;
    private readonly ICriterionMapper _criterionMapper;
    private readonly IPdfRequirementSummarizer _pdfRequirementSummarizer;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<ExportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="responseExporter">The response exporter service.</param>
    /// <param name="layoutGenerator">The layout generator service.</param>
    /// <param name="criterionMapper">The criterion mapper service.</param>
    /// <param name="pdfRequirementSummarizer">The PDF requirement summarizer service.</param>
    /// <param name="auditLogger">The audit logger service.</param>
    /// <param name="logger">The logger instance.</param>
    public ExportService(
        IResponseExporter responseExporter,
        ILayoutGenerator layoutGenerator,
        ICriterionMapper criterionMapper,
        IPdfRequirementSummarizer pdfRequirementSummarizer,
        IAuditLogger auditLogger,
        ILogger<ExportService> logger)
    {
        _responseExporter = responseExporter;
        _layoutGenerator = layoutGenerator;
        _criterionMapper = criterionMapper;
        _pdfRequirementSummarizer = pdfRequirementSummarizer;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Exports unified metadata record to SIRO-compliant XML format.
    /// </summary>
    /// <param name="metadata">The unified metadata record to export.</param>
    /// <param name="outputStream">The output stream to write the XML content.</param>
    /// <param name="fileId">The file identifier (optional, for audit logging).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages (optional, generates new if not provided).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> ExportSiroXmlAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        string? fileId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SIRO XML export cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        // Input validation
        if (metadata == null)
        {
            return Result.WithFailure("Metadata cannot be null");
        }

        if (outputStream == null)
        {
            return Result.WithFailure("Output stream cannot be null");
        }

        // Generate correlation ID if not provided
        var actualCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Starting SIRO XML export orchestration for expediente: {Expediente} (CorrelationId: {CorrelationId})", metadata.Expediente?.NumeroExpediente ?? "Unknown", actualCorrelationId);

            // Validate data completeness before export
            var validationResult = ValidateMetadataCompleteness(metadata);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            // Export to SIRO XML format
            var exportResult = await _responseExporter.ExportSiroXmlAsync(metadata, outputStream, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation FIRST
            if (exportResult.IsCancelled())
            {
                _logger.LogWarning("SIRO XML export cancelled by exporter");
                return ResultExtensions.Cancelled();
            }

            if (exportResult.IsFailure)
            {
                _logger.LogError("SIRO XML export failed: {Error}", exportResult.Error);
                
                // Log audit for failed export
                var exportDetails = $"{{\"Expediente\":\"{metadata.Expediente?.NumeroExpediente ?? "Unknown"}\",\"Oficio\":\"{metadata.Expediente?.NumeroOficio ?? "Unknown"}\",\"Format\":\"SIRO XML\"}}";
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Export,
                    ProcessingStage.Export,
                    fileId,
                    actualCorrelationId,
                    null,
                    exportDetails,
                    false,
                    exportResult.Error,
                    cancellationToken).ConfigureAwait(false);
                
                return Result.WithFailure($"SIRO XML export failed: {exportResult.Error}");
            }

            // Log audit for successful export
            var successDetails = $"{{\"Expediente\":\"{metadata.Expediente?.NumeroExpediente ?? "Unknown"}\",\"Oficio\":\"{metadata.Expediente?.NumeroOficio ?? "Unknown"}\",\"Format\":\"SIRO XML\"}}";
            await _auditLogger.LogAuditAsync(
                AuditActionType.Export,
                ProcessingStage.Export,
                fileId,
                actualCorrelationId,
                null,
                successDetails,
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully completed SIRO XML export for expediente: {Expediente}", metadata.Expediente?.NumeroExpediente ?? "Unknown");
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("SIRO XML export cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error orchestrating SIRO XML export");
            return Result.WithFailure($"Error orchestrating SIRO XML export: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates Excel layout file from unified metadata record for SIRO registration systems.
    /// </summary>
    /// <param name="metadata">The unified metadata record to generate layout from.</param>
    /// <param name="outputStream">The output stream to write the Excel content.</param>
    /// <param name="fileId">The file identifier (optional, for audit logging).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages (optional, generates new if not provided).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> GenerateExcelLayoutAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        string? fileId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Excel layout generation cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        // Input validation
        if (metadata == null)
        {
            return Result.WithFailure("Metadata cannot be null");
        }

        if (outputStream == null)
        {
            return Result.WithFailure("Output stream cannot be null");
        }

        // Generate correlation ID if not provided
        var actualCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Starting Excel layout generation orchestration for expediente: {Expediente} (CorrelationId: {CorrelationId})", metadata.Expediente?.NumeroExpediente ?? "Unknown", actualCorrelationId);

            // Validate data completeness
            if (metadata.Expediente == null)
            {
                return Result.WithFailure("Expediente is required for Excel layout generation");
            }

            // Generate Excel layout
            var layoutResult = await _layoutGenerator.GenerateExcelLayoutAsync(metadata, outputStream, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation FIRST
            if (layoutResult.IsCancelled())
            {
                _logger.LogWarning("Excel layout generation cancelled by generator");
                return ResultExtensions.Cancelled();
            }

            if (layoutResult.IsFailure)
            {
                _logger.LogError("Excel layout generation failed: {Error}", layoutResult.Error);
                
                // Log audit for failed layout generation
                var layoutDetails = $"{{\"Expediente\":\"{metadata.Expediente.NumeroExpediente}\",\"Format\":\"Excel\"}}";
                await _auditLogger.LogAuditAsync(
                    AuditActionType.Export,
                    ProcessingStage.Export,
                    fileId,
                    actualCorrelationId,
                    null,
                    layoutDetails,
                    false,
                    layoutResult.Error,
                    cancellationToken).ConfigureAwait(false);
                
                return Result.WithFailure($"Excel layout generation failed: {layoutResult.Error}");
            }

            // Log audit for successful layout generation
            var successLayoutDetails = $"{{\"Expediente\":\"{metadata.Expediente.NumeroExpediente}\",\"Format\":\"Excel\"}}";
            await _auditLogger.LogAuditAsync(
                AuditActionType.Export,
                ProcessingStage.Export,
                fileId,
                actualCorrelationId,
                null,
                successLayoutDetails,
                true,
                null,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully completed Excel layout generation for expediente: {Expediente}", metadata.Expediente.NumeroExpediente);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Excel layout generation cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error orchestrating Excel layout generation");
            return Result.WithFailure($"Error orchestrating Excel layout generation: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Maps compliance requirements to SIRO regulatory criteria.
    /// </summary>
    /// <param name="requirements">The list of compliance requirements to map.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the mapped SIRO criteria or an error.</returns>
    public async Task<Result<Dictionary<string, object>>> MapToSiroCriteriaAsync(
        List<ComplianceRequirement> requirements,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Criterion mapping cancelled before starting");
            return ResultExtensions.Cancelled<Dictionary<string, object>>();
        }

        // Input validation
        if (requirements == null)
        {
            return Result<Dictionary<string, object>>.WithFailure("Requirements cannot be null");
        }

        try
        {
            _logger.LogInformation("Starting criterion mapping orchestration for {Count} requirements", requirements.Count);

            // Map to SIRO criteria
            var mappingResult = await _criterionMapper.MapToSiroCriteriaAsync(requirements, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation
            if (mappingResult.IsCancelled())
            {
                _logger.LogWarning("Criterion mapping cancelled by mapper");
                return ResultExtensions.Cancelled<Dictionary<string, object>>();
            }

            if (mappingResult.IsFailure)
            {
                _logger.LogError("Criterion mapping failed: {Error}", mappingResult.Error);
                return Result<Dictionary<string, object>>.WithFailure($"Criterion mapping failed: {mappingResult.Error}");
            }

            _logger.LogInformation("Successfully completed criterion mapping for {Count} requirements", requirements.Count);
            return mappingResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Criterion mapping cancelled");
            return ResultExtensions.Cancelled<Dictionary<string, object>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error orchestrating criterion mapping");
            return Result<Dictionary<string, object>>.WithFailure($"Error orchestrating criterion mapping: {ex.Message}", default(Dictionary<string, object>), ex);
        }
    }

    /// <summary>
    /// Exports unified metadata record to digitally signed PDF format with requirement summarization.
    /// Orchestrates PDF summarization → PDF generation → digital signing workflow.
    /// </summary>
    /// <param name="metadata">The unified metadata record to export.</param>
    /// <param name="pdfContent">The original PDF content for requirement summarization (optional).</param>
    /// <param name="outputStream">The output stream to write the signed PDF content.</param>
    /// <param name="fileId">The file identifier (optional, for audit logging).</param>
    /// <param name="correlationId">The correlation ID for tracking requests across stages (optional, generates new if not provided).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> ExportSignedPdfWithSummarizationAsync(
        UnifiedMetadataRecord metadata,
        byte[]? pdfContent,
        Stream outputStream,
        string? fileId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Signed PDF export cancelled before starting");
            return ResultExtensions.Cancelled();
        }

        // Input validation
        if (metadata == null)
        {
            return Result.WithFailure("Metadata cannot be null");
        }

        if (outputStream == null)
        {
            return Result.WithFailure("Output stream cannot be null");
        }

        // Generate correlation ID if not provided
        var actualCorrelationId = correlationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Starting signed PDF export with summarization for expediente: {Expediente} (CorrelationId: {CorrelationId})",
                metadata.Expediente?.NumeroExpediente ?? "Unknown", actualCorrelationId);

            // Step 1: Summarize PDF requirements if PDF content provided
            if (pdfContent != null && pdfContent.Length > 0)
            {
                _logger.LogDebug("Summarizing PDF requirements from provided PDF content");
                var summarizationResult = await _pdfRequirementSummarizer.SummarizeRequirementsAsync(
                    pdfContent, cancellationToken).ConfigureAwait(false);

                // Propagate cancellation
                if (summarizationResult.IsCancelled())
                {
                    _logger.LogWarning("PDF summarization cancelled");
                    return ResultExtensions.Cancelled();
                }

                if (summarizationResult.IsFailure)
                {
                    _logger.LogWarning("PDF summarization failed: {Error}. Continuing without summary.", summarizationResult.Error);
                    // Continue without summary - not a blocking error
                }
                else if (summarizationResult.Value != null)
                {
                    // Add requirement summary to metadata
                    metadata.RequirementSummary = summarizationResult.Value;
                    _logger.LogInformation("Successfully summarized {Count} requirements into categories",
                        summarizationResult.Value.Requirements.Count);
                }
            }
            else if (metadata.RequirementSummary == null)
            {
                _logger.LogDebug("No PDF content provided and no existing requirement summary. PDF will be generated without summarization.");
            }

            // Step 2: Generate and sign PDF
            var exportResult = await _responseExporter.ExportSignedPdfAsync(
                metadata, outputStream, cancellationToken).ConfigureAwait(false);

            // Log PDF export audit
            var exportDetails = $"{{\"Expediente\":\"{metadata.Expediente?.NumeroExpediente ?? "Unknown"}\",\"Oficio\":\"{metadata.Expediente?.NumeroOficio ?? "Unknown"}\",\"Format\":\"Signed PDF\",\"HasSummary\":{metadata.RequirementSummary != null}}}";
            await _auditLogger.LogAuditAsync(
                AuditActionType.Export,
                ProcessingStage.Export,
                fileId,
                actualCorrelationId,
                null,
                exportDetails,
                exportResult.IsSuccess,
                exportResult.IsFailure ? exportResult.Error : null,
                cancellationToken).ConfigureAwait(false);

            // Propagate cancellation
            if (exportResult.IsCancelled())
            {
                _logger.LogWarning("Signed PDF export cancelled by exporter");
                return ResultExtensions.Cancelled();
            }

            if (exportResult.IsFailure)
            {
                _logger.LogError("Signed PDF export failed: {Error}", exportResult.Error);
                return Result.WithFailure($"Signed PDF export failed: {exportResult.Error}");
            }

            _logger.LogInformation("Successfully completed signed PDF export with summarization for expediente: {Expediente}",
                metadata.Expediente?.NumeroExpediente ?? "Unknown");
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Signed PDF export cancelled");
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error orchestrating signed PDF export");
            return Result.WithFailure($"Error orchestrating signed PDF export: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that metadata contains all required fields for export.
    /// </summary>
    /// <param name="metadata">The metadata to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private Result ValidateMetadataCompleteness(UnifiedMetadataRecord metadata)
    {
        if (metadata.Expediente == null)
        {
            var missing = new ValidationState();
            missing.Require(false, "Expediente");
            metadata.Validation = missing;
            return Result.WithFailure($"Validation failed: {string.Join(", ", missing.Missing)}");
        }

        var validation = metadata.Validation ?? new ValidationState();
        validation.Require(!string.IsNullOrWhiteSpace(metadata.Expediente.NumeroExpediente), "Expediente");
        validation.Require(!string.IsNullOrWhiteSpace(metadata.Expediente.NumeroOficio), "NumeroOficio");
        validation.Require(!string.IsNullOrWhiteSpace(metadata.Expediente.FundamentoLegal), "FundamentoLegal");
        validation.Require(!string.IsNullOrWhiteSpace(metadata.Expediente.MedioEnvio), "MedioEnvio");
        validation.Require(metadata.Expediente.Subdivision != LegalSubdivisionKind.Unknown, "Subdivision");
        validation.Require(metadata.Expediente.FechaRecepcion != default, "FechaRecepcion");
        validation.Require(metadata.Expediente.FechaEstimadaConclusion != default, "FechaEstimadaConclusion");

        foreach (var action in metadata.ComplianceActions)
        {
            validation.Require(action.ActionType != ComplianceActionKind.Unknown, "ComplianceAction.ActionType");
            if (action.ActionType == ComplianceActionKind.Block ||
                action.ActionType == ComplianceActionKind.Unblock ||
                action.ActionType == ComplianceActionKind.Transfer)
            {
                var hasAccount = !string.IsNullOrWhiteSpace(action.AccountNumber) ||
                                 !string.IsNullOrWhiteSpace(action.Cuenta?.Numero);
                validation.Require(hasAccount, "ComplianceAction.Account");
            }
        }

        // Additional merged fields: warn on conflicts and unknowns
        foreach (var conflict in metadata.AdditionalFieldConflicts)
        {
            validation.Warn($"Conflict:{conflict}");
        }

        // Example required additional fields: Subdivision, MeasureHint
        validation.WarnIf(metadata.AdditionalFields.TryGetValue("Subdivision", out var subdivision) &&
                          !string.Equals(subdivision, "Unknown", StringComparison.OrdinalIgnoreCase),
            "Subdivision");
        validation.WarnIf(metadata.AdditionalFields.TryGetValue("MeasureHint", out var measure) &&
                          !string.Equals(measure, "Informacion", StringComparison.OrdinalIgnoreCase),
            "MeasureHint");

        metadata.Validation = validation;

        return validation.IsValid
            ? Result.Success()
            : Result.WithFailure($"Validation failed: {string.Join(", ", validation.Missing)}");
    }
}

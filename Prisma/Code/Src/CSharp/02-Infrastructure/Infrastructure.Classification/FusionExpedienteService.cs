using FuzzySharp;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Multi-source data fusion service for reconciling Expediente data from XML, PDF, and DOCX sources.
/// Implements weighted voting algorithm with dynamic source reliability calculation.
/// </summary>
/// <remarks>
/// Fusion Algorithm:
/// 1. Calculate dynamic source reliability from ExtractionMetadata (OCR confidence, image quality, extraction success)
/// 2. For each field: exact match → fuzzy match (85% threshold) → weighted voting
/// 3. Calculate overall confidence weighted by field importance (required fields have higher weight)
/// 4. Determine NextAction based on confidence thresholds (AutoProcess: &gt;0.85, ManualReview: &lt;0.70)
///
/// DRY Principle Applied:
/// - Extractors pre-calculate all quality metrics in ExtractionMetadata
/// - Fusion service uses these metrics for dynamic weighting, no duplicate validation
/// </remarks>
public class FusionExpedienteService : IFusionExpediente
{
    private readonly ILogger<FusionExpedienteService> _logger;
    private readonly FusionCoefficients _coefficients;

    /// <summary>
    /// Initializes a new instance of the <see cref="FusionExpedienteService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="coefficients">Fusion coefficients (optional, defaults to FusionCoefficients).</param>
    public FusionExpedienteService(
        ILogger<FusionExpedienteService> logger,
        FusionCoefficients? coefficients = null)
    {
        _logger = logger;
        _coefficients = coefficients ?? new FusionCoefficients();
    }

    /// <inheritdoc />
    public async Task<Result<FusionResult>> FuseAsync(
        Expediente? xmlExpediente,
        Expediente? pdfExpediente,
        Expediente? docxExpediente,
        ExtractionMetadata xmlMetadata,
        ExtractionMetadata pdfMetadata,
        ExtractionMetadata docxMetadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting multi-source Expediente fusion");

            // Validate at least one source is available
            if (xmlExpediente == null && pdfExpediente == null && docxExpediente == null)
            {
                return Result<FusionResult>.WithFailure("At least one source Expediente must be provided");
            }

            // Calculate dynamic source reliabilities
            var sourceReliabilities = CalculateSourceReliabilities(xmlMetadata, pdfMetadata, docxMetadata);

            _logger.LogDebug("Source reliabilities - XML: {XML:F2}, PDF: {PDF:F2}, DOCX: {DOCX:F2}",
                sourceReliabilities[SourceType.XML_HandFilled],
                sourceReliabilities[SourceType.PDF_OCR_CNBV],
                sourceReliabilities[SourceType.DOCX_OCR_Authority]);

            // Fuse all fields
            var fusedExpediente = new Expediente();
            var fieldResults = new Dictionary<string, FieldFusionResult>();
            var conflictingFields = new List<string>();

            // Fuse critical fields
            await FuseNumeroExpedienteAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseNumeroOficioAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseAreaDescripcionAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseAutoridadNombreAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);

            // Calculate overall confidence
            var (overallConfidence, requiredFieldsScore, optionalFieldsScore) = CalculateOverallConfidence(fieldResults);

            // Determine next action
            var nextAction = DetermineNextAction(overallConfidence, conflictingFields.Count, fieldResults);

            // Check for missing required fields
            var missingRequiredFields = GetMissingRequiredFields(fusedExpediente);

            var fusionResult = new FusionResult
            {
                FusedExpediente = fusedExpediente,
                OverallConfidence = overallConfidence,
                RequiredFieldsScore = requiredFieldsScore,
                OptionalFieldsScore = optionalFieldsScore,
                ConflictingFields = conflictingFields,
                MissingRequiredFields = missingRequiredFields,
                NextAction = nextAction,
                FieldResults = fieldResults,
                SourceReliabilities = sourceReliabilities
            };

            _logger.LogInformation(
                "Fusion complete - Confidence: {Confidence:F2}, Conflicts: {Conflicts}, NextAction: {NextAction}",
                overallConfidence, conflictingFields.Count, nextAction);

            return await Task.FromResult(Result<FusionResult>.Success(fusionResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Expediente fusion");
            return Result<FusionResult>.WithFailure($"Fusion error: {ex.Message}", default(FusionResult), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FieldFusionResult>> FuseFieldAsync(
        string fieldName,
        List<FieldCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fusing field: {FieldName} with {CandidateCount} candidates", fieldName, candidates.Count);

            // Remove null/empty candidates
            var validCandidates = candidates.Where(c => !string.IsNullOrWhiteSpace(c.Value)).ToList();

            if (validCandidates.Count == 0)
            {
                // All sources null
                return Result<FieldFusionResult>.Success(new FieldFusionResult
                {
                    Value = null,
                    Confidence = 0.0,
                    Decision = FusionDecision.AllSourcesNull,
                    ContributingSources = new List<SourceType>()
                });
            }

            // Check for exact agreement
            var distinctValues = validCandidates.Select(c => c.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctValues.Count == 1)
            {
                // All agree exactly
                var agreedValue = validCandidates[0].Value;
                var avgReliability = validCandidates.Average(c => c.SourceReliability);

                return Result<FieldFusionResult>.Success(new FieldFusionResult
                {
                    Value = agreedValue,
                    Confidence = avgReliability,
                    Decision = FusionDecision.AllAgree,
                    ContributingSources = validCandidates.Select(c => c.Source).ToList()
                });
            }

            // Check for fuzzy agreement (for text fields like names)
            if (IsTextField(fieldName))
            {
                var fuzzyResult = TryFuzzyAgreement(validCandidates);
                if (fuzzyResult != null)
                {
                    return Result<FieldFusionResult>.Success(fuzzyResult);
                }
            }

            // Weighted voting - select value from source with highest reliability
            var winner = validCandidates.OrderByDescending(c => c.SourceReliability).First();

            var conflictingValues = validCandidates
                .Where(c => !string.Equals(c.Value, winner.Value, StringComparison.OrdinalIgnoreCase))
                .Select(c => (c.Source, c.Value))
                .ToList();

            var decision = conflictingValues.Count == 0 ? FusionDecision.AllAgree :
                          conflictingValues.Count >= validCandidates.Count - 1 ? FusionDecision.Conflict :
                          FusionDecision.WeightedVoting;

            return await Task.FromResult(Result<FieldFusionResult>.Success(new FieldFusionResult
            {
                Value = winner.Value,
                Confidence = winner.SourceReliability,
                Decision = decision,
                ContributingSources = validCandidates.Select(c => c.Source).ToList(),
                WinningSource = winner.Source,
                ConflictingValues = conflictingValues,
                RequiresManualReview = decision == FusionDecision.Conflict,
                SuggestReview = conflictingValues.Count > 0
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fusing field: {FieldName}", fieldName);
            return Result<FieldFusionResult>.WithFailure($"Field fusion error: {ex.Message}", default(FieldFusionResult), ex);
        }
    }

    #region Dynamic Source Reliability Calculation

    private Dictionary<SourceType, double> CalculateSourceReliabilities(
        ExtractionMetadata xmlMetadata,
        ExtractionMetadata pdfMetadata,
        ExtractionMetadata docxMetadata)
    {
        return new Dictionary<SourceType, double>
        {
            [SourceType.XML_HandFilled] = CalculateSourceReliability(SourceType.XML_HandFilled, xmlMetadata),
            [SourceType.PDF_OCR_CNBV] = CalculateSourceReliability(SourceType.PDF_OCR_CNBV, pdfMetadata),
            [SourceType.DOCX_OCR_Authority] = CalculateSourceReliability(SourceType.DOCX_OCR_Authority, docxMetadata)
        };
    }

    private double CalculateSourceReliability(SourceType sourceType, ExtractionMetadata metadata)
    {
        // Start with base reliability
        var baseReliability = sourceType.Value switch
        {
            1 => _coefficients.XML_BaseReliability,      // 0.60 - Hand-filled forms
            2 => _coefficients.PDF_BaseReliability,      // 0.85 - High quality CNBV scans
            3 => _coefficients.DOCX_BaseReliability,     // 0.70 - Authority scans
            _ => 0.50
        };

        // Dynamic adjustment based on extraction metadata
        var ocrWeight = _coefficients.OCR_ConfidenceWeight;           // 0.50
        var imageWeight = _coefficients.ImageQualityWeight;           // 0.30
        var extractionWeight = _coefficients.ExtractionSuccessWeight; // 0.20

        // OCR confidence adjustment
        var ocrAdjustment = 0.0;
        if (metadata.MeanConfidence.HasValue && sourceType != SourceType.XML_HandFilled)
        {
            ocrAdjustment = (metadata.MeanConfidence.Value - 0.75) * ocrWeight; // Normalize around 0.75
        }

        // Image quality adjustment
        var imageAdjustment = 0.0;
        if (metadata.QualityIndex.HasValue && sourceType != SourceType.XML_HandFilled)
        {
            imageAdjustment = (metadata.QualityIndex.Value - 0.75) * imageWeight; // Normalize around 0.75
        }

        // Extraction success adjustment
        var extractionAdjustment = 0.0;
        if (metadata.TotalFieldsExtracted > 0)
        {
            var successRate = (double)metadata.RegexMatches / metadata.TotalFieldsExtracted;
            var violationRate = (double)metadata.PatternViolations / metadata.TotalFieldsExtracted;
            extractionAdjustment = (successRate - violationRate) * extractionWeight;
        }

        // Calculate final reliability (clamped to 0.0-1.0)
        var reliability = baseReliability + ocrAdjustment + imageAdjustment + extractionAdjustment;
        return Math.Clamp(reliability, 0.0, 1.0);
    }

    #endregion

    #region Field Fusion Methods

    private async Task FuseNumeroExpedienteAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();
        if (xml != null) candidates.Add(new FieldCandidate { Value = xml.NumeroExpediente, Source = SourceType.XML_HandFilled, SourceReliability = reliabilities[SourceType.XML_HandFilled] });
        if (pdf != null) candidates.Add(new FieldCandidate { Value = pdf.NumeroExpediente, Source = SourceType.PDF_OCR_CNBV, SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV] });
        if (docx != null) candidates.Add(new FieldCandidate { Value = docx.NumeroExpediente, Source = SourceType.DOCX_OCR_Authority, SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority] });

        var result = await FuseFieldAsync("NumeroExpediente", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.NumeroExpediente = result.Value.Value ?? string.Empty;
            results["NumeroExpediente"] = result.Value;
            // Add to conflicts if there was disagreement (either resolved by voting or unresolved)
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("NumeroExpediente");
            }
        }
    }

    private async Task FuseNumeroOficioAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();
        if (xml != null) candidates.Add(new FieldCandidate { Value = xml.NumeroOficio, Source = SourceType.XML_HandFilled, SourceReliability = reliabilities[SourceType.XML_HandFilled] });
        if (pdf != null) candidates.Add(new FieldCandidate { Value = pdf.NumeroOficio, Source = SourceType.PDF_OCR_CNBV, SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV] });
        if (docx != null) candidates.Add(new FieldCandidate { Value = docx.NumeroOficio, Source = SourceType.DOCX_OCR_Authority, SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority] });

        var result = await FuseFieldAsync("NumeroOficio", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.NumeroOficio = result.Value.Value ?? string.Empty;
            results["NumeroOficio"] = result.Value;
            // Add to conflicts if there was disagreement (either resolved by voting or unresolved)
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("NumeroOficio");
            }
        }
    }

    private async Task FuseAreaDescripcionAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();
        if (xml != null) candidates.Add(new FieldCandidate { Value = xml.AreaDescripcion, Source = SourceType.XML_HandFilled, SourceReliability = reliabilities[SourceType.XML_HandFilled] });
        if (pdf != null) candidates.Add(new FieldCandidate { Value = pdf.AreaDescripcion, Source = SourceType.PDF_OCR_CNBV, SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV] });
        if (docx != null) candidates.Add(new FieldCandidate { Value = docx.AreaDescripcion, Source = SourceType.DOCX_OCR_Authority, SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority] });

        var result = await FuseFieldAsync("AreaDescripcion", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.AreaDescripcion = result.Value.Value ?? string.Empty;
            results["AreaDescripcion"] = result.Value;
            // Add to conflicts if there was disagreement (either resolved by voting or unresolved)
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("AreaDescripcion");
            }
        }
    }

    private async Task FuseAutoridadNombreAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();
        if (xml != null) candidates.Add(new FieldCandidate { Value = xml.AutoridadNombre, Source = SourceType.XML_HandFilled, SourceReliability = reliabilities[SourceType.XML_HandFilled] });
        if (pdf != null) candidates.Add(new FieldCandidate { Value = pdf.AutoridadNombre, Source = SourceType.PDF_OCR_CNBV, SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV] });
        if (docx != null) candidates.Add(new FieldCandidate { Value = docx.AutoridadNombre, Source = SourceType.DOCX_OCR_Authority, SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority] });

        var result = await FuseFieldAsync("AutoridadNombre", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.AutoridadNombre = result.Value.Value ?? string.Empty;
            results["AutoridadNombre"] = result.Value;
            // Add to conflicts if there was disagreement (either resolved by voting or unresolved)
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("AutoridadNombre");
            }
        }
    }

    #endregion

    #region Fuzzy Matching

    private static bool IsTextField(string fieldName)
    {
        var textFields = new[] { "AutoridadNombre", "NombreSolicitante", "Nombre", "Paterno", "Materno" };
        return textFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }

    private FieldFusionResult? TryFuzzyAgreement(List<FieldCandidate> candidates)
    {
        // Use FuzzySharp to calculate similarity between all pairs
        var values = candidates.Select(c => c.Value!).ToList();

        for (int i = 0; i < values.Count; i++)
        {
            for (int j = i + 1; j < values.Count; j++)
            {
                var similarity = Fuzz.Ratio(values[i], values[j]) / 100.0;

                if (similarity >= _coefficients.FuzzyMatchThreshold) // Default 0.85
                {
                    // Fuzzy match found - pick the value from the most reliable source
                    var winner = candidates.OrderByDescending(c => c.SourceReliability).First();

                    return new FieldFusionResult
                    {
                        Value = winner.Value,
                        Confidence = winner.SourceReliability * similarity, // Reduce confidence by similarity
                        Decision = FusionDecision.FuzzyAgreement,
                        FuzzySimilarity = similarity,
                        ContributingSources = candidates.Select(c => c.Source).ToList(),
                        WinningSource = winner.Source,
                        SuggestReview = true // Fuzzy matches should be reviewed
                    };
                }
            }
        }

        return null; // No fuzzy match found
    }

    #endregion

    #region Confidence Calculation

    private (double overall, double requiredFields, double optionalFields) CalculateOverallConfidence(
        Dictionary<string, FieldFusionResult> fieldResults)
    {
        // Required fields have higher weight
        var requiredFields = new[] { "NumeroExpediente", "NumeroOficio", "AreaDescripcion" };

        var requiredFieldsConfidence = fieldResults
            .Where(kvp => requiredFields.Contains(kvp.Key))
            .Average(kvp => kvp.Value.Confidence);

        var optionalFieldsConfidence = fieldResults
            .Where(kvp => !requiredFields.Contains(kvp.Key))
            .Select(kvp => kvp.Value.Confidence)
            .DefaultIfEmpty(0.0)
            .Average();

        // Weighted average: 70% required fields, 30% optional fields
        var overallConfidence = (requiredFieldsConfidence * 0.70) + (optionalFieldsConfidence * 0.30);

        return (overallConfidence, requiredFieldsConfidence, optionalFieldsConfidence);
    }

    private NextAction DetermineNextAction(double confidence, int conflictCount, Dictionary<string, FieldFusionResult> fieldResults)
    {
        // Manual review required if:
        // - Confidence below threshold
        // - Critical fields have conflicts
        // - Any field requires manual review
        if (confidence < _coefficients.ManualReviewThreshold ||
            conflictCount > 0 ||
            fieldResults.Values.Any(f => f.RequiresManualReview))
        {
            return NextAction.ManualReviewRequired;
        }

        // Auto-process if confidence is high
        if (confidence >= _coefficients.AutoProcessThreshold)
        {
            return NextAction.AutoProcess;
        }

        // Review recommended for medium confidence
        return NextAction.ReviewRecommended;
    }

    private static List<string> GetMissingRequiredFields(Expediente expediente)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(expediente.NumeroExpediente)) missing.Add("NumeroExpediente");
        if (string.IsNullOrWhiteSpace(expediente.NumeroOficio)) missing.Add("NumeroOficio");
        if (string.IsNullOrWhiteSpace(expediente.AreaDescripcion)) missing.Add("AreaDescripcion");

        return missing;
    }

    #endregion
}

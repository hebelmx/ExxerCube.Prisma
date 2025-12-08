using FuzzySharp;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Domain.Sanitizers;
using ExxerCube.Prisma.Domain.Validators;

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

            // Fuse high-value R29 fields (Phase 2 Task 3)
            await FuseSolicitudSiaraAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseFechaRecepcionAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseFechaPublicacionAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);

            // Fuse titular fields (first SolicitudParte)
            await FusePrimaryTitularFieldsAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);

            // Fuse additional R29 fields (Phase 2 Task 4)
            await FuseFundamentoLegalAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseDiasPlazoAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseFechaRegistroAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseNombreSolicitanteAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseAutoridadEspecificaNombreAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseFolioAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseMedioEnvioAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseOficioYearAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseOficioOrigenAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseAcuerdoReferenciaAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseEvidenciaFirmaAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseReferenciaAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseReferencia1Async(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseReferencia2Async(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseAreaClaveAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseSubdivisionAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            await FuseTieneAseguramientoAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);
            // Fuse primary solicitud especifica fields (first SolicitudEspecifica)
            await FusePrimarySolicitudEspecificaFieldsAsync(xmlExpediente, pdfExpediente, docxExpediente, sourceReliabilities, fusedExpediente, fieldResults, conflictingFields, cancellationToken);


            // Calculate FechaEstimadaConclusion (FechaRecepcion + DiasPlazo business days)
            if (fusedExpediente.FechaRecepcion != default && fusedExpediente.DiasPlazo > 0)
            {
                fusedExpediente.FechaEstimadaConclusion = CalculateBusinessDays(fusedExpediente.FechaRecepcion, fusedExpediente.DiasPlazo);
                _logger.LogDebug("Calculated FechaEstimadaConclusion: {FechaEstimada} (FechaRecepcion: {FechaRecepcion} + {DiasPlazo} business days)",
                    fusedExpediente.FechaEstimadaConclusion, fusedExpediente.FechaRecepcion, fusedExpediente.DiasPlazo);
            }

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
                // All agree exactly - unanimous agreement boosts confidence
                // Use max reliability instead of average because agreement is strong evidence
                var agreedValue = validCandidates[0].Value;
                var maxReliability = validCandidates.Max(c => c.SourceReliability);

                return Result<FieldFusionResult>.Success(new FieldFusionResult
                {
                    Value = agreedValue,
                    Confidence = maxReliability,
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

    private async Task FuseSolicitudSiaraAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudSiara);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudSiara);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudSiara);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("SolicitudSiara", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.SolicitudSiara = result.Value.Value ?? string.Empty;
            results["SolicitudSiara"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("SolicitudSiara");
            }
        }
    }

    private async Task FuseFechaRecepcionAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null && xml.FechaRecepcion != default)
        {
            var dateString = xml.FechaRecepcion.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        if (pdf != null && pdf.FechaRecepcion != default)
        {
            var dateString = pdf.FechaRecepcion.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        if (docx != null && docx.FechaRecepcion != default)
        {
            var dateString = docx.FechaRecepcion.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        var result = await FuseFieldAsync("FechaRecepcion", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (DateTime.TryParseExact(result.Value.Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                fused.FechaRecepcion = date;
                results["FechaRecepcion"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("FechaRecepcion");
                }
            }
        }
    }

    private async Task FuseFechaPublicacionAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null && xml.FechaPublicacion != default)
        {
            var dateString = xml.FechaPublicacion.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        if (pdf != null && pdf.FechaPublicacion != default)
        {
            var dateString = pdf.FechaPublicacion.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        if (docx != null && docx.FechaPublicacion != default)
        {
            var dateString = docx.FechaPublicacion.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        var result = await FuseFieldAsync("FechaPublicacion", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (DateTime.TryParseExact(result.Value.Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                fused.FechaPublicacion = date;
                results["FechaPublicacion"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("FechaPublicacion");
                }
            }
        }
    }

    private async Task FusePrimaryTitularFieldsAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        // Ensure fusedExpediente has at least one SolicitudParte for the primary titular
        if (fused.SolicitudPartes.Count == 0)
        {
            fused.SolicitudPartes.Add(new SolicitudParte());
        }

        var fusedTitular = fused.SolicitudPartes[0];

        // Fuse RFC
        await FuseTitularRfcAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);

        // Fuse CURP
        await FuseTitularCurpAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);

        // Fuse Nombre
        await FuseTitularNombreAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);

        // Fuse Paterno
        await FuseTitularPaternoAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);

        // Fuse Materno
        await FuseTitularMaternoAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);

        // Fuse PersonaTipo (Fisica/Moral)
        await FuseTitularPersonaTipoAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);

        // Fuse Caracter (role/character)
        await FuseTitularCaracterAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);
        await FuseTitularRelacionAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);
        await FuseTitularDomicilioAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);
        await FuseTitularComplementariosAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);
        await FuseTitularFechaNacimientoAsync(xml, pdf, docx, reliabilities, fusedTitular, results, conflicts, cancellationToken);
    }

    private async Task FuseTitularRfcAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Rfc);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidRFC(sanitized)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Rfc);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidRFC(sanitized)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Rfc);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidRFC(sanitized)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_RFC", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Rfc = result.Value.Value;
            results["Titular_RFC"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_RFC");
            }
        }
    }

    private async Task FuseTitularCurpAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Curp);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidCURP(sanitized)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Curp);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidCURP(sanitized)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Curp);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidCURP(sanitized)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_CURP", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Curp = result.Value.Value ?? string.Empty;
            results["Titular_CURP"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_CURP");
            }
        }
    }

    private async Task FuseTitularNombreAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Nombre);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Nombre);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Nombre);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Nombre", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Nombre = result.Value.Value ?? string.Empty;
            results["Titular_Nombre"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Nombre");
            }
        }
    }

    private async Task FuseTitularPaternoAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Paterno);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Paterno);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Paterno);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Paterno", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Paterno = result.Value.Value;
            results["Titular_Paterno"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Paterno");
            }
        }
    }

    private async Task FuseTitularMaternoAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Materno);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Materno);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Materno);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Materno", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Materno = result.Value.Value;
            results["Titular_Materno"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Materno");
            }
        }
    }

    private async Task FuseFundamentoLegalAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.FundamentoLegal);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.FundamentoLegal);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.FundamentoLegal);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        var result = await FuseFieldAsync("FundamentoLegal", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.FundamentoLegal = result.Value.Value ?? string.Empty;
            results["FundamentoLegal"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("FundamentoLegal");
            }
        }
    }

    private async Task FuseDiasPlazoAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null && xml.DiasPlazo > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.DiasPlazo.ToString(),
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true // int is always valid if > 0
            });
        }

        if (pdf != null && pdf.DiasPlazo > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.DiasPlazo.ToString(),
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        if (docx != null && docx.DiasPlazo > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.DiasPlazo.ToString(),
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        var result = await FuseFieldAsync("DiasPlazo", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (int.TryParse(result.Value.Value, out var days))
            {
                fused.DiasPlazo = days;
                results["DiasPlazo"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("DiasPlazo");
                }
            }
        }
    }

    private async Task FuseFechaRegistroAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null && xml.FechaRegistro != default)
        {
            var dateString = xml.FechaRegistro.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        if (pdf != null && pdf.FechaRegistro != default)
        {
            var dateString = pdf.FechaRegistro.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        if (docx != null && docx.FechaRegistro != default)
        {
            var dateString = docx.FechaRegistro.ToString("yyyyMMdd");
            candidates.Add(new FieldCandidate
            {
                Value = dateString,
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
            });
        }

        var result = await FuseFieldAsync("FechaRegistro", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (DateTime.TryParseExact(result.Value.Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                fused.FechaRegistro = date;
                results["FechaRegistro"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("FechaRegistro");
                }
            }
        }
    }

    private async Task FuseNombreSolicitanteAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.NombreSolicitante);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.NombreSolicitante);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.NombreSolicitante);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("NombreSolicitante", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.NombreSolicitante = result.Value.Value;
            results["NombreSolicitante"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("NombreSolicitante");
            }
        }
    }

    private async Task FuseAutoridadEspecificaNombreAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.AutoridadEspecificaNombre);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 250)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.AutoridadEspecificaNombre);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 250)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.AutoridadEspecificaNombre);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 250)
                });
            }
        }

        var result = await FuseFieldAsync("AutoridadEspecificaNombre", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.AutoridadEspecificaNombre = result.Value.Value;
            results["AutoridadEspecificaNombre"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("AutoridadEspecificaNombre");
            }
        }
    }

    private async Task FuseFolioAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null && xml.Folio > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.Folio.ToString(),
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true // int is always valid if > 0
            });
        }

        if (pdf != null && pdf.Folio > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.Folio.ToString(),
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        if (docx != null && docx.Folio > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.Folio.ToString(),
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        var result = await FuseFieldAsync("Folio", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (int.TryParse(result.Value.Value, out var folio))
            {
                fused.Folio = folio;
                results["Folio"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("Folio");
                }
            }
        }
    }

    private async Task FuseMedioEnvioAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.MedioEnvio);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 50)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.MedioEnvio);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 50)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.MedioEnvio);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 50)
                });
            }
        }

        var result = await FuseFieldAsync("MedioEnvio", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.MedioEnvio = result.Value.Value ?? string.Empty;
            results["MedioEnvio"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("MedioEnvio");
            }
        }
    }

    private async Task FuseOficioYearAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate
        if (xml != null && xml.OficioYear > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.OficioYear.ToString(),
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true // int is always valid if > 0
            });
        }

        // PDF candidate
        if (pdf != null && pdf.OficioYear > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.OficioYear.ToString(),
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        // DOCX candidate
        if (docx != null && docx.OficioYear > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.OficioYear.ToString(),
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        // Fuse
        var result = await FuseFieldAsync("OficioYear", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (int.TryParse(result.Value.Value, out var oficioYear))
            {
                fused.OficioYear = oficioYear;
                results["OficioYear"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("OficioYear");
                }
            }
        }
    }

    private async Task FuseOficioOrigenAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.OficioOrigen);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.OficioOrigen);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.OficioOrigen);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("OficioOrigen", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.OficioOrigen = result.Value.Value ?? string.Empty;
            results["OficioOrigen"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("OficioOrigen");
            }
        }
    }

    private async Task FuseAcuerdoReferenciaAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.AcuerdoReferencia);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 200)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.AcuerdoReferencia);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 200)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.AcuerdoReferencia);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 200)
                });
            }
        }

        var result = await FuseFieldAsync("AcuerdoReferencia", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.AcuerdoReferencia = result.Value.Value ?? string.Empty;
            results["AcuerdoReferencia"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("AcuerdoReferencia");
            }
        }
    }

    private async Task FuseEvidenciaFirmaAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.EvidenciaFirma);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.EvidenciaFirma);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.EvidenciaFirma);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("EvidenciaFirma", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.EvidenciaFirma = result.Value.Value ?? string.Empty;
            results["EvidenciaFirma"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("EvidenciaFirma");
            }
        }
    }

    private async Task FuseReferenciaAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.Referencia);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.Referencia);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.Referencia);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Referencia", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.Referencia = result.Value.Value ?? string.Empty;
            results["Referencia"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Referencia");
            }
        }
    }

    private async Task FuseReferencia1Async(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.Referencia1);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.Referencia1);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.Referencia1);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Referencia1", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.Referencia1 = result.Value.Value ?? string.Empty;
            results["Referencia1"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Referencia1");
            }
        }
    }

    private async Task FuseReferencia2Async(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml != null)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.Referencia2);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf != null)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.Referencia2);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx != null)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.Referencia2);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Referencia2", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.Referencia2 = result.Value.Value ?? string.Empty;
            results["Referencia2"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Referencia2");
            }
        }
    }

    private async Task FuseAreaClaveAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate
        if (xml != null && xml.AreaClave > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.AreaClave.ToString(),
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true // int is always valid if > 0
            });
        }

        // PDF candidate
        if (pdf != null && pdf.AreaClave > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.AreaClave.ToString(),
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        // DOCX candidate
        if (docx != null && docx.AreaClave > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.AreaClave.ToString(),
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        // Fuse
        var result = await FuseFieldAsync("AreaClave", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (int.TryParse(result.Value.Value, out var areaClave))
            {
                fused.AreaClave = areaClave;
                results["AreaClave"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("AreaClave");
                }
            }
        }
    }

    private async Task FuseSubdivisionAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate
        if (xml != null && xml.Subdivision != null && xml.Subdivision.Value > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.Subdivision.Name,
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true // SmartEnum validated at parse time
            });
        }

        // PDF candidate
        if (pdf != null && pdf.Subdivision != null && pdf.Subdivision.Value > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.Subdivision.Name,
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        // DOCX candidate
        if (docx != null && docx.Subdivision != null && docx.Subdivision.Value > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.Subdivision.Name,
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        // Fuse
        var result = await FuseFieldAsync("Subdivision", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            try
            {
                fused.Subdivision = LegalSubdivisionKind.FromName(result.Value.Value);
                results["Subdivision"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("Subdivision");
                }
            }
            catch
            {
                // If name not recognized, default to Unknown
                fused.Subdivision = LegalSubdivisionKind.Unknown;
            }
        }
    }

    private async Task FuseTieneAseguramientoAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate - only add if explicitly true (false is default)
        if (xml != null && xml.TieneAseguramiento)
        {
            candidates.Add(new FieldCandidate
            {
                Value = "true",
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true // bool is always valid
            });
        }

        // PDF candidate
        if (pdf != null && pdf.TieneAseguramiento)
        {
            candidates.Add(new FieldCandidate
            {
                Value = "true",
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        // DOCX candidate
        if (docx != null && docx.TieneAseguramiento)
        {
            candidates.Add(new FieldCandidate
            {
                Value = "true",
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        // Fuse - if any source says true with sufficient confidence, set to true
        var result = await FuseFieldAsync("TieneAseguramiento", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (bool.TryParse(result.Value.Value, out var tieneAseguramiento))
            {
                fused.TieneAseguramiento = tieneAseguramiento;
                results["TieneAseguramiento"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("TieneAseguramiento");
                }
            }
        }
    }

    private async Task FuseTitularPersonaTipoAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].PersonaTipo);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 50)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].PersonaTipo);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 50)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].PersonaTipo);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 50)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_PersonaTipo", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.PersonaTipo = result.Value.Value ?? string.Empty;
            results["Titular_PersonaTipo"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_PersonaTipo");
            }
        }
    }

    private async Task FuseTitularCaracterAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Caracter);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Caracter);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Caracter);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 100)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Caracter", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Caracter = result.Value.Value ?? string.Empty;
            results["Titular_Caracter"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Caracter");
            }
        }
    }


    private async Task FuseTitularRelacionAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Relacion);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 200)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Relacion);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 200)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Relacion);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 200)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Relacion", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Relacion = result.Value.Value;
            results["Titular_Relacion"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Relacion");
            }
        }
    }
    #endregion

    private async Task FuseTitularDomicilioAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Domicilio);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Domicilio);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Domicilio);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Domicilio", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Domicilio = result.Value.Value;
            results["Titular_Domicilio"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Domicilio");
            }
        }
    }


    private async Task FuseTitularComplementariosAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(xml.SolicitudPartes[0].Complementarios);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(pdf.SolicitudPartes[0].Complementarios);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var sanitized = FieldSanitizer.Sanitize(docx.SolicitudPartes[0].Complementarios);
            if (sanitized != null)
            {
                candidates.Add(new FieldCandidate
                {
                    Value = sanitized,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidTextField(sanitized, 500)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_Complementarios", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fusedTitular.Complementarios = result.Value.Value;
            results["Titular_Complementarios"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("Titular_Complementarios");
            }
        }
    }
    #region Fuzzy Matching

    private async Task FuseTitularFechaNacimientoAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        SolicitudParte fusedTitular, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        if (xml?.SolicitudPartes.Count > 0)
        {
            var fechaNac = xml.SolicitudPartes[0].FechaNacimiento;
            if (fechaNac.HasValue)
            {
                var dateString = fechaNac.Value.ToString("yyyyMMdd");
                candidates.Add(new FieldCandidate
                {
                    Value = dateString,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled],
                    MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
                });
            }
        }

        if (pdf?.SolicitudPartes.Count > 0)
        {
            var fechaNac = pdf.SolicitudPartes[0].FechaNacimiento;
            if (fechaNac.HasValue)
            {
                var dateString = fechaNac.Value.ToString("yyyyMMdd");
                candidates.Add(new FieldCandidate
                {
                    Value = dateString,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                    MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
                });
            }
        }

        if (docx?.SolicitudPartes.Count > 0)
        {
            var fechaNac = docx.SolicitudPartes[0].FechaNacimiento;
            if (fechaNac.HasValue)
            {
                var dateString = fechaNac.Value.ToString("yyyyMMdd");
                candidates.Add(new FieldCandidate
                {
                    Value = dateString,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                    MatchesPattern = FieldPatternValidator.IsValidDate(dateString)
                });
            }
        }

        var result = await FuseFieldAsync("Titular_FechaNacimiento", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (DateOnly.TryParseExact(result.Value.Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fechaNacimiento))
            {
                fusedTitular.FechaNacimiento = fechaNacimiento;
                results["Titular_FechaNacimiento"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("Titular_FechaNacimiento");
                }
            }
        }
    }

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

        // Only include fields with actual data (exclude AllSourcesNull) to avoid diluting confidence
        var fieldsWithData = fieldResults
            .Where(kvp => kvp.Value.Decision != FusionDecision.AllSourcesNull)
            .ToList();

        var requiredFieldsConfidence = fieldsWithData
            .Where(kvp => requiredFields.Contains(kvp.Key))
            .Select(kvp => kvp.Value.Confidence)
            .DefaultIfEmpty(0.0)
            .Average();

        var optionalFieldsConfidence = fieldsWithData
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

    /// <summary>
    /// Fuses fields from the primary SolicitudEspecifica (first in collection).
    /// </summary>
    private async Task FusePrimarySolicitudEspecificaFieldsAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        // Only fuse if at least one source has SolicitudEspecifica collection
        var hasEspecificas = (xml?.SolicitudEspecificas?.Count > 0) ||
                             (pdf?.SolicitudEspecificas?.Count > 0) ||
                             (docx?.SolicitudEspecificas?.Count > 0);

        if (!hasEspecificas)
        {
            _logger.LogDebug("No SolicitudEspecifica collections found, skipping primary solicitud especifica fusion");
            return;
        }

        _logger.LogDebug("Fusing primary SolicitudEspecifica fields (first in collection)");

        // Ensure fusedExpediente has collection initialized
        fused.SolicitudEspecificas ??= new List<SolicitudEspecifica>();

        // Ensure at least one SolicitudEspecifica exists to hold fused values
        if (fused.SolicitudEspecificas.Count == 0)
        {
            fused.SolicitudEspecificas.Add(new SolicitudEspecifica());
        }

        // Fuse individual fields
        await FuseSolicitudEspecificaIdAsync(xml, pdf, docx, reliabilities, fused, results, conflicts, cancellationToken);
        await FuseSolicitudMeasureAsync(xml, pdf, docx, reliabilities, fused, results, conflicts, cancellationToken);
        await FuseSolicitudInstruccionesAsync(xml, pdf, docx, reliabilities, fused, results, conflicts, cancellationToken);
    }

    /// <summary>
    /// Fuses SolicitudEspecificaId from the primary SolicitudEspecifica.
    /// </summary>
    private async Task FuseSolicitudEspecificaIdAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate
        if (xml?.SolicitudEspecificas?.Count > 0 && xml.SolicitudEspecificas[0].SolicitudEspecificaId > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.SolicitudEspecificas[0].SolicitudEspecificaId.ToString(),
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true
            });
        }

        // PDF candidate
        if (pdf?.SolicitudEspecificas?.Count > 0 && pdf.SolicitudEspecificas[0].SolicitudEspecificaId > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.SolicitudEspecificas[0].SolicitudEspecificaId.ToString(),
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        // DOCX candidate
        if (docx?.SolicitudEspecificas?.Count > 0 && docx.SolicitudEspecificas[0].SolicitudEspecificaId > 0)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.SolicitudEspecificas[0].SolicitudEspecificaId.ToString(),
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        // Fuse
        var result = await FuseFieldAsync("SolicitudEspecificaId", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            if (int.TryParse(result.Value.Value, out var fusedId))
            {
                fused.SolicitudEspecificas[0].SolicitudEspecificaId = fusedId;
                results["SolicitudEspecificaId"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("SolicitudEspecificaId");
                }
            }
        }
    }

    /// <summary>
    /// Fuses Measure (MeasureKind enum) from the primary SolicitudEspecifica.
    /// </summary>
    private async Task FuseSolicitudMeasureAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate
        if (xml?.SolicitudEspecificas?.Count > 0 && xml.SolicitudEspecificas[0].Measure != MeasureKind.Unknown)
        {
            candidates.Add(new FieldCandidate
            {
                Value = xml.SolicitudEspecificas[0].Measure.Name,
                Source = SourceType.XML_HandFilled,
                SourceReliability = reliabilities[SourceType.XML_HandFilled],
                MatchesPattern = true
            });
        }

        // PDF candidate
        if (pdf?.SolicitudEspecificas?.Count > 0 && pdf.SolicitudEspecificas[0].Measure != MeasureKind.Unknown)
        {
            candidates.Add(new FieldCandidate
            {
                Value = pdf.SolicitudEspecificas[0].Measure.Name,
                Source = SourceType.PDF_OCR_CNBV,
                SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV],
                MatchesPattern = true
            });
        }

        // DOCX candidate
        if (docx?.SolicitudEspecificas?.Count > 0 && docx.SolicitudEspecificas[0].Measure != MeasureKind.Unknown)
        {
            candidates.Add(new FieldCandidate
            {
                Value = docx.SolicitudEspecificas[0].Measure.Name,
                Source = SourceType.DOCX_OCR_Authority,
                SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority],
                MatchesPattern = true
            });
        }

        // Fuse
        var result = await FuseFieldAsync("Measure", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null && result.Value.Value != null)
        {
            try
            {
                var fusedMeasure = MeasureKind.FromName(result.Value.Value);
                fused.SolicitudEspecificas[0].Measure = fusedMeasure;
                results["Measure"] = result.Value;
                if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
                {
                    conflicts.Add("Measure");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse fused Measure value: {Value}", result.Value.Value);
                fused.SolicitudEspecificas[0].Measure = MeasureKind.Unknown;
            }
        }
    }

    /// <summary>
    /// Fuses InstruccionesCuentasPorConocer from the primary SolicitudEspecifica.
    /// </summary>
    private async Task FuseSolicitudInstruccionesAsync(
        Expediente? xml, Expediente? pdf, Expediente? docx,
        Dictionary<SourceType, double> reliabilities,
        Expediente fused, Dictionary<string, FieldFusionResult> results,
        List<string> conflicts, CancellationToken cancellationToken)
    {
        var candidates = new List<FieldCandidate>();

        // XML candidate
        if (xml?.SolicitudEspecificas?.Count > 0)
        {
            var value = xml.SolicitudEspecificas[0].InstruccionesCuentasPorConocer;
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(new FieldCandidate
                {
                    Value = value,
                    Source = SourceType.XML_HandFilled,
                    SourceReliability = reliabilities[SourceType.XML_HandFilled]
                });
            }
        }

        // PDF candidate
        if (pdf?.SolicitudEspecificas?.Count > 0)
        {
            var value = pdf.SolicitudEspecificas[0].InstruccionesCuentasPorConocer;
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(new FieldCandidate
                {
                    Value = value,
                    Source = SourceType.PDF_OCR_CNBV,
                    SourceReliability = reliabilities[SourceType.PDF_OCR_CNBV]
                });
            }
        }

        // DOCX candidate
        if (docx?.SolicitudEspecificas?.Count > 0)
        {
            var value = docx.SolicitudEspecificas[0].InstruccionesCuentasPorConocer;
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(new FieldCandidate
                {
                    Value = value,
                    Source = SourceType.DOCX_OCR_Authority,
                    SourceReliability = reliabilities[SourceType.DOCX_OCR_Authority]
                });
            }
        }

        // Fuse
        var result = await FuseFieldAsync("InstruccionesCuentasPorConocer", candidates, cancellationToken);
        if (result.IsSuccess && result.Value != null)
        {
            fused.SolicitudEspecificas[0].InstruccionesCuentasPorConocer = result.Value.Value ?? string.Empty;
            results["InstruccionesCuentasPorConocer"] = result.Value;
            if (result.Value.Decision == FusionDecision.WeightedVoting || result.Value.Decision == FusionDecision.Conflict)
            {
                conflicts.Add("InstruccionesCuentasPorConocer");
            }
        }
    }

    /// <summary>
    /// Calculates a future date by adding business days (skipping weekends).
    /// </summary>
    /// <param name="startDate">The starting date.</param>
    /// <param name="businessDays">The number of business days to add.</param>
    /// <returns>The calculated end date.</returns>
    /// <remarks>
    /// Simplification: Only skips weekends (Saturday/Sunday).
    /// Does not account for Mexican federal holidays.
    /// For production, integrate with holiday calendar service.
    /// </remarks>
    private DateTime CalculateBusinessDays(DateTime startDate, int businessDays)
    {
        var currentDate = startDate;
        var daysAdded = 0;

        while (daysAdded < businessDays)
        {
            currentDate = currentDate.AddDays(1);

            // Skip weekends
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                daysAdded++;
            }
        }

        return currentDate;
    }

    #endregion
}

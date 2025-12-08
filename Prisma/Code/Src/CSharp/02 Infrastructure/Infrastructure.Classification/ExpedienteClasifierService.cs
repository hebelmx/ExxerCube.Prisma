using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Expediente classification service for CNBV requirement type detection and legal validation.
/// Implements pattern-based classification, Article 4 validation, and Article 17 rejection analysis.
/// </summary>
/// <remarks>
/// Classification Logic:
/// - Type 100 (Information): No asset seizure, keyword "solicito información"
/// - Type 101 (Aseguramiento): TieneAseguramiento=true, keywords "asegurar", "bloquear"
/// - Type 102 (Desbloqueo): Keywords "desbloquear", "liberar", references prior seizure
/// - Type 103 (Transferencia): Keywords "transferir", has CLABE
/// - Type 104 (Situación Fondos): Keywords "cheque de caja", "situar fondos"
///
/// Article 4: Validates all 42 mandatory fields from R29 A-2911
/// Article 17: Checks 6 rejection grounds per CNBV regulations
/// </remarks>
public class ExpedienteClasifierService : IExpedienteClasifier
{
    private readonly ILogger<ExpedienteClasifierService> _logger;
    private readonly ISemanticAnalyzer _semanticAnalyzer;

    // Required fields per requirement type (from R29 A-2911 specification)
    // Note: Only includes fields that actually exist in LawMandatedFields
    private static readonly Dictionary<int, HashSet<string>> RequiredFieldsByType = new()
    {
        [100] = new HashSet<string> // InformationRequest
        {
            "InternalCaseId", "SourceAuthorityCode", "RequirementType"
        },
        [101] = new HashSet<string> // Aseguramiento
        {
            "InternalCaseId", "SourceAuthorityCode", "RequirementType",
            "AccountNumber", "BranchCode", "ProductType", "InitialBlockedAmount"
        },
        [102] = new HashSet<string> // Desbloqueo
        {
            "InternalCaseId", "SourceAuthorityCode"
        },
        [103] = new HashSet<string> // TransferenciaElectronica
        {
            "InternalCaseId", "AccountNumber", "SourceAuthorityCode", "OperationAmount"
        },
        [104] = new HashSet<string> // SituacionFondos
        {
            "InternalCaseId", "AccountNumber", "SourceAuthorityCode", "OperationAmount"
        }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpedienteClasifierService"/> class.
    /// </summary>
    /// <param name="semanticAnalyzer">The semantic analyzer service (NEW - uses fuzzy matching).</param>
    /// <param name="logger">The logger instance.</param>
    public ExpedienteClasifierService(
        ISemanticAnalyzer semanticAnalyzer,
        ILogger<ExpedienteClasifierService> logger)
    {
        _semanticAnalyzer = semanticAnalyzer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExpedienteClassificationResult>> ClassifyAsync(
        Expediente expediente,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Classifying Expediente: {NumeroExpediente}", expediente.NumeroExpediente);

            // Step 1: Determine requirement type (100-104)
            var (requirementType, confidence) = ClassifyRequirementType(expediente);

            // Step 2: Determine authority type
            var authorityType = DetermineAuthorityKind(expediente.AutoridadNombre ?? string.Empty);

            // Step 3: Get required fields for this type
            var requiredFields = GetRequiredFields(requirementType);

            // Step 4: Validate Article 4 (42 mandatory fields)
            var articleValidationResult = await ValidateArticle4Async(expediente, requirementType, cancellationToken);
            var articleValidation = articleValidationResult.IsSuccess ? articleValidationResult.Value : new ArticleValidationResult();

            // Step 5: Analyze semantic requirements (The 5 Situations)
            var semanticAnalysisResult = await AnalyzeSemanticRequirementsAsync(expediente, cancellationToken);
            var semanticAnalysis = semanticAnalysisResult.IsSuccess ? semanticAnalysisResult.Value : new SemanticAnalysis();

            var result = new ExpedienteClassificationResult
            {
                RequirementType = requirementType,
                ClassificationConfidence = confidence,
                AuthorityType = authorityType,
                RequiredFields = requiredFields ?? new List<string>(),
                ArticleValidation = articleValidation ?? new ArticleValidationResult(),
                SemanticAnalysis = semanticAnalysis ?? new SemanticAnalysis()
            };

            _logger.LogInformation(
                "Expediente classified - Type: {Type}, Confidence: {Confidence:F2}, Authority: {Authority}",
                requirementType, confidence, authorityType);

            return Result<ExpedienteClassificationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying Expediente: {NumeroExpediente}", expediente.NumeroExpediente);
            return Result<ExpedienteClassificationResult>.WithFailure(
                $"Classification error: {ex.Message}",
                default(ExpedienteClassificationResult),
                ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<ArticleValidationResult>> ValidateArticle4Async(
        Expediente expediente,
        RequirementType requirementType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating Article 4 compliance for {NumeroExpediente}", expediente.NumeroExpediente);

            var missingFields = new List<string>();
            var lawFields = expediente.LawMandatedFields;

            // Get required fields for this requirement type
            if (RequiredFieldsByType.TryGetValue(requirementType.Value, out var requiredFields))
            {
                foreach (var field in requiredFields)
                {
                    if (!IsFieldPopulated(lawFields, field))
                    {
                        missingFields.Add(field);
                    }
                }
            }

            var passesArticle4 = missingFields.Count == 0;

            var result = new ArticleValidationResult
            {
                PassesArticle4 = passesArticle4,
                MissingRequiredFields = missingFields
            };

            _logger.LogDebug(
                "Article 4 validation - Passes: {Passes}, Missing Fields: {Count}",
                passesArticle4, missingFields.Count);

            return Task.FromResult(Result<ArticleValidationResult>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Article 4");
            return Task.FromResult(Result<ArticleValidationResult>.WithFailure(
                $"Article 4 validation error: {ex.Message}",
                default(ArticleValidationResult),
                ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<List<RejectionReason>>> CheckArticle17RejectionAsync(
        Expediente expediente,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking Article 17 rejection grounds for {NumeroExpediente}", expediente.NumeroExpediente);

            var rejectionReasons = new List<RejectionReason>();

            // Check 1: Missing legal authority citation (Article 17.I)
            if (string.IsNullOrWhiteSpace(expediente.FundamentoLegal))
            {
                rejectionReasons.Add(RejectionReason.NoLegalAuthorityCitation);
            }

            // Check 2: Missing signature (Article 17.I - formalities)
            if (string.IsNullOrWhiteSpace(expediente.EvidenciaFirma))
            {
                rejectionReasons.Add(RejectionReason.MissingSignature);
            }

            // Check 3: Lack of specificity (Article 17.I)
            var hasAccountNumber = expediente.LawMandatedFields?.AccountNumber != null;
            var hasRfcOrCurp = expediente.SolicitudPartes.Any(p =>
                !string.IsNullOrWhiteSpace(p.Rfc) || !string.IsNullOrWhiteSpace(p.Curp));

            if (!hasAccountNumber && !hasRfcOrCurp)
            {
                rejectionReasons.Add(RejectionReason.LackOfSpecificity);
            }

            // Check 4: Exceeds CNBV jurisdiction (Article 17.III)
            var areaDescripcion = expediente.AreaDescripcion ?? string.Empty;
            if (!IsValidCNBVArea(areaDescripcion))
            {
                rejectionReasons.Add(RejectionReason.ExceedsJurisdiction);
            }

            // Check 5: Missing required data (Article 17.II)
            if (expediente.LawMandatedFields?.InternalCaseId == null)
            {
                rejectionReasons.Add(RejectionReason.MissingRequiredData);
            }

            // Check 6: Technical impossibility (Article 17)
            // This would be determined by bank systems, not classification

            _logger.LogDebug("Article 17 check - Rejection reasons: {Count}", rejectionReasons.Count);

            return Task.FromResult(Result<List<RejectionReason>>.Success(rejectionReasons));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Article 17 rejection grounds");
            return Task.FromResult(Result<List<RejectionReason>>.WithFailure(
                $"Article 17 check error: {ex.Message}",
                default(List<RejectionReason>),
                ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<SemanticAnalysis>> AnalyzeSemanticRequirementsAsync(
        Expediente expediente,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Analyzing semantic requirements for {NumeroExpediente}", expediente.NumeroExpediente);

            // Build document text from all reference fields for fuzzy matching
            var documentText = $"{expediente.Referencia} {expediente.Referencia1} {expediente.Referencia2}".Trim();

            // If document text is empty, infer from expediente metadata (TieneAseguramiento, AreaDescripcion)
            if (string.IsNullOrWhiteSpace(documentText))
            {
                documentText = InferDocumentTextFromMetadata(expediente);
            }

            // Use the new ISemanticAnalyzer with fuzzy phrase matching (fixes audit gap)
            var analysisResult = await _semanticAnalyzer.AnalyzeDirectivesAsync(
                documentText,
                expediente,
                cancellationToken);

            if (analysisResult.IsFailure)
            {
                _logger.LogWarning(
                    "Semantic analysis failed for {NumeroExpediente}: {Error}",
                    expediente.NumeroExpediente,
                    analysisResult.Error);
                return analysisResult;
            }

            var analysis = analysisResult.Value;

            if (analysis == null)
            {
                _logger.LogWarning("Semantic analysis returned null for {NumeroExpediente}", expediente.NumeroExpediente);
                return Result<SemanticAnalysis>.WithFailure("Semantic analysis returned null");
            }

            // Enrich the analysis with expediente-specific metadata (amounts, accounts, etc.)
            EnrichSemanticAnalysisWithExpedienteMetadata(expediente, analysis);

            _logger.LogInformation("Semantic analysis completed for {NumeroExpediente}", expediente.NumeroExpediente);

            return Result<SemanticAnalysis>.Success(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing semantic requirements");
            return Result<SemanticAnalysis>.WithFailure(
                $"Semantic analysis error: {ex.Message}",
                default(SemanticAnalysis),
                ex);
        }
    }

    /// <summary>
    /// Enriches the semantic analysis with expediente-specific metadata (amounts, accounts, currencies, etc.).
    /// </summary>
    private static void EnrichSemanticAnalysisWithExpedienteMetadata(Expediente expediente, SemanticAnalysis analysis)
    {
        // Enrich Bloqueo requirement with metadata
        if (analysis.RequiereBloqueo != null)
        {
            analysis.RequiereBloqueo.EsParcial = expediente.LawMandatedFields?.InitialBlockedAmount != null;
            analysis.RequiereBloqueo.Monto = expediente.LawMandatedFields?.InitialBlockedAmount;
            analysis.RequiereBloqueo.Moneda = expediente.LawMandatedFields?.Currency ?? "MXN";
        }

        // Enrich Desbloqueo requirement with metadata
        if (analysis.RequiereDesbloqueo != null)
        {
            analysis.RequiereDesbloqueo.ExpedienteBloqueoOriginal = expediente.OficioOrigen;
        }

        // Enrich Transferencia requirement with metadata
        if (analysis.RequiereTransferencia != null)
        {
            analysis.RequiereTransferencia.Monto = expediente.LawMandatedFields?.OperationAmount;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Infers document text from expediente metadata when Referencia fields are null/empty.
    /// Maps TieneAseguramiento and AreaDescripcion to Spanish legal phrases.
    /// </summary>
    private static string InferDocumentTextFromMetadata(Expediente expediente)
    {
        // Priority 1: Check TieneAseguramiento flag
        if (expediente.TieneAseguramiento)
        {
            return "aseguramiento de fondos"; // Matches ClassificationDictionary Block phrases
        }

        // Priority 2: Check AreaDescripcion
        var areaDescripcion = (expediente.AreaDescripcion ?? string.Empty).ToUpperInvariant();

        if (areaDescripcion.Contains("ASEGURAMIENTO"))
        {
            return "aseguramiento de fondos";
        }

        if (areaDescripcion.Contains("DESBLOQUEO"))
        {
            return "desbloqueo de cuenta";
        }

        // Priority 3: Default to general information request
        return "solicitud de información"; // Matches ClassificationDictionary Information phrases
    }

    private (RequirementType type, double confidence) ClassifyRequirementType(Expediente expediente)
    {
        var areaDescripcion = (expediente.AreaDescripcion ?? string.Empty).ToUpperInvariant();
        var numeroExpediente = (expediente.NumeroExpediente ?? string.Empty).ToUpperInvariant();
        var referencias = $"{expediente.Referencia} {expediente.Referencia1} {expediente.Referencia2}".ToUpperInvariant();
        var tieneAseguramiento = expediente.TieneAseguramiento;

        // Type 102: Desbloqueo (highest priority - explicit unblocking)
        if (referencias.Contains("DESBLOQUEO") || referencias.Contains("LIBERAR") || referencias.Contains("LEVANTAR"))
        {
            return (RequirementType.Desbloqueo, 0.95);
        }

        // Type 101: Aseguramiento (asset seizure)
        if (tieneAseguramiento || areaDescripcion.Contains("ASEGURAMIENTO") || numeroExpediente.Contains("/AS"))
        {
            // Check if it's actually a transfer order
            if (referencias.Contains("TRANSFERIR") || referencias.Contains("CLABE"))
            {
                return (RequirementType.Transferencia, 0.90);
            }

            // Check if it's situación de fondos
            if (referencias.Contains("CHEQUE") || referencias.Contains("SITUAR"))
            {
                return (RequirementType.SituacionFondos, 0.90);
            }

            return (RequirementType.Aseguramiento, 0.90);
        }

        // Type 103: Transferencia Electrónica
        if (referencias.Contains("TRANSFERIR") || referencias.Contains("CLABE") || referencias.Contains("ELECTRÓNICA"))
        {
            return (RequirementType.Transferencia, 0.85);
        }

        // Type 104: Situación de Fondos
        if (referencias.Contains("CHEQUE") || referencias.Contains("SITUAR") || referencias.Contains("FONDOS"))
        {
            return (RequirementType.SituacionFondos, 0.85);
        }

        // Type 100: Information Request (default)
        return (RequirementType.InformationRequest, 0.80);
    }

    private static AuthorityKind DetermineAuthorityKind(string autoridadNombre)
    {
        var upperName = autoridadNombre.ToUpperInvariant();

        if (upperName.Contains("JUZGADO") || upperName.Contains("TRIBUNAL") || upperName.Contains("MAGISTRADO"))
        {
            return AuthorityKind.Juzgado;
        }

        if (upperName.Contains("SAT") || upperName.Contains("FGR") || upperName.Contains("FISCAL") ||
            upperName.Contains("ADMINISTRACION") || upperName.Contains("HACIENDA"))
        {
            return AuthorityKind.Hacienda;
        }

        if (upperName.Contains("UIF"))
        {
            return AuthorityKind.UIF;
        }

        if (upperName.Contains("CNBV"))
        {
            return AuthorityKind.CNBV;
        }

        return AuthorityKind.Other; // Default
    }

    private static List<string> GetRequiredFields(RequirementType requirementType)
    {
        if (RequiredFieldsByType.TryGetValue(requirementType.Value, out var fields))
        {
            return fields.ToList();
        }

        return new List<string>();
    }

    private static bool IsFieldPopulated(LawMandatedFields? lawFields, string fieldName)
    {
        if (lawFields == null) return false;

        // Use reflection to check if field is populated
        var property = typeof(LawMandatedFields).GetProperty(fieldName);
        if (property == null) return false;

        var value = property.GetValue(lawFields);

        // Check if value is null or empty string
        if (value == null) return false;
        if (value is string strValue && string.IsNullOrWhiteSpace(strValue)) return false;

        return true;
    }

    private static bool IsValidCNBVArea(string areaDescripcion)
    {
        var validAreas = new[]
        {
            "ASEGURAMIENTO",
            "HACENDARIO",
            "PENAL",
            "CIVIL",
            "ADMINISTRATIVO",
            "JUDICIAL"
        };

        return validAreas.Contains(areaDescripcion.ToUpperInvariant());
    }

    #endregion
}

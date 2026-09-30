using System;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Production semantic analyzer using fuzzy phrase matching and classification dictionaries.
/// Fixes the audit gap by returning rich SemanticAnalysis domain objects instead of primitive List&lt;ComplianceAction&gt;.
/// </summary>
/// <remarks>
/// Architecture:
/// - Uses ITextComparer.FindBestMatch for fuzzy phrase matching (tolerates typos and variations)
/// - Uses ClassificationDictionary for phrase-to-action mappings (100+ Spanish legal phrases)
/// - Populates SemanticAnalysis with confidence scores from fuzzy matching
/// - Supports multiple directives in single document
///
/// Audit Gap Fixes:
/// - FIXED: Returns SemanticAnalysis (rich domain objects) instead of List&lt;ComplianceAction&gt; (primitives)
/// - FIXED: Uses fuzzy matching instead of naive keyword Contains() checks
/// - FIXED: Tolerates phrase variations (e.g., "aseguramiento de fondos" vs "aseguramiento de los fondos")
/// - FIXED: Provides confidence scores (0.0-1.0) instead of arbitrary integers
///
/// Phase 1 Implementation:
/// - Hardcoded classification dictionary (extensible for Phase 3 AI approach)
/// - Fuzzy matching with 85% similarity threshold
/// - Precedence-based classification (Unblock > Block/Transfer/Document > Information)
/// </remarks>
public class SemanticAnalyzerService : ISemanticAnalyzer
{
    private readonly ITextComparer _textComparer;
    private readonly ILogger<SemanticAnalyzerService> _logger;
    private readonly IOllamaClient? _ollamaClient;
    private readonly OllamaOptions _ollamaOptions;

    /// <summary>
    /// Grounded prompt used to ask the LLM what information the authority requests.
    /// The document text is appended before this question at call time.
    /// </summary>
    internal const string InformacionLlmQuestion =
        "\n\n¿Qué información solicita la autoridad en este oficio? " +
        "Responde solo con la información solicitada, en una o dos oraciones breves.";

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticAnalyzerService"/> class
    /// with an optional <see cref="IOllamaClient"/> for LLM-based enrichment of interpreted fields.
    /// </summary>
    /// <param name="textComparer">Text comparer for fuzzy phrase matching.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ollamaClient">
    /// Optional Ollama LLM client.  When <see langword="null"/> or when
    /// <see cref="OllamaOptions.Enabled"/> is <see langword="false"/> the analyzer runs in
    /// structured-only mode (E1 result) and never calls the LLM.
    /// </param>
    /// <param name="ollamaOptions">
    /// Ollama configuration (Enabled, Endpoint, Model, Timeout).
    /// Defaults to a disabled instance when not supplied.
    /// </param>
    public SemanticAnalyzerService(
        ITextComparer textComparer,
        ILogger<SemanticAnalyzerService> logger,
        IOllamaClient? ollamaClient = null,
        IOptions<OllamaOptions>? ollamaOptions = null)
    {
        _textComparer = textComparer;
        _logger = logger;
        _ollamaClient = ollamaClient;
        _ollamaOptions = ollamaOptions?.Value ?? new OllamaOptions();
    }

    /// <inheritdoc />
    public async Task<Result<SemanticAnalysis>> AnalyzeDirectivesAsync(
        string documentText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<SemanticAnalysis>.WithFailure("Operation was cancelled.");
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Document text cannot be null or empty for semantic analysis");
            return Result<SemanticAnalysis>.WithFailure("Document text cannot be null or empty.");
        }

        try
        {
            _logger.LogDebug(
                "Analyzing legal directives from document text (length: {Length}, expediente: {Expediente})",
                documentText.Length,
                expediente?.NumeroExpediente ?? "N/A");

            // Initialize semantic analysis object
            var semanticAnalysis = new SemanticAnalysis();

            // Scan document for all directive types using fuzzy phrase matching
            // Order matters: Unblock > Block/Transfer/Document > Information (precedence)

            // 1. Check for Unblock directives (HIGHEST PRIORITY)
            //    "desbloquear el aseguramiento" should be Unblock, not Block
            DetectUnblockRequirement(documentText, semanticAnalysis, expediente?.NumeroExpediente);

            // 2. Check for Block directives
            DetectBlockRequirement(documentText, semanticAnalysis);

            // 3. Check for Transfer directives
            DetectTransferRequirement(documentText, semanticAnalysis);

            // 4. Check for Document directives
            DetectDocumentRequirement(documentText, semanticAnalysis);

            // 5. Check for Information directives (LOWEST PRIORITY - catch-all)
            DetectInformationRequirement(documentText, semanticAnalysis);

            // E2: LLM enrichment — only for Información interpreted free-text,
            // only when a client is wired, the feature is enabled, the requirement
            // was detected, and E1 left InformacionSolicitada empty/weak.
            if (_ollamaClient != null
                && _ollamaOptions.Enabled
                && semanticAnalysis.RequiereInformacionGeneral != null
                && string.IsNullOrWhiteSpace(semanticAnalysis.RequiereInformacionGeneral.InformacionSolicitada))
            {
                await EnrichInformacionWithLlmAsync(
                    documentText,
                    semanticAnalysis.RequiereInformacionGeneral,
                    cancellationToken)
                    .ConfigureAwait(false);
            }

            // Log results
            var detectedRequirements = CountDetectedRequirements(semanticAnalysis);
            _logger.LogDebug(
                "Semantic analysis complete: {RequirementCount} requirement(s) detected " +
                "(Block: {Block}, Unblock: {Unblock}, Document: {Document}, Transfer: {Transfer}, Information: {Information})",
                detectedRequirements,
                semanticAnalysis.RequiereBloqueo != null ? 1 : 0,
                semanticAnalysis.RequiereDesbloqueo != null ? 1 : 0,
                semanticAnalysis.RequiereDocumentacion != null ? 1 : 0,
                semanticAnalysis.RequiereTransferencia != null ? 1 : 0,
                semanticAnalysis.RequiereInformacionGeneral != null ? 1 : 0);

            return Result<SemanticAnalysis>.Success(semanticAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing legal directives");
            return Result<SemanticAnalysis>.WithFailure(
                $"Error analyzing legal directives: {ex.Message}",
                default(SemanticAnalysis),
                ex);
        }
    }

    /// <summary>
    /// Calls the LLM with a grounded prompt to fill <see cref="InformacionGeneralRequirement.InformacionSolicitada"/>
    /// when E1 left it empty. Fails-open: any error is logged and the structured result is kept unchanged.
    /// The LLM NEVER overwrites a confidently-extracted structured value; this method is only called
    /// when the field is empty after structured extraction.
    /// </summary>
    private async Task EnrichInformacionWithLlmAsync(
        string documentText,
        InformacionGeneralRequirement requirement,
        CancellationToken cancellationToken)
    {
        try
        {
            // Truncate very long documents to stay within reasonable token budgets
            const int MaxDocChars = 4000;
            var docExcerpt = documentText.Length > MaxDocChars
                ? documentText[..MaxDocChars]
                : documentText;

            var prompt = docExcerpt + InformacionLlmQuestion;

            _logger.LogDebug("Calling Ollama LLM to enrich InformacionSolicitada ({DocChars} chars).", docExcerpt.Length);

            var llmResult = await _ollamaClient!.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false);

            if (llmResult.IsSuccess && !string.IsNullOrWhiteSpace(llmResult.Value))
            {
                requirement.InformacionSolicitada = llmResult.Value!.Trim();
                _logger.LogInformation(
                    "LLM enriched InformacionSolicitada ({Length} chars).",
                    requirement.InformacionSolicitada.Length);
            }
            else
            {
                _logger.LogWarning(
                    "LLM enrichment failed or returned empty (fail-open): {Error}",
                    llmResult.IsFailure ? llmResult.Error : "empty response");
            }
        }
        catch (Exception ex)
        {
            // Fail-open: keep E1 result, do not rethrow.
            _logger.LogWarning(ex, "LLM enrichment threw unexpectedly (fail-open); E1 result kept.");
        }
    }

    /// <summary>
    /// Detects Block (asset freeze) requirements using fuzzy phrase matching.
    /// </summary>
    private void DetectBlockRequirement(string documentText, SemanticAnalysis analysis)
    {
        var (matched, confidence, matchedPhrase) = FindBestPhraseMatch(
            documentText,
            ClassificationDictionary.BlockPhrases);

        if (matched)
        {
            _logger.LogDebug(
                "Block requirement detected: phrase='{Phrase}', confidence={Confidence:F2}",
                matchedPhrase,
                confidence);

            var bloqueo = new BloqueoRequirement
            {
                EsRequerido = true,
                Confidence = confidence
            };
            RequirementDetailExtractor.PopulateBloqueo(documentText, bloqueo);
            analysis.RequiereBloqueo = bloqueo;
        }
    }

    /// <summary>
    /// Detects Unblock (asset unfreeze) requirements using fuzzy phrase matching.
    /// </summary>
    private void DetectUnblockRequirement(
        string documentText,
        SemanticAnalysis analysis,
        string? currentExpedienteId)
    {
        var (matched, confidence, matchedPhrase) = FindBestPhraseMatch(
            documentText,
            ClassificationDictionary.UnblockPhrases);

        if (matched)
        {
            _logger.LogDebug(
                "Unblock requirement detected: phrase='{Phrase}', confidence={Confidence:F2}",
                matchedPhrase,
                confidence);

            var desbloqueo = new DesbloqueoRequirement
            {
                EsRequerido = true,
                Confidence = confidence
            };
            RequirementDetailExtractor.PopulateDesbloqueo(
                documentText,
                currentExpedienteId,
                desbloqueo);
            analysis.RequiereDesbloqueo = desbloqueo;
        }
    }

    /// <summary>
    /// Detects Document submission requirements using fuzzy phrase matching.
    /// </summary>
    private void DetectDocumentRequirement(string documentText, SemanticAnalysis analysis)
    {
        var (matched, confidence, matchedPhrase) = FindBestPhraseMatch(
            documentText,
            ClassificationDictionary.DocumentPhrases);

        if (matched)
        {
            _logger.LogDebug(
                "Document requirement detected: phrase='{Phrase}', confidence={Confidence:F2}",
                matchedPhrase,
                confidence);

            var documentacion = new DocumentacionRequirement
            {
                EsRequerido = true,
                Confidence = confidence
            };
            RequirementDetailExtractor.PopulateDocumentacion(documentText, documentacion);
            analysis.RequiereDocumentacion = documentacion;
        }
    }

    /// <summary>
    /// Detects Transfer requirements using fuzzy phrase matching.
    /// </summary>
    private void DetectTransferRequirement(string documentText, SemanticAnalysis analysis)
    {
        var (matched, confidence, matchedPhrase) = FindBestPhraseMatch(
            documentText,
            ClassificationDictionary.TransferPhrases);

        if (matched)
        {
            _logger.LogDebug(
                "Transfer requirement detected: phrase='{Phrase}', confidence={Confidence:F2}",
                matchedPhrase,
                confidence);

            var transferencia = new TransferenciaRequirement
            {
                EsRequerido = true,
                Confidence = confidence
            };
            RequirementDetailExtractor.PopulateTransferencia(documentText, transferencia);
            analysis.RequiereTransferencia = transferencia;
        }
    }

    /// <summary>
    /// Detects Information request requirements using fuzzy phrase matching.
    /// </summary>
    private void DetectInformationRequirement(string documentText, SemanticAnalysis analysis)
    {
        var (matched, confidence, matchedPhrase) = FindBestPhraseMatch(
            documentText,
            ClassificationDictionary.InformationPhrases);

        if (matched)
        {
            _logger.LogDebug(
                "Information requirement detected: phrase='{Phrase}', confidence={Confidence:F2}",
                matchedPhrase,
                confidence);

            var informacion = new InformacionGeneralRequirement
            {
                EsRequerido = true,
                Confidence = confidence
            };
            RequirementDetailExtractor.PopulateInformacion(documentText, informacion);
            analysis.RequiereInformacionGeneral = informacion;
        }
    }

    /// <summary>
    /// Finds the best matching phrase in a dictionary using fuzzy matching.
    /// </summary>
    /// <param name="documentText">The document text to search.</param>
    /// <param name="phraseDictionary">Dictionary of phrases to match against.</param>
    /// <returns>
    /// Tuple of (matched: bool, confidence: double, matchedPhrase: string):
    /// - matched: true if any phrase matched above threshold
    /// - confidence: similarity score (0.0-1.0) of best match
    /// - matchedPhrase: the dictionary phrase that matched (or empty if no match)
    /// </returns>
    private (bool matched, double confidence, string matchedPhrase) FindBestPhraseMatch(
        string documentText,
        Dictionary<string, ComplianceActionKind> phraseDictionary)
    {
        double bestConfidence = 0.0;
        string bestPhrase = string.Empty;

        foreach (var phrase in phraseDictionary.Keys)
        {
            var matchResult = _textComparer.FindBestMatch(
                phrase,
                documentText,
                ClassificationDictionary.DefaultThreshold);

            if (matchResult != null && matchResult.Similarity > bestConfidence)
            {
                bestConfidence = matchResult.Similarity;
                bestPhrase = phrase;

                _logger.LogTrace(
                    "Fuzzy match found: phrase='{Phrase}', matched='{MatchedText}', similarity={Similarity:F2}",
                    phrase,
                    matchResult.MatchedText,
                    matchResult.Similarity);
            }
        }

        bool matched = bestConfidence >= ClassificationDictionary.DefaultThreshold;

        if (!matched)
        {
            _logger.LogTrace(
                "No fuzzy match found above threshold ({Threshold:F2}) in dictionary with {PhraseCount} phrases",
                ClassificationDictionary.DefaultThreshold,
                phraseDictionary.Count);
        }

        return (matched, bestConfidence, bestPhrase);
    }

    /// <summary>
    /// Counts how many requirements were detected in the semantic analysis.
    /// </summary>
    private static int CountDetectedRequirements(SemanticAnalysis analysis)
    {
        int count = 0;
        if (analysis.RequiereBloqueo != null) count++;
        if (analysis.RequiereDesbloqueo != null) count++;
        if (analysis.RequiereDocumentacion != null) count++;
        if (analysis.RequiereTransferencia != null) count++;
        if (analysis.RequiereInformacionGeneral != null) count++;
        return count;
    }
}

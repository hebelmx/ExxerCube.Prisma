using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Service for classifying legal directives from document text and mapping clauses to compliance actions.
/// </summary>
/// <remarks>
/// DEPRECATED: This implementation uses naive keyword matching which has been superseded by fuzzy phrase matching.
/// Use SemanticAnalyzerAdapter (wrapping SemanticAnalyzerService) instead for improved accuracy.
/// This class is maintained for backward compatibility during migration but will be removed in a future release.
/// Migration path: ILegalDirectiveClassifier now resolves to SemanticAnalyzerAdapter in DI container.
/// </remarks>
[Obsolete("Use SemanticAnalyzerAdapter with fuzzy phrase matching instead. This naive implementation will be removed after migration verification period.")]
public class LegalDirectiveClassifierService : ILegalDirectiveClassifier
{
    private readonly ILogger<LegalDirectiveClassifierService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegalDirectiveClassifierService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public LegalDirectiveClassifierService(ILogger<LegalDirectiveClassifierService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<List<ComplianceAction>>> ClassifyDirectivesAsync(
        string documentText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<List<ComplianceAction>>.WithFailure("Operation was cancelled."));
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Document text cannot be null or empty for classification");
            return Task.FromResult(Result<List<ComplianceAction>>.WithFailure("Document text cannot be null or empty."));
        }

        try
        {
            _logger.LogDebug("Classifying legal directives from document text (length: {Length})", documentText.Length);

            var actions = new List<ComplianceAction>();
            var upperText = documentText.ToUpperInvariant();

            // Detect document relation type (Recordatorio, Alcance, Precisión, or NewRequirement)
            var documentRelationType = DetectDocumentRelationType(upperText);

            // Detect block directives
            if (ContainsBlockDirective(upperText))
            {
                var blockAction = new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Block,
                    ExpedienteOrigen = expediente?.NumeroExpediente,
                    OficioOrigen = expediente?.NumeroOficio,
                    Confidence = CalculateConfidence(upperText, BlockKeywords),
                    DocumentRelationType = documentRelationType
                };
                ExtractActionDetails(upperText, blockAction);
                ApplyEdgeCaseValidation(upperText, blockAction);
                actions.Add(blockAction);
            }

            // Detect unblock directives
            if (ContainsUnblockDirective(upperText))
            {
                var unblockAction = new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Unblock,
                    ExpedienteOrigen = expediente?.NumeroExpediente,
                    OficioOrigen = expediente?.NumeroOficio,
                    Confidence = CalculateConfidence(upperText, UnblockKeywords),
                    DocumentRelationType = documentRelationType
                };
                ExtractActionDetails(upperText, unblockAction);
                ApplyEdgeCaseValidation(upperText, unblockAction);
                actions.Add(unblockAction);
            }

            // Detect document directives
            if (ContainsDocumentDirective(upperText))
            {
                var documentAction = new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Document,
                    ExpedienteOrigen = expediente?.NumeroExpediente,
                    OficioOrigen = expediente?.NumeroOficio,
                    Confidence = CalculateConfidence(upperText, DocumentKeywords)
                };
                actions.Add(documentAction);
            }

            // Detect transfer directives
            if (ContainsTransferDirective(upperText))
            {
                var transferAction = new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Transfer,
                    ExpedienteOrigen = expediente?.NumeroExpediente,
                    OficioOrigen = expediente?.NumeroOficio,
                    Confidence = CalculateConfidence(upperText, TransferKeywords),
                    DocumentRelationType = documentRelationType
                };
                ExtractActionDetails(upperText, transferAction);
                ApplyEdgeCaseValidation(upperText, transferAction);
                actions.Add(transferAction);
            }

            // Detect information directives
            if (ContainsInformationDirective(upperText))
            {
                var informationAction = new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Information,
                    ExpedienteOrigen = expediente?.NumeroExpediente,
                    OficioOrigen = expediente?.NumeroOficio,
                    Confidence = CalculateConfidence(upperText, InformationKeywords)
                };
                actions.Add(informationAction);
            }

            // If no specific directives found, classify as Ignore
            if (actions.Count == 0)
            {
                actions.Add(new ComplianceAction
                {
                    ActionType = ComplianceActionKind.Ignore,
                    ExpedienteOrigen = expediente?.NumeroExpediente,
                    OficioOrigen = expediente?.NumeroOficio,
                    Confidence = 50
                });
            }

            _logger.LogDebug("Classified {Count} compliance actions", actions.Count);
            return Task.FromResult(Result<List<ComplianceAction>>.Success(actions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying legal directives");
            return Task.FromResult(Result<List<ComplianceAction>>.WithFailure($"Error classifying legal directives: {ex.Message}", default(List<ComplianceAction>), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<List<string>>> DetectLegalInstrumentsAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<List<string>>.WithFailure("Operation was cancelled."));
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Document text cannot be null or empty for legal instrument detection");
            return Task.FromResult(Result<List<string>>.WithFailure("Document text cannot be null or empty."));
        }

        try
        {
            _logger.LogDebug("Detecting legal instruments in document text");

            var instruments = new List<string>();
            
            // Pattern for Acuerdo (e.g., "Acuerdo 105/2021")
            var acuerdoPattern = new Regex(@"Acuerdo\s+(\d+/\d{4})", RegexOptions.IgnoreCase);
            var acuerdoMatches = acuerdoPattern.Matches(documentText);
            foreach (Match match in acuerdoMatches)
            {
                instruments.Add($"Acuerdo {match.Groups[1].Value}");
            }

            // Pattern for Ley (e.g., "Ley 123/2020")
            var leyPattern = new Regex(@"Ley\s+(\d+/\d{4})", RegexOptions.IgnoreCase);
            var leyMatches = leyPattern.Matches(documentText);
            foreach (Match match in leyMatches)
            {
                instruments.Add($"Ley {match.Groups[1].Value}");
            }

            // Pattern for Circular (e.g., "Circular 456/2022")
            var circularPattern = new Regex(@"Circular\s+(\d+/\d{4})", RegexOptions.IgnoreCase);
            var circularMatches = circularPattern.Matches(documentText);
            foreach (Match match in circularMatches)
            {
                instruments.Add($"Circular {match.Groups[1].Value}");
            }

            _logger.LogDebug("Detected {Count} legal instruments", instruments.Count);
            return Task.FromResult(Result<List<string>>.Success(instruments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting legal instruments");
            return Task.FromResult(Result<List<string>>.WithFailure($"Error detecting legal instruments: {ex.Message}", default(List<string>), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<ComplianceAction>> MapToComplianceActionAsync(
        string directiveText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<ComplianceAction>.WithFailure("Operation was cancelled."));
        }

        if (string.IsNullOrWhiteSpace(directiveText))
        {
            _logger.LogWarning("Directive text cannot be null or empty for mapping");
            return Task.FromResult(Result<ComplianceAction>.WithFailure("Directive text cannot be null or empty."));
        }

        try
        {
            _logger.LogDebug("Mapping directive to compliance action: {Directive}", directiveText.Substring(0, Math.Min(100, directiveText.Length)));

            var upperText = directiveText.ToUpperInvariant();

            // Use precedence-based classification to handle ambiguous documents
            var actionType = DetermineActionTypeWithPrecedence(upperText);

            // Calculate confidence based on detected action type
            int confidence;
            if (actionType == ComplianceActionKind.Block)
            {
                confidence = CalculateConfidence(upperText, BlockKeywords);
            }
            else if (actionType == ComplianceActionKind.Unblock)
            {
                confidence = CalculateConfidence(upperText, UnblockKeywords);
            }
            else if (actionType == ComplianceActionKind.Document)
            {
                confidence = CalculateConfidence(upperText, DocumentKeywords);
            }
            else if (actionType == ComplianceActionKind.Transfer)
            {
                confidence = CalculateConfidence(upperText, TransferKeywords);
            }
            else if (actionType == ComplianceActionKind.Information)
            {
                confidence = CalculateConfidence(upperText, InformationKeywords);
            }
            else if (actionType == ComplianceActionKind.Unknown)
            {
                confidence = 30;
            }
            else
            {
                confidence = 50;
            }

            var action = new ComplianceAction
            {
                ActionType = actionType,
                ExpedienteOrigen = expediente?.NumeroExpediente,
                OficioOrigen = expediente?.NumeroOficio,
                Confidence = confidence,
                DocumentRelationType = DetectDocumentRelationType(upperText)
            };

            ExtractActionDetails(upperText, action);
            ApplyEdgeCaseValidation(upperText, action);

            _logger.LogDebug("Mapped directive to {ActionType} with confidence {Confidence}%", actionType, confidence);
            return Task.FromResult(Result<ComplianceAction>.Success(action));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping directive to compliance action");
            return Task.FromResult(Result<ComplianceAction>.WithFailure($"Error mapping directive to compliance action: {ex.Message}", default(ComplianceAction), ex));
        }
    }

    private static readonly string[] BlockKeywords = { "BLOQUEO", "EMBARGO", "ASEGURAR", "CONGELAR", "RETENER", "INMOVILIZAR" };
    private static readonly string[] UnblockKeywords = { "DESBLOQUEO", "DESEMBARGO", "LIBERAR", "DESCONGELAR", "DESRETENER" };
    private static readonly string[] DocumentKeywords = { "DOCUMENTACIÓN", "DOCUMENTOS", "EXPEDIR", "ENTREGAR DOCUMENTOS" };
    private static readonly string[] TransferKeywords = { "TRANSFERENCIA", "TRANSFERIR", "MOVIMIENTO", "GIRO" };
    private static readonly string[] InformationKeywords = { "INFORMACIÓN", "INFORMAR", "REPORTAR", "COMUNICAR" };

    private static bool ContainsBlockDirective(string text) => BlockKeywords.Any(keyword => text.Contains(keyword));
    private static bool ContainsUnblockDirective(string text) => UnblockKeywords.Any(keyword => text.Contains(keyword));
    private static bool ContainsDocumentDirective(string text) => DocumentKeywords.Any(keyword => text.Contains(keyword));
    private static bool ContainsTransferDirective(string text) => TransferKeywords.Any(keyword => text.Contains(keyword));
    private static bool ContainsInformationDirective(string text) => InformationKeywords.Any(keyword => text.Contains(keyword));

    /// <summary>
    /// Determines the action type with precedence rules when multiple keywords are present.
    /// Implements priority-based classification to handle ambiguous documents.
    /// </summary>
    /// <param name="text">The document text (already uppercased).</param>
    /// <returns>The highest priority action type detected.</returns>
    /// <remarks>
    /// Priority order:
    /// 1. Unblock (highest - e.g., "desbloquear el aseguramiento" is Unblock, not Block)
    /// 2. Block, Transfer, Document (specific operations)
    /// 3. Information (default for general requests)
    /// 4. Unknown (flag for review)
    /// </remarks>
    private static ComplianceActionKind DetermineActionTypeWithPrecedence(string text)
    {
        // PRIORITY 1: Unblock takes precedence over Block
        // Document saying "desbloquear el aseguramiento" is an Unblock, not a Block
        if (ContainsUnblockDirective(text))
        {
            return ComplianceActionKind.Unblock;
        }

        // PRIORITY 2: Specific operations
        if (ContainsBlockDirective(text))
        {
            return ComplianceActionKind.Block;
        }

        if (ContainsTransferDirective(text))
        {
            return ComplianceActionKind.Transfer;
        }

        if (ContainsDocumentDirective(text))
        {
            return ComplianceActionKind.Document;
        }

        // PRIORITY 3: Information request (default for general requests)
        if (ContainsInformationDirective(text))
        {
            return ComplianceActionKind.Information;
        }

        // PRIORITY 4: Unknown (flag for review)
        return ComplianceActionKind.Unknown;
    }

    /// <summary>
    /// Detects the document relation type to determine how to process relative to existing requirements.
    /// Helps avoid duplicate processing of reminders and properly link related documents.
    /// </summary>
    /// <param name="text">The document text (already uppercased).</param>
    /// <returns>The detected document relation type.</returns>
    private static DocumentRelationType DetectDocumentRelationType(string text)
    {
        // Check for Recordatorio (reminder) - do not duplicate processing
        if (text.Contains("RECORDATORIO DEL OFICIO") ||
            text.Contains("RECORDATORIO DE OFICIO") ||
            text.Contains("RECORDATORIO AL OFICIO"))
        {
            return DocumentRelationType.Recordatorio;
        }

        // Check for Alcance (scope expansion) - create new record linked to original
        if (text.Contains("ALCANCE AL OFICIO") ||
            text.Contains("ALCANCE DE OFICIO") ||
            text.Contains("ALCANCE DEL OFICIO") ||
            text.Contains("AMPLÍA") ||
            text.Contains("AMPLIA") ||
            text.Contains("AMPLIACIÓN"))
        {
            return DocumentRelationType.Alcance;
        }

        // Check for Precisión (clarification) - update existing record
        if (text.Contains("PRECISIÓN") ||
            text.Contains("PRECISION") ||
            text.Contains("ACLARA") ||
            text.Contains("ACLARACIÓN") ||
            text.Contains("ACLARACION") ||
            text.Contains("CORRIGE") ||
            text.Contains("CORRECCIÓN") ||
            text.Contains("CORRECCION"))
        {
            return DocumentRelationType.Precision;
        }

        // Default: New requirement
        return DocumentRelationType.NewRequirement;
    }

    private static int CalculateConfidence(string text, string[] keywords)
    {
        var matches = keywords.Count(keyword => text.Contains(keyword));
        if (matches == 0)
        {
            return 0;
        }

        // Higher confidence with more keyword matches
        var baseConfidence = 60 + (matches * 10);
        return Math.Min(100, baseConfidence);
    }

    private static void ExtractActionDetails(string text, ComplianceAction action)
    {
        // Extract account number pattern (e.g., "cuenta 1234567890")
        // Use word boundary to avoid matching numbers that are part of amounts
        var accountPattern = new Regex(@"cuenta\s+(\d{4,})", RegexOptions.IgnoreCase);
        var accountMatch = accountPattern.Match(text);
        if (accountMatch.Success)
        {
            action.AccountNumber = accountMatch.Groups[1].Value;
        }

        // Extract amount pattern - prioritize monetary amounts with currency symbols or explicit monetary context
        // Pattern 1: Amounts with dollar sign: "$1,000,000.00" or "$ 1,000,000.00"
        // Pattern 2: Amounts with "monto", "cantidad", "importe" keywords: "monto de $1,000,000.00" or "monto de 1,000,000.00"
        // Pattern 3: Amounts with "pesos", "dolares": "1,000,000.00 pesos"
        // Pattern 4: Formatted amounts with commas (at least 2 commas to avoid matching account numbers): "1,000,000.00"
        var amountPatterns = new[]
        {
            // Pattern 1: Dollar sign amounts (highest priority) - matches "$1,000,000.00" or "$ 1,000,000.00"
            new Regex(@"\$\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", RegexOptions.IgnoreCase),
            // Pattern 2: Amounts with monetary keywords followed by dollar sign or formatted number
            // Matches "monto de $1,000,000.00" or "monto de 1,000,000.00"
            new Regex(@"(?:monto|cantidad|importe|suma)\s+(?:de\s+)?\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", RegexOptions.IgnoreCase),
            // Pattern 3: Amounts with currency words
            new Regex(@"(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s+(?:pesos|dolares|dólares)", RegexOptions.IgnoreCase),
            // Pattern 4: Formatted amounts with at least 2 commas (to distinguish from account numbers)
            new Regex(@"(\d{1,3}(?:,\d{3}){2,}(?:\.\d{2})?)", RegexOptions.IgnoreCase)
        };
        
        Match? bestMatch = null;
        int bestPatternPriority = int.MaxValue;
        
        // Try each pattern in priority order
        for (int i = 0; i < amountPatterns.Length; i++)
        {
            var matches = amountPatterns[i].Matches(text);
            foreach (Match match in matches)
            {
                // Skip if this looks like an account number (all digits, no formatting context)
                var amountValue = match.Groups[1].Value;
                var digitsOnly = amountValue.Replace(",", "").Replace(".", "");
                
                // Skip if it's a long number without commas/formatting that could be an account number
                if (digitsOnly.Length >= 10 && 
                    !match.Value.Contains("$") && 
                    !match.Value.Contains("monto", StringComparison.OrdinalIgnoreCase) &&
                    !match.Value.Contains("cantidad", StringComparison.OrdinalIgnoreCase) &&
                    !match.Value.Contains("importe", StringComparison.OrdinalIgnoreCase) &&
                    !match.Value.Contains("pesos", StringComparison.OrdinalIgnoreCase) &&
                    !match.Value.Contains("dolares", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Likely an account number, skip it
                }
                
                // Prefer matches from higher priority patterns (lower index)
                // Also prefer longer matches (more complete amounts)
                if (bestMatch == null || 
                    (i < bestPatternPriority) ||
                    (i == bestPatternPriority && match.Value.Length > bestMatch.Value.Length))
                {
                    bestMatch = match;
                    bestPatternPriority = i;
                }
            }
        }
        
        if (bestMatch != null)
        {
            // Extract the numeric part from group 1 (all patterns use group 1 for the amount)
            var amountString = bestMatch.Groups[1].Value.Replace(",", string.Empty);
            if (decimal.TryParse(amountString, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                action.Amount = amount;
            }
        }

        // Extract product type (simplified - could be enhanced)
        if (text.Contains("TARJETA", StringComparison.OrdinalIgnoreCase))
        {
            action.ProductType = "TARJETA";
        }
        else if (text.Contains("CUENTA", StringComparison.OrdinalIgnoreCase))
        {
            action.ProductType = "CUENTA";
        }
    }

    /// <summary>
    /// Applies edge case validation to detect potential issues requiring manual review.
    /// Adds warnings and sets RequiresManualReview flag when edge cases are detected.
    /// </summary>
    /// <param name="text">The document text (already uppercased).</param>
    /// <param name="action">The compliance action to validate.</param>
    private static void ApplyEdgeCaseValidation(string text, ComplianceAction action)
    {
        // Edge Case 1: Transfer without CLABE (18-digit account)
        if (action.ActionType == ComplianceActionKind.Transfer)
        {
            var clabePattern = new Regex(@"\b\d{18}\b");
            if (!clabePattern.IsMatch(text))
            {
                action.Warnings.Add("Missing CLABE - Transferencia requires 18-digit CLABE account number");
                action.RequiresManualReview = true;
            }
        }

        // Edge Case 2: Unblock without prior order reference
        if (action.ActionType == ComplianceActionKind.Unblock)
        {
            // Look for reference to prior order (oficio, expediente, etc.)
            var hasPriorReference = text.Contains("OFICIO") ||
                                   text.Contains("EXPEDIENTE") ||
                                   text.Contains("ORDEN") ||
                                   text.Contains("ANTERIOR");

            if (!hasPriorReference)
            {
                action.Warnings.Add("Missing prior order reference - Desbloqueo should reference original blocking order");
                action.RequiresManualReview = true;
            }
        }

        // Edge Case 3: Block without account or amount
        if (action.ActionType == ComplianceActionKind.Block)
        {
            if (string.IsNullOrWhiteSpace(action.AccountNumber) && !action.Amount.HasValue)
            {
                action.Warnings.Add("Missing account or amount - Aseguramiento should specify what to block");
                action.RequiresManualReview = true;
            }
        }

        // Edge Case 4: Low confidence threshold
        if (action.Confidence < 70)
        {
            action.Warnings.Add($"Low classification confidence ({action.Confidence}%) - Review recommended");
            action.RequiresManualReview = true;
        }

        // Edge Case 5: Multiple action types detected (ambiguous classification)
        // This will be detected by the calling method since it creates multiple actions
        // We'll handle this in Gap 2 with precedence rules
    }
}


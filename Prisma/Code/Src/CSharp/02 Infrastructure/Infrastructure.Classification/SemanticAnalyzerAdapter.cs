using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Adapter that implements ILegalDirectiveClassifier by wrapping ISemanticAnalyzer.
/// Provides backward compatibility for legacy code while using the new semantic analysis architecture.
/// </summary>
/// <remarks>
/// Architecture Notes:
/// - This is an ADAPTER pattern implementation that bridges legacy ILegalDirectiveClassifier with new ISemanticAnalyzer
/// - ISemanticAnalyzer returns rich SemanticAnalysis domain objects (the "WHAT" of requirements)
/// - ILegalDirectiveClassifier returns primitive List&lt;ComplianceAction&gt; (legacy interface)
/// - This adapter converts SemanticAnalysis → List&lt;ComplianceAction&gt; for backward compatibility
///
/// Design Philosophy:
/// - Allows gradual migration: Legacy consumers continue using ILegalDirectiveClassifier
/// - New consumers can inject ISemanticAnalyzer directly for richer semantics
/// - Eventually, ILegalDirectiveClassifier will be deprecated and this adapter removed
///
/// Mapping Strategy:
/// - SemanticAnalysis.RequiereBloqueo → ComplianceAction(Block)
/// - SemanticAnalysis.RequiereDesbloqueo → ComplianceAction(Unblock)
/// - SemanticAnalysis.RequiereTransferencia → ComplianceAction(Transfer)
/// - SemanticAnalysis.RequiereDocumentacion → ComplianceAction(Document)
/// - SemanticAnalysis.RequiereInformacionGeneral → ComplianceAction(Information)
/// - Confidence scores converted from 0.0-1.0 (double) to 0-100 (int)
/// </remarks>
public class SemanticAnalyzerAdapter : ILegalDirectiveClassifier
{
    private readonly ISemanticAnalyzer _semanticAnalyzer;
    private readonly ILogger<SemanticAnalyzerAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticAnalyzerAdapter"/> class.
    /// </summary>
    /// <param name="semanticAnalyzer">The semantic analyzer to wrap.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public SemanticAnalyzerAdapter(
        ISemanticAnalyzer semanticAnalyzer,
        ILogger<SemanticAnalyzerAdapter> logger)
    {
        _semanticAnalyzer = semanticAnalyzer;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Implementation delegates to ISemanticAnalyzer.AnalyzeDirectivesAsync and converts the result.
    /// </remarks>
    public async Task<Result<List<ComplianceAction>>> ClassifyDirectivesAsync(
        string documentText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<List<ComplianceAction>>.WithFailure("Operation was cancelled.");
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogWarning("Document text cannot be null or empty for classification");
            return Result<List<ComplianceAction>>.WithFailure("Document text cannot be null or empty.");
        }

        try
        {
            _logger.LogDebug(
                "Classifying directives via SemanticAnalyzer (documentLength: {Length}, expediente: {Expediente})",
                documentText.Length,
                expediente?.NumeroExpediente ?? "N/A");

            // Call the semantic analyzer
            var analysisResult = await _semanticAnalyzer.AnalyzeDirectivesAsync(
                documentText,
                expediente,
                cancellationToken);

            if (!analysisResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Semantic analysis failed: {Error}",
                    analysisResult.Error);
                return Result<List<ComplianceAction>>.WithFailure(
                    analysisResult.Error,
                    default(List<ComplianceAction>),
                    analysisResult.Exception);
            }

            // Convert SemanticAnalysis to List<ComplianceAction>
            var actions = ConvertToComplianceActions(analysisResult.Value!, expediente);

            _logger.LogDebug(
                "Successfully converted SemanticAnalysis to {Count} compliance action(s)",
                actions.Count);

            return Result<List<ComplianceAction>>.Success(actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying directives via SemanticAnalyzerAdapter");
            return Result<List<ComplianceAction>>.WithFailure(
                $"Error classifying directives: {ex.Message}",
                default(List<ComplianceAction>),
                ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Legal instrument detection is not implemented in the new semantic analyzer architecture.
    /// Returns empty list to maintain interface compatibility.
    /// </remarks>
    public Task<Result<List<string>>> DetectLegalInstrumentsAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<List<string>>.WithFailure("Operation was cancelled."));
        }

        _logger.LogDebug(
            "DetectLegalInstrumentsAsync called - feature not implemented in new architecture, returning empty list");

        // Legal instrument detection is not part of the new SemanticAnalyzer implementation
        // Return empty list for backward compatibility
        return Task.FromResult(Result<List<string>>.Success(new List<string>()));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Implementation delegates to ClassifyDirectivesAsync and returns the first action.
    /// If multiple actions are detected, logs a warning and returns the highest-confidence action.
    /// </remarks>
    public async Task<Result<ComplianceAction>> MapToComplianceActionAsync(
        string directiveText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<ComplianceAction>.WithFailure("Operation was cancelled.");
        }

        if (string.IsNullOrWhiteSpace(directiveText))
        {
            _logger.LogWarning("Directive text cannot be null or empty for mapping");
            return Result<ComplianceAction>.WithFailure("Directive text cannot be null or empty.");
        }

        try
        {
            _logger.LogDebug(
                "Mapping single directive to compliance action (length: {Length})",
                directiveText.Length);

            // Use ClassifyDirectivesAsync to analyze the text
            var classificationResult = await ClassifyDirectivesAsync(
                directiveText,
                expediente,
                cancellationToken);

            if (!classificationResult.IsSuccess)
            {
                return Result<ComplianceAction>.WithFailure(
                    classificationResult.Error,
                    default(ComplianceAction),
                    classificationResult.Exception);
            }

            var actions = classificationResult.Value!;

            if (actions.Count == 0)
            {
                _logger.LogWarning("No compliance actions detected in directive text");
                return Result<ComplianceAction>.WithFailure("No compliance actions detected in directive text.");
            }

            if (actions.Count > 1)
            {
                _logger.LogWarning(
                    "Multiple compliance actions detected ({Count}), returning highest-confidence action",
                    actions.Count);
            }

            // Return the action with highest confidence
            var bestAction = actions.OrderByDescending(a => a.Confidence).First();

            _logger.LogDebug(
                "Mapped directive to {ActionType} with confidence {Confidence}%",
                bestAction.ActionType.Name,
                bestAction.Confidence);

            return Result<ComplianceAction>.Success(bestAction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping directive to compliance action");
            return Result<ComplianceAction>.WithFailure(
                $"Error mapping directive to compliance action: {ex.Message}",
                default(ComplianceAction),
                ex);
        }
    }

    /// <summary>
    /// Converts SemanticAnalysis domain object to list of ComplianceAction entities.
    /// </summary>
    /// <param name="analysis">The semantic analysis result.</param>
    /// <param name="expediente">Optional expediente for context.</param>
    /// <returns>List of compliance actions derived from the semantic analysis.</returns>
    private List<ComplianceAction> ConvertToComplianceActions(
        Domain.ValueObjects.SemanticAnalysis analysis,
        Expediente? expediente)
    {
        var actions = new List<ComplianceAction>();

        // Map RequiereBloqueo → Block
        if (analysis.RequiereBloqueo != null && analysis.RequiereBloqueo.EsRequerido)
        {
            _logger.LogTrace(
                "Converting RequiereBloqueo to Block action (confidence: {Confidence:F2})",
                analysis.RequiereBloqueo.Confidence);

            var blockAction = new ComplianceAction
            {
                ActionType = ComplianceActionKind.Block,
                ExpedienteOrigen = expediente?.NumeroExpediente,
                OficioOrigen = expediente?.NumeroOficio,
                Confidence = ConvertConfidence(analysis.RequiereBloqueo.Confidence),
                Amount = analysis.RequiereBloqueo.Monto,
                AccountNumber = analysis.RequiereBloqueo.CuentasEspecificas.FirstOrDefault()
            };

            // Add specific accounts to additional data if multiple
            if (analysis.RequiereBloqueo.CuentasEspecificas.Count > 1)
            {
                blockAction.AdditionalData["CuentasEspecificas"] = analysis.RequiereBloqueo.CuentasEspecificas;
            }

            // Add product types to additional data if present
            if (analysis.RequiereBloqueo.ProductosEspecificos.Any())
            {
                blockAction.AdditionalData["ProductosEspecificos"] = analysis.RequiereBloqueo.ProductosEspecificos;
                blockAction.ProductType = analysis.RequiereBloqueo.ProductosEspecificos.FirstOrDefault();
            }

            actions.Add(blockAction);
        }

        // Map RequiereDesbloqueo → Unblock
        if (analysis.RequiereDesbloqueo != null && analysis.RequiereDesbloqueo.EsRequerido)
        {
            _logger.LogTrace(
                "Converting RequiereDesbloqueo to Unblock action (confidence: {Confidence:F2})",
                analysis.RequiereDesbloqueo.Confidence);

            var unblockAction = new ComplianceAction
            {
                ActionType = ComplianceActionKind.Unblock,
                ExpedienteOrigen = expediente?.NumeroExpediente,
                OficioOrigen = expediente?.NumeroOficio,
                Confidence = ConvertConfidence(analysis.RequiereDesbloqueo.Confidence)
            };

            actions.Add(unblockAction);
        }

        // Map RequiereTransferencia → Transfer
        if (analysis.RequiereTransferencia != null && analysis.RequiereTransferencia.EsRequerido)
        {
            _logger.LogTrace(
                "Converting RequiereTransferencia to Transfer action (confidence: {Confidence:F2})",
                analysis.RequiereTransferencia.Confidence);

            var transferAction = new ComplianceAction
            {
                ActionType = ComplianceActionKind.Transfer,
                ExpedienteOrigen = expediente?.NumeroExpediente,
                OficioOrigen = expediente?.NumeroOficio,
                Confidence = ConvertConfidence(analysis.RequiereTransferencia.Confidence)
            };

            actions.Add(transferAction);
        }

        // Map RequiereDocumentacion → Document
        if (analysis.RequiereDocumentacion != null && analysis.RequiereDocumentacion.EsRequerido)
        {
            _logger.LogTrace(
                "Converting RequiereDocumentacion to Document action (confidence: {Confidence:F2})",
                analysis.RequiereDocumentacion.Confidence);

            var documentAction = new ComplianceAction
            {
                ActionType = ComplianceActionKind.Document,
                ExpedienteOrigen = expediente?.NumeroExpediente,
                OficioOrigen = expediente?.NumeroOficio,
                Confidence = ConvertConfidence(analysis.RequiereDocumentacion.Confidence)
            };

            actions.Add(documentAction);
        }

        // Map RequiereInformacionGeneral → Information
        if (analysis.RequiereInformacionGeneral != null && analysis.RequiereInformacionGeneral.EsRequerido)
        {
            _logger.LogTrace(
                "Converting RequiereInformacionGeneral to Information action (confidence: {Confidence:F2})",
                analysis.RequiereInformacionGeneral.Confidence);

            var informationAction = new ComplianceAction
            {
                ActionType = ComplianceActionKind.Information,
                ExpedienteOrigen = expediente?.NumeroExpediente,
                OficioOrigen = expediente?.NumeroOficio,
                Confidence = ConvertConfidence(analysis.RequiereInformacionGeneral.Confidence)
            };

            actions.Add(informationAction);
        }

        return actions;
    }

    /// <summary>
    /// Converts confidence score from 0.0-1.0 (double) to 0-100 (int).
    /// </summary>
    /// <param name="confidence">Confidence score as double (0.0 to 1.0).</param>
    /// <returns>Confidence score as integer (0 to 100).</returns>
    private static int ConvertConfidence(double confidence)
    {
        // Convert 0.0-1.0 to 0-100 and round to nearest integer
        return (int)Math.Round(confidence * 100.0);
    }
}

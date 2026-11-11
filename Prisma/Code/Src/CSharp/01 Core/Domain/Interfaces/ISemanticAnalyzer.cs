namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Semantic analyzer for extracting structured requirements from legal directive text.
/// Uses fuzzy phrase matching and classification dictionaries to populate SemanticAnalysis domain objects.
/// </summary>
/// <remarks>
/// This is the NEW implementation that fixes the audit gap by:
/// - Returning rich SemanticAnalysis domain objects (not primitive List&lt;ComplianceAction&gt;)
/// - Using fuzzy string matching with ITextComparer (not naive keyword matching)
/// - Supporting dictionary-driven classification (extensible for future AI/vector approaches)
///
/// Design Philosophy:
/// - Domain-centric: Returns WHAT the case requires (SemanticAnalysis)
/// - Infrastructure-agnostic: Doesn't expose HOW classification works
/// - Testable: Accepts expediente context for better classification
///
/// Replaces: ILegalDirectiveClassifier.ClassifyDirectivesAsync (primitive approach)
/// </remarks>
public interface ISemanticAnalyzer
{
    /// <summary>
    /// Analyzes legal directive text and returns structured semantic requirements.
    /// </summary>
    /// <param name="documentText">The legal directive document text to analyze.</param>
    /// <param name="expediente">Optional expediente context for enhanced classification accuracy.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A result containing SemanticAnalysis with populated requirement objects:
    /// - RequiereBloqueo (asset freeze details)
    /// - RequiereDesbloqueo (asset unfreeze details)
    /// - RequiereDocumentacion (document request details)
    /// - RequiereTransferencia (transfer order details)
    /// - RequiereInformacionGeneral (general information request details)
    /// Each requirement includes confidence scores from the classifier.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await semanticAnalyzer.AnalyzeDirectivesAsync(ocrText, expediente, ct);
    /// if (result.IsSuccess)
    /// {
    ///     var analysis = result.Value;
    ///     if (analysis.RequiereBloqueo?.EsRequerido == true)
    ///     {
    ///         // Handle asset freeze requirement
    ///         var confidence = analysis.RequiereBloqueo.Confidence;
    ///         var accounts = analysis.RequiereBloqueo.CuentasEspecificas;
    ///     }
    /// }
    /// </code>
    /// </example>
    Task<Result<SemanticAnalysis>> AnalyzeDirectivesAsync(
        string documentText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default);
}

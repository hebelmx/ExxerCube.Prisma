namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the legal directive classifier service for classifying legal directives from document text and mapping clauses to compliance actions.
/// </summary>
public interface ILegalDirectiveClassifier
{
    /// <summary>
    /// Classifies legal directives from document text, mapping clauses to compliance actions (block, unblock, document, transfer, information, ignore).
    /// </summary>
    /// <param name="documentText">The document text to classify.</param>
    /// <param name="expediente">The expediente information for context (optional).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of compliance actions with confidence scores or an error.</returns>
    Task<Result<List<ComplianceAction>>> ClassifyDirectivesAsync(
        string documentText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects references to legal instruments (e.g., Acuerdo 105/2021) in document text.
    /// </summary>
    /// <param name="documentText">The document text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of detected legal instrument references or an error.</returns>
    Task<Result<List<string>>> DetectLegalInstrumentsAsync(
        string documentText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps classified directives to specific compliance actions with confidence scores.
    /// </summary>
    /// <param name="directiveText">The directive text to map.</param>
    /// <param name="expediente">The expediente information for context (optional).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the compliance action with confidence score or an error.</returns>
    Task<Result<ComplianceAction>> MapToComplianceActionAsync(
        string directiveText,
        Expediente? expediente = null,
        CancellationToken cancellationToken = default);
}


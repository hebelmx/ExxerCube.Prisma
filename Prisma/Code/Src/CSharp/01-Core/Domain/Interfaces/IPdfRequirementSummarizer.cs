namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the PDF requirement summarizer service for extracting and categorizing compliance requirements from PDF documents.
/// </summary>
public interface IPdfRequirementSummarizer
{
    /// <summary>
    /// Summarizes PDF content into requirement categories (bloqueo, desbloqueo, documentacion, transferencia, informacion).
    /// Uses semantic analysis or rule-based classification to categorize requirements.
    /// </summary>
    /// <param name="pdfContent">The PDF file content as a byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the requirement summary with categorized requirements or an error.</returns>
    Task<Result<RequirementSummary>> SummarizeRequirementsAsync(
        byte[] pdfContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes requirements from extracted PDF text content.
    /// </summary>
    /// <param name="pdfText">The extracted text content from the PDF.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the requirement summary with categorized requirements or an error.</returns>
    Task<Result<RequirementSummary>> SummarizeRequirementsFromTextAsync(
        string pdfText,
        CancellationToken cancellationToken = default);
}


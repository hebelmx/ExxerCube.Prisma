namespace ExxerCube.Prisma.Domain.Interfaces
{
    /// <summary>
    /// Application service for orchestrating field extraction and matching across XML, DOCX, and PDF sources.
    /// </summary>
    public interface IFieldMatchingService
    {
        /// <summary>
        /// Orchestrates field extraction and matching across XML, DOCX, and PDF sources, generating a unified metadata record.
        /// </summary>
        /// <param name="docxSource">The DOCX document source (optional).</param>
        /// <param name="pdfSource">The PDF document source (optional).</param>
        /// <param name="xmlSource">The XML document source (optional).</param>
        /// <param name="fieldDefinitions">The field definitions specifying which fields to extract and match.</param>
        /// <param name="expediente">The expediente information (optional, may be extracted from XML).</param>
        /// <param name="classification">The classification result (optional).</param>
        /// <param name="requiredFields">The list of required field names for validation (optional).</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A result containing the unified metadata record or an error.</returns>
        Task<Result<UnifiedMetadataRecord>> MatchFieldsAndGenerateUnifiedRecordAsync(
            DocxSource? docxSource,
            PdfSource? pdfSource,
            XmlSource? xmlSource,
            FieldDefinition[] fieldDefinitions,
            Expediente? expediente = null,
            ClassificationResult? classification = null,
            List<string>? requiredFields = null,
            CancellationToken cancellationToken = default);
    }
}
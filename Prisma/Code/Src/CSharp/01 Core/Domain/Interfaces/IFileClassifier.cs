namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the file classifier service for classifying documents into regulatory categories using deterministic rules.
/// </summary>
public interface IFileClassifier
{
    /// <summary>
    /// Classifies a document into Level 1 categories (Aseguramiento, Desembargo, Documentacion, etc.) and Level 2/3 subcategories (Especial, Judicial, Hacendario) based on metadata.
    /// </summary>
    /// <param name="metadata">The extracted metadata to classify.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the classification result with confidence scores or an error.</returns>
    Task<Result<ClassificationResult>> ClassifyAsync(
        ExtractedMetadata metadata,
        CancellationToken cancellationToken = default);
}


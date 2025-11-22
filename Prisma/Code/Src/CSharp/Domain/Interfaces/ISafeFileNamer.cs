namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the safe file namer service for generating safe, normalized file names.
/// </summary>
public interface ISafeFileNamer
{
    /// <summary>
    /// Generates a safe, normalized file name based on classification and metadata.
    /// </summary>
    /// <param name="originalFileName">The original filename.</param>
    /// <param name="classification">The document classification result.</param>
    /// <param name="metadata">The extracted metadata (optional).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the safe file name or an error.</returns>
    Task<Result<string>> GenerateSafeFileNameAsync(
        string originalFileName,
        ClassificationResult classification,
        ExtractedMetadata? metadata = null,
        CancellationToken cancellationToken = default);
}

